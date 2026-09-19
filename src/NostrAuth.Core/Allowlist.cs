using System.Text.Json;
using NBitcoin.Secp256k1;
using NNostr.Client;
using NNostr.Client.Protocols;

namespace NostrAuth.Core;

/// <summary>Authorization decision for one user pubkey against the cached allowlist.</summary>
public record AllowlistDecision(bool Allowed, string? Reason)
{
    public const string NotInAllowlist = "not_in_allowlist";
    public const string AllowlistStale = "allowlist_stale";
    public const string AllowlistUnavailable = "allowlist_unavailable";
}

/// <summary>Outcome of merging relay-fetched allowlist events.</summary>
/// <param name="AuthorizedPubkeysHex">The pubkeys now authorized, empty when no valid event was seen.</param>
/// <param name="PublicListWarning">Whether the winning event was plaintext (public-list warning for the dashboard).</param>
/// <param name="EventCreatedAtUnix">The winning event's created_at, null when no valid event was seen; feeds staleness policy.</param>
public sealed record AllowlistMergeResult(
    IReadOnlyCollection<string> AuthorizedPubkeysHex,
    bool PublicListWarning,
    long? EventCreatedAtUnix);

/// <summary>Feed relay-fetched allowlist events into the sync layer.</summary>
public interface IAllowlistSync
{
    /// <summary>
    /// Processes candidate events for the configured list npub: drops events
    /// authored by another key or with an invalid signature, keeps the newest,
    /// decrypts NIP-44 private content or falls back to plaintext p-tags.
    /// </summary>
    AllowlistMergeResult Merge(IReadOnlyList<NostrEvent> candidates);
}

/// <summary>Cached state of the decrypted allowlist.</summary>
/// <param name="AuthorizedPubkeysHex">The pubkeys currently authorized.</param>
/// <param name="EventCreatedAtUnix">The winning event's created_at; feeds staleness policy.</param>
/// <param name="RetrievedAtUnix">When the winning event was fetched; dashboard context.</param>
/// <param name="PublicListWarning">Whether the winning event was plaintext (dashboard warning).</param>
public sealed record AllowlistSnapshot(
    IReadOnlyCollection<string> AuthorizedPubkeysHex,
    long EventCreatedAtUnix,
    long RetrievedAtUnix,
    bool PublicListWarning = false);

/// <summary>Checks a user pubkey against the cached allowlist, including staleness policy.</summary>
public interface IAllowlistEvaluator
{
    AllowlistDecision Check(string userPubkeyHex, long nowUnixSeconds);
}

/// <summary>
/// Merges relay-fetched allowlist events per the spec (docs/design.md
/// "Allowlist sync"): signature verified against the list npub, newest
/// created_at wins, NIP-44 decrypt-to-self with the list nsec, plaintext
/// p-tag fallback, kind 10000 (NIP-51 private list) and kind 30078
/// (d="jellyfin-allowlist") both supported.
/// </summary>
public sealed class AllowlistSync : IAllowlistSync
{
    public const int KindPrivateList = 10000;
    public const int KindAppData = 30078;
    public const string AppDataDTag = "jellyfin-allowlist";

    private readonly ECPrivKey _listKey;
    private readonly string _listPubkeyHex;

    public AllowlistSync(string listNsec, string listPubkeyHex)
    {
        _listKey = NIP19.FromNIP19Nsec(listNsec);
        _listPubkeyHex = listPubkeyHex.ToLowerInvariant();
    }

    public AllowlistMergeResult Merge(IReadOnlyList<NostrEvent> candidates)
    {
        var winner = candidates
            .Where(IsAuthoritative)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefault();

        if (winner is null)
        {
            return new(Array.Empty<string>(), false, null);
        }

        if (winner.Kind == KindPrivateList || winner.Kind == KindAppData)
        {
            var (pubkeys, isPlaintext) = Read(winner);
            if (pubkeys is not null)
            {
                return new(pubkeys, isPlaintext, winner.CreatedAt?.ToUnixTimeSeconds());
            }
        }

        return new(Array.Empty<string>(), false, winner.CreatedAt?.ToUnixTimeSeconds());
    }

    private bool IsAuthoritative(NostrEvent e)
        => string.Equals(e.PublicKey?.ToLowerInvariant(), _listPubkeyHex, StringComparison.Ordinal)
           && e.Verify();

    /// <summary>Decrypts NIP-44 content or falls back to plaintext tags.</summary>
    /// <returns>Authorized pubkeys and whether the source was plaintext.</returns>
    private (IReadOnlyCollection<string>? Pubkeys, bool IsPlaintext) Read(NostrEvent ev)
    {
        if (!string.IsNullOrEmpty(ev.Content))
        {
            try
            {
                var selfPub = NostrExtensions.ParsePubKey(ev.PublicKey);
                var plaintext = NIP44.Decrypt(_listKey, selfPub, ev.Content);

                // NIP-51 private items: stringified JSON tags-array [["p", hex], ...]
                try
                {
                    if (JsonSerializer.Deserialize<string[][]>(plaintext, NostrEventJson.Options) is { } tagArray
                        && Extract(tagArray) is { } fromTags)
                    {
                        return (fromTags, false);
                    }
                }
                catch (JsonException)
                {
                    // not the NIP-51 tags-array shape; try the app-data form
                }

                // kind 30078 app-data form: {"allowed": [hex, ...]}
                try
                {
                    if (JsonSerializer.Deserialize<AppData>(plaintext, NostrEventJson.Options) is { } appData
                        && appData.Allowed.Count > 0)
                    {
                        return (Sanitize(appData.Allowed), false);
                    }
                }
                catch (JsonException)
                {
                    // unknown encrypted payload shape
                }
            }
            catch (Exception)
            {
                // Not decryptable with the list key — fall through to plaintext.
            }
        }

        // Plaintext fallback: p-tags directly on the event.
        var plaintextPubkeys = Sanitize(ev.GetTaggedPublicKeys());
        return plaintextPubkeys.Count > 0 ? (plaintextPubkeys, true) : (null, true);
    }

    private static IReadOnlyCollection<string>? Extract(IEnumerable<IEnumerable<string>> tags)
        => Sanitize(tags
            .Where(t => t.FirstOrDefault() == "p")
            .Select(t => t.Skip(1).FirstOrDefault())
            .OfType<string>());

    private static IReadOnlyCollection<string> Sanitize(IEnumerable<string?> pubkeys)
        => pubkeys
            .Where(k => k is not null && k.Length == 64 && k.All(Uri.IsHexDigit))
            .Select(k => k!.ToLowerInvariant())
            .Distinct()
            .ToList();

    private sealed record AppData(IReadOnlyCollection<string> Allowed);
}

/// <summary>
/// Evaluates a pubkey against the allowlist cache per the spec
/// ("Replaceable-event selection and staleness"): allowed while the winning
/// event's age is within max-age; stale beyond it; unavailable when there is
/// no cache at all.
/// </summary>
public sealed class AllowlistEvaluator : IAllowlistEvaluator
{
    private readonly Func<AllowlistSnapshot?> _snapshot;
    private readonly long _maxAgeSeconds;

    public AllowlistEvaluator(Func<AllowlistSnapshot?> snapshot, long maxAgeSeconds)
    {
        _snapshot = snapshot;
        _maxAgeSeconds = maxAgeSeconds;
    }

    public AllowlistDecision Check(string userPubkeyHex, long nowUnixSeconds)
    {
        var snapshot = _snapshot();
        if (snapshot is null)
        {
            return new(false, AllowlistDecision.AllowlistUnavailable);
        }

        if (nowUnixSeconds - snapshot.EventCreatedAtUnix > _maxAgeSeconds)
        {
            return new(false, AllowlistDecision.AllowlistStale);
        }

        var allowed = snapshot.AuthorizedPubkeysHex.Contains(
            userPubkeyHex.ToLowerInvariant(),
            StringComparer.OrdinalIgnoreCase);
        return allowed
            ? new(true, null)
            : new(false, AllowlistDecision.NotInAllowlist);
    }
}
