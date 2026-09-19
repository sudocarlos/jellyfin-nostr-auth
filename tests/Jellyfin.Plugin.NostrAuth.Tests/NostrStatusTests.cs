using Jellyfin.Plugin.NostrAuth.Auth;
using Jellyfin.Plugin.NostrAuth.Configuration;
using Jellyfin.Plugin.NostrAuth.Sync;
using Jellyfin.Plugin.NostrAuth.Tests.TestSupport;
using NostrAuth.Core;
using Xunit;

namespace Jellyfin.Plugin.NostrAuth.Tests;

/// <summary>
/// The dashboard allowlist status (docs/design.md config preview): decrypted
/// authorized npubs, last fetch, staleness under the same max-age policy as
/// the login evaluator, and the plaintext-list warning.
/// </summary>
public class NostrStatusTests
{
    private const long MaxAgeSeconds = 24 * 3600;
    private const long Base = 1760000000; // fixture epoch used by generate.mjs

    private static PluginConfiguration Config()
        => new()
        {
            ListNpub = "npub1glaklfzhtdekacg4ef3w9jtkh",
            ListNsec = "nsec1pw0n4rcu9ex4k6n0d3f0",
            Relays = ["wss://relay.example"]
        };

    [Fact]
    public void reports_not_configured_when_required_fields_are_missing()
    {
        var status = Status(new PluginConfiguration(), null, Base);
        Assert.False(status.Configured);
        Assert.False(status.HasSnapshot);
        Assert.False(status.Stale);
        Assert.Empty(status.AuthorizedNpubs);
    }

    [Fact]
    public void reports_fresh_snapshot_with_authorized_npubs()
    {
        var snapshot = new AllowlistSnapshot([Hex("userAuthorized")], Base, Base);
        var status = Status(Config(), snapshot, Base + 60);
        Assert.True(status.Configured);
        Assert.True(status.HasSnapshot);
        Assert.False(status.Stale);
        Assert.False(status.PublicListWarning);
        Assert.Contains(Npub("userAuthorized"), status.AuthorizedNpubs);
    }

    [Fact]
    public void reports_unavailable_before_the_first_sync()
    {
        var status = Status(Config(), null, Base + 60);
        Assert.True(status.Configured);
        Assert.False(status.HasSnapshot);
        Assert.False(status.Stale);
        Assert.Empty(status.AuthorizedNpubs);
    }

    [Fact]
    public void reports_stale_when_cache_is_past_max_age()
    {
        var snapshot = new AllowlistSnapshot([Hex("userAuthorized")], Base, Base);
        var status = Status(Config(), snapshot, Base + MaxAgeSeconds + 60);
        Assert.True(status.HasSnapshot);
        Assert.True(status.Stale);
    }

    [Fact]
    public void carries_the_public_list_warning_from_the_snapshot()
    {
        var snapshot = new AllowlistSnapshot([Hex("userAuthorized")], Base, Base, PublicListWarning: true);
        var status = Status(Config(), snapshot, Base + 60);
        Assert.True(status.PublicListWarning);
    }

    // -- helpers --

    private NostrStatus Status(PluginConfiguration? config, AllowlistSnapshot? snapshot, long now)
        => new NostrStatusProvider(
            () => config,
            () => snapshot,
            new FixedTimeProvider(DateTimeOffset.FromUnixTimeSeconds(now))).GetStatus();

    private static string Hex(string keyName) => Fixtures.PubkeyHex(Fixtures.Allowlist, keyName);

    private static string Npub(string keyName) => Fixtures.KeyField(Fixtures.Allowlist, keyName, "npub");
}
