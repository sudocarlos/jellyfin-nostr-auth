using Jellyfin.Plugin.NostrAuth.Tests.TestSupport;
using NostrAuth.Core;
using Xunit;

namespace Jellyfin.Plugin.NostrAuth.Tests;

/// <summary>
/// List keypair generation and bech32 encoding (docs/design.md config-page
/// keypair generation): a fresh keypair roundtrips and the fixture keys encode
/// to the committed npubs.
/// </summary>
public class NostrKeysTests
{
    [Fact]
    public void generates_valid_bech32_keypair()
    {
        var keypair = NostrKeys.Generate();
        Assert.StartsWith("npub1", keypair.Npub);
        Assert.StartsWith("nsec1", keypair.Nsec);
        Assert.Equal(64, keypair.PubkeyHex.Length);
        Assert.All(keypair.PubkeyHex, c => Assert.True(Uri.IsHexDigit(c)));
    }

    [Fact]
    public void generates_a_fresh_keypair_each_call()
    {
        var first = NostrKeys.Generate();
        var second = NostrKeys.Generate();
        Assert.NotEqual(first.PubkeyHex, second.PubkeyHex);
        Assert.NotEqual(first.Nsec, second.Nsec);
    }

    [Fact]
    public void npub_encoding_roundtrips_pubkey_hex()
    {
        var keypair = NostrKeys.Generate();
        Assert.Equal(keypair.Npub, NostrKeys.ToNip19Npub(keypair.PubkeyHex));
    }

    [Fact]
    public void encodes_fixture_keys_to_the_committed_npubs()
    {
        Assert.Equal(Npub("list"), NostrKeys.ToNip19Npub(Hex("list")));
        Assert.Equal(Npub("userAuthorized"), NostrKeys.ToNip19Npub(Hex("userAuthorized")));
    }

    // -- helpers --

    private static string Hex(string keyName) => Fixtures.PubkeyHex(Fixtures.Allowlist, keyName);

    private static string Npub(string keyName) => Fixtures.KeyField(Fixtures.Allowlist, keyName, "npub");
}
