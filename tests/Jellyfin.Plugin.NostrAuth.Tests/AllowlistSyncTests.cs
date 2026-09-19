using System.Text.Json;
using Jellyfin.Plugin.NostrAuth.Tests.TestSupport;
using NostrAuth.Core;
using Xunit;
using NNostrEvent = NNostr.Client.NostrEvent;

namespace Jellyfin.Plugin.NostrAuth.Tests;

/// <summary>
/// Allowlist sync and staleness behavior against the real AllowlistSync and
/// AllowlistEvaluator (docs/design.md, "Allowlist sync", "Replaceable-event
/// selection and staleness"). Cases come verbatim from fixtures/allowlist.json.
/// </summary>
public class AllowlistSyncTests
{
    [Fact]
    public void merges_encrypted_private_list_and_authorizes_allowed_npub()
    {
        var sync = Sync();
        var (authorized, warning, _) = sync.Merge(new[] { Event("encrypted private list authorizes userAuthorized") });
        Assert.Contains(Hex("userAuthorized"), authorized);
        Assert.DoesNotContain(Hex("userUnauthorized"), authorized);
        Assert.False(warning);
    }

    [Fact]
    public void falls_back_to_plaintext_p_tags_and_flags_public_list_warning()
    {
        var sync = Sync();
        var (authorized, warning, _) = sync.Merge(new[] { Event("plaintext fallback list authorizes userAuthorized, warns about public list") });
        Assert.Contains(Hex("userAuthorized"), authorized);
        Assert.True(warning);
    }

    [Fact]
    public void merges_kind_30078_allowlist_with_d_tag()
    {
        var sync = Sync();
        var (authorized, _, _) = sync.Merge(new[] { Event("kind 30078 d=jellyfin-allowlist authorizes userAuthorized") });
        Assert.Contains(Hex("userAuthorized"), authorized);
    }

    [Fact]
    public void newest_created_at_wins_when_multiple_versions_seen()
    {
        // Relay delivered an older event (granting userUnauthorized) and the newer
        // one (granting userAuthorized): only the newest may take effect.
        var sync = Sync();
        var candidates = AllowlistCase("older replaceable event is ignored when newer exists")
            .GetProperty("events").EnumerateArray()
            .Select(ToEvent)
            .ToList();
        var (authorized, _, _) = sync.Merge(candidates);
        Assert.Contains(Hex("userAuthorized"), authorized);
        Assert.DoesNotContain(Hex("userUnauthorized"), authorized);
    }

    [Fact]
    public void rejects_event_signed_by_wrong_key()
    {
        var sync = Sync();
        var (authorized, _, _) = sync.Merge(new[] { Event("event signed by wrong key is rejected") });
        Assert.Empty(authorized);
    }

    // -- evaluation against cache state --

    private AllowlistSnapshot Snapshot(IReadOnlyCollection<string> authorized)
        => new(authorized, EventCreatedAtUnix: Base, RetrievedAtUnix: Base);

    private AllowlistEvaluator CachingEvaluator(AllowlistSnapshot snapshot)
        => new(() => snapshot, maxAgeSeconds: MaxAge);

    [Fact]
    public void allows_pubkey_present_in_cached_allowlist()
    {
        var sync = Sync();
        var (authorized, _, _) = sync.Merge(new[] { Event("encrypted private list authorizes userAuthorized") });
        var evaluator = CachingEvaluator(new AllowlistSnapshot(authorized, Base, Base));
        Assert.True(evaluator.Check(Hex("userAuthorized"), Base + 10).Allowed);
    }

    [Fact]
    public void rejects_pubkey_absent_from_cached_allowlist()
    {
        var sync = Sync();
        var (authorized, _, _) = sync.Merge(new[] { Event("encrypted private list authorizes userAuthorized") });
        var evaluator = CachingEvaluator(new AllowlistSnapshot(authorized, Base, Base));
        var decision = evaluator.Check(Hex("userUnauthorized"), Base + 10);
        Assert.False(decision.Allowed);
        Assert.Equal(AllowlistDecision.NotInAllowlist, decision.Reason);
    }

    [Fact]
    public void rejects_new_logins_when_cached_allowlist_exceeds_max_age()
    {
        var sync = Sync();
        var (authorized, _, _) = sync.Merge(new[] { Event("encrypted private list authorizes userAuthorized") });
        var evaluator = CachingEvaluator(new AllowlistSnapshot(authorized, Base, Base));
        var decision = evaluator.Check(Hex("userAuthorized"), Base + MaxAge + 60);
        Assert.False(decision.Allowed);
        Assert.Equal(AllowlistDecision.AllowlistStale, decision.Reason);
    }

    [Fact]
    public void keeps_last_known_allowlist_during_relay_outage_within_max_age()
    {
        // Default fail-open: no fresh fetch available, cache still valid -> allow.
        var sync = Sync();
        var (authorized, _, _) = sync.Merge(new[] { Event("encrypted private list authorizes userAuthorized") });
        var evaluator = CachingEvaluator(new AllowlistSnapshot(authorized, Base, Base));
        Assert.True(evaluator.Check(Hex("userAuthorized"), Base + MaxAge - 60).Allowed);
    }

    [Fact]
    public void reports_unavailable_when_no_cached_allowlist_exists()
    {
        var evaluator = new AllowlistEvaluator(() => null, maxAgeSeconds: MaxAge);
        var decision = evaluator.Check(Hex("userAuthorized"), Base + 10);
        Assert.False(decision.Allowed);
        Assert.Equal(AllowlistDecision.AllowlistUnavailable, decision.Reason);
    }

    // -- helpers --

    private const long MaxAge = 24 * 3600;
    private const long Base = 1760000000; // fixture epoch used by generate.mjs

    private static AllowlistSync Sync()
    {
        var listNsec = Fixtures.KeyField(Fixtures.Allowlist, "list", "sk");
        var listPubkeyHex = Fixtures.PubkeyHex(Fixtures.Allowlist, "list");
        return new(listNsec, listPubkeyHex);
    }

    private static NNostrEvent Event(string name)
        => ToEvent(AllowlistCase(name).GetProperty("event"));

    private static JsonElement AllowlistCase(string name)
    {
        foreach (var c in Fixtures.Allowlist.GetProperty("cases").EnumerateArray())
        {
            if (c.GetProperty("name").GetString() == name)
            {
                return c;
            }
        }
        throw new InvalidOperationException($"allowlist case '{name}' not found");
    }

    private static NNostrEvent ToEvent(JsonElement e)
        => JsonSerializer.Deserialize<NNostrEvent>(e.GetRawText(), NostrEventJson.Options)
           ?? throw new InvalidOperationException("fixture event failed to parse");

    private static string Hex(string keyName) => Fixtures.PubkeyHex(Fixtures.Allowlist, keyName);
}
