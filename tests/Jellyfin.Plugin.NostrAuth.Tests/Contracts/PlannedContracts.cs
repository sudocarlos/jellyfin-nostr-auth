// Planned public contracts for the Jellyfin Nostr Auth plugin, expressed at the
// minimum surface needed to encode behavior expectations. These are the API shapes
// the spec (docs/design.md) promises; the implementation either satisfies them or
// the tests are rewired in the same change. Nothing here references Jellyfin —
// that wiring happens at integration.

namespace Jellyfin.Plugin.NostrAuth.Tests.Contracts;

/// <summary>A minimal NIP-01 event shape as delivered by relays.</summary>
public record NostrEvent(
    long Kind,
    long CreatedAt,
    string PubkeyHex,
    string SignatureHex,
    string Content,
    IReadOnlyList<IReadOnlyList<string>> Tags)
{
    public static NostrEvent FromJson(System.Text.Json.JsonElement e) => new(
        e.GetProperty("kind").GetInt64(),
        e.GetProperty("created_at").GetInt64(),
        e.GetProperty("pubkey").GetString()!,
        e.GetProperty("sig").GetString()!,
        e.GetProperty("content").GetString()!,
        e.GetProperty("tags").EnumerateArray()
            .Select(t => (IReadOnlyList<string>)t.EnumerateArray().Select(x => x.GetString()!).ToList())
            .ToList());
}

/// <summary>Outcome of verifying a NIP-98 Authorization header.</summary>
public record Nip98Result(bool Accepted, string? Reason, string? PubkeyHex)
{
    public const string InvalidEvent = "invalid_event";
    public const string ExpiredEvent = "expired_event";
    public const string UrlMismatch = "url_mismatch";
    public const string MethodMismatch = "method_mismatch";
}

/// <summary>Verifies NIP-98 signed HTTP auth events (kind 27235).</summary>
public interface INip98Verifier
{
    /// <param name="authHeader">Raw Authorization header, e.g. "Nostr &lt;base64 event&gt;".</param>
    /// <param name="requestUrl">The request's absolute URL (NIP-98 u tag must equal this exactly).</param>
    /// <param name="httpMethod">HTTP method of the request.</param>
    /// <param name="requestBody">Raw body bytes as string, or null; hashed when the event carries a payload tag.</param>
    /// <param name="nowUnixSeconds">Verifier time; freshness window is relative to this.</param>
    Nip98Result Verify(string? authHeader, string requestUrl, string httpMethod, string? requestBody, long nowUnixSeconds);
}

/// <summary>Authorization decision for one user pubkey against the cached allowlist.</summary>
public record AllowlistDecision(bool Allowed, string? Reason)
{
    public const string NotInAllowlist = "not_in_allowlist";
    public const string AllowlistStale = "allowlist_stale";
    public const string AllowlistUnavailable = "allowlist_unavailable";
}

/// <summary>
/// Checks a user pubkey against the decrypted allowlist cache, including staleness
/// and fail-open/closed policy. Implementations poll the configured relays on their
/// own cadence; evaluation here is against the cache state.
/// </summary>
public interface IAllowlistEvaluator
{
    AllowlistDecision Check(string userPubkeyHex, long nowUnixSeconds);
}

/// <summary>Feed parsed allowlist events into the sync layer (newest-wins, decrypt, cache).</summary>
public interface IAllowlistSync
{
    /// <summary>Processes relay-fetched candidate events for the configured list npub.</summary>
    /// <returns>The pubkeys now authorized, and whether the winning event was plaintext (public-list warning).</summary>
    (IReadOnlyCollection<string> AuthorizedPubkeysHex, bool PublicListWarning) Merge(IReadOnlyList<NostrEvent> candidates);
}

/// <summary>Minimal user record for contract tests; real wiring maps to Jellyfin's User.</summary>
public record UserRecord(
    Guid Id,
    string Name,
    string? PasswordHash,
    string? AuthenticationProviderId);

/// <summary>Maps an authorized npub to a Jellyfin user, creating it on first login.</summary>
public interface IUserProvisioner
{
    /// <summary>Resolves the Jellyfin user for an npub, creating it if absent.
    /// Created users get an unusable random password and AuthenticationProviderId = plugin.</summary>
    (UserRecord User, bool Created) ResolveOrCreate(string userPubkeyHex);
}
