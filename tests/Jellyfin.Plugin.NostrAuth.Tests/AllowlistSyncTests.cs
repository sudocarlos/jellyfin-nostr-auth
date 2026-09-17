using System.Text.Json;
using Jellyfin.Plugin.NostrAuth.Tests.Contracts;
using Jellyfin.Plugin.NostrAuth.Tests.TestSupport;
using Xunit;

namespace Jellyfin.Plugin.NostrAuth.Tests;

/// <summary>
/// Allowlist sync and staleness behavior (docs/design.md, "Allowlist sync" and
/// "Replaceable-event selection and staleness"). Cases come verbatim from
/// fixtures/allowlist.json. All tests are Skip-stubs until the implementation exists.
/// </summary>
public class AllowlistSyncTests
{
    private static IAllowlistSync Sync => throw new NotImplementedException("wire IAllowlistSync implementation");
    private static IAllowlistEvaluator Evaluator => throw new NotImplementedException("wire IAllowlistEvaluator implementation");

    [Fact(Skip = "pending implementation")]
    public void merges_encrypted_private_list_and_authorizes_allowed_npub()
    {
        var (authorized, _) = Sync.Merge(new[] { NostrEvent.FromJson(Event("encrypted private list authorizes userAuthorized")) });
        Assert.Contains(Hex("userAuthorized"), authorized);
    }

    [Fact(Skip = "pending implementation")]
    public void falls_back_to_plaintext_p_tags_and_flags_public_list_warning()
    {
        var (authorized, warning) = Sync.Merge(new[] { NostrEvent.FromJson(Event("plaintext fallback list authorizes userAuthorized, warns about public list")) });
        Assert.Contains(Hex("userAuthorized"), authorized);
        Assert.True(warning);
    }

    [Fact(Skip = "pending implementation")]
    public void merges_kind_30078_allowlist_with_d_tag()
    {
        var (authorized, _) = Sync.Merge(new[] { NostrEvent.FromJson(Event("kind 30078 d=jellyfin-allowlist authorizes userAuthorized")) });
        Assert.Contains(Hex("userAuthorized"), authorized);
    }

    [Fact(Skip = "pending implementation")]
    public void newest_created_at_wins_when_multiple_versions_seen()
    {
        var older = Event("older replaceable event is ignored when newer exists", withEvents: true);
        var candidates = older.GetProperty("events").EnumerateArray().Select(NostrEvent.FromJson).ToList();
        var (authorized, _) = Sync.Merge(candidates);
        Assert.Contains(Hex("userAuthorized"), authorized);
        Assert.DoesNotContain(Hex("userUnauthorized"), authorized);
    }

    [Fact(Skip = "pending implementation")]
    public void rejects_event_signed_by_wrong_key()
    {
        var (authorized, _) = Sync.Merge(new[] { NostrEvent.FromJson(Event("event signed by wrong key is rejected")) });
        Assert.Empty(authorized);
    }

    // -- evaluation against cache state --

    [Fact(Skip = "pending implementation")]
    public void allows_pubkey_present_in_cached_allowlist()
    {
        var decision = Evaluator.Check(Hex("userAuthorized"), Base + 10);
        Assert.True(decision.Allowed);
    }

    [Fact(Skip = "pending implementation")]
    public void rejects_pubkey_absent_from_cached_allowlist()
    {
        var decision = Evaluator.Check(Hex("userUnauthorized"), Base + 10);
        Assert.False(decision.Allowed);
        Assert.Equal(AllowlistDecision.NotInAllowlist, decision.Reason);
    }

    [Fact(Skip = "pending implementation")]
    public void rejects_new_logins_when_cached_allowlist_exceeds_max_age()
    {
        var decision = Evaluator.Check(Hex("userAuthorized"), Base + MaxAge + 60);
        Assert.False(decision.Allowed);
        Assert.Equal(AllowlistDecision.AllowlistStale, decision.Reason);
    }

    [Fact(Skip = "pending implementation")]
    public void keeps_last_known_allowlist_during_relay_outage_within_max_age()
    {
        // Default fail-open: no fresh fetch available, cache still valid -> allow.
        var decision = Evaluator.Check(Hex("userAuthorized"), Base + MaxAge - 60);
        Assert.True(decision.Allowed);
    }

    // -- helpers --

    private const long MaxAge = 24 * 3600;
    private const long Base = 1760000000; // fixture epoch used by generate.mjs

    private static JsonElement Event(string name, bool withEvents = false)
    {
        var cases = Fixtures.Allowlist.GetProperty("cases");
        foreach (var c in cases.EnumerateArray())
        {
            if (c.GetProperty("name").GetString() == name)
            {
                return withEvents ? c : c.GetProperty("event");
            }
        }
        throw new InvalidOperationException($"allowlist case '{name}' not found");
    }

    private static string Hex(string keyName) => Fixtures.PubkeyHex(Fixtures.Allowlist, keyName);
}
