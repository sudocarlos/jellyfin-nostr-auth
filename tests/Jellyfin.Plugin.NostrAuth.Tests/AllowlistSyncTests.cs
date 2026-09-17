using System.Text.Json;
using Jellyfin.Plugin.NostrAuth.Tests.Contracts;
using Jellyfin.Plugin.NostrAuth.Tests.TestSupport;
using Xunit;

namespace Jellyfin.Plugin.NostrAuth.Tests;

/// <summary>
/// Allowlist sync and staleness behavior (docs/design.md, "Allowlist sync" and
/// "Replaceable-event selection"). Cases come verbatim from fixtures/allowlist.json.
/// All tests are Skip-stubs until the implementation exists.
/// </summary>
public class AllowlistSyncTests
{
    private static IAllowlistSync Sync => throw new NotImplementedException("wire IAllowlistSync implementation");
    private static IAllowlistEvaluator Evaluator => throw new NotImplementedException("wire IAllowlistEvaluator implementation");

    [Fact(Skip = "pending implementation")]
    public void merges_encrypted_private_list_and_authorizes_allowed_npub()
    {
        var c = Case("encrypted private list authorizes userAuthorized");
        var (authorized, warning) = Sync.Merge(new[] { NostrEvent.FromJson(c.GetProperty("event")) });
        Assert.Contains(Hex("userAuthorized"), authorized);
        Assert.False(WarningFlag());
    }

    [Fact(Skip = "pending implementation")]
    public void falls_back_to_plaintext_p_tags_and_flags_public_list_warning()
    {
        var c = Case("plaintext fallback list authorizes userAuthorized");
        var (authorized, _) = Sync.Merge(new[] { NostrEvent.FromJson(c.GetProperty("event")) });
        Assert.Contains(Hex("userAuthorized"), authorized);
        Assert.True(PublicListWarning());
    }

    [Fact(Skip = "pending implementation")]
    public void merges_kind_30078_allowlist_with_d_tag()
    {
        var c = Case("kind 30078 d=jellyfin-allowlist authorizes userAuthorized");
        var (authorized, _) = Sync.Merge(new[] { NostrEvent.FromJson(c.GetProperty("event")) });
        Assert.Contains(Hex("userAuthorized"), authorized);
    }

    [Fact(Skip = "pending implementation")]
    public void newest_created_at_wins_when_multiple_versions_seen()
    {
        // Relay delivered an older event (granting userUnauthorized) and the newer
        // one (granting userAuthorized): only the newest may take effect.
        var c = Case("older replaceable event is ignored when newer exists");
        var (authorized, _) = Sync.Merge(c.GetProperty("events").EnumerateArray()
            .Select(NostrEvent.FromJson).ToList());
        Assert.Contains(Hex("userAuthorized"), authorized);
        Assert.DoesNotContain(Hex("userUnauthorized"), authorized);
    }

    [Fact(Skip = "pending implementation")]
    public void rejects_event_signed_by_wrong_key()
    {
        var c = Case("event signed by wrong key is rejected");
        var (authorized, _) = Sync.Merge(new[] { NostrEvent.FromJson(c.GetProperty("event")) });
        Assert.DoesNotContain(Hex("userUnauthorized"), authorized);
    }

    // -- evaluation against cache state --

    [Fact(Skip = "pending implementation")]
    public void allows_pubkey_present_in_cached_allowlist()
    {
        var decision = Evaluator.Check(Hex("userAuthorized"), Now: Base + 10);
        Assert.True(decision.Allowed);
    }

    [Fact(Skip = "pending implementation")]
    public void rejects_pubkey_absent_from_cached_allowlist()
    {
        var decision = Evaluator.Check(Hex("userUnauthorized"), Now: Base + 10);
        Assert.False(decision.Allowed);
        Assert.Equal(AllowlistDecision.NotInAllowlist, decision.Reason);
    }

    [Fact(Skip = "pending implementation")]
    public void rejects_new_logins_when_cached_allowlist_exceeds_max_age()
    {
        var decision = Evaluator.Check(Hex("userAuthorized"), Now: Base + MaxAge + 60);
        Assert.False(decision.Allowed);
        Assert.Equal(AllowlistDecision.AllowlistStale, decision.Reason);
    }

    [Fact(Skip = "pending implementation")]
    public void keeps_last_known_allowlist_during_relay_outage_within_max_age()
    {
        // Default fail-open: no fresh fetch available, cache still valid -> allow.
        var decision = Evaluator.Check(Hex("userAuthorized"), Now: Base + MaxAge - 60);
        Assert.True(decision.Allowed);
    }

    // -- helpers --

    private const long MaxAge = 24 * 3600;

    private static bool PublicListWarning() => throw new NotImplementedException("expose warning from Merge");

    private static string Hex(string keyName) => Fixtures.PubkeyHex(Fixtures.Allowlist, keyName);

    private static bool WarningFlag() => throw new NotImplementedException();
}

public static class AllowlistCaseExtensions
{
    public static long Now(this JsonElement c)
        => c.TryGetProperty("now", out var n) ? n.GetInt64() : 1760000000;
}
