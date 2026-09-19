using System.Security.Cryptography;
using NBitcoin.Secp256k1;
using NNostr.Client;
using NNostr.Client.Protocols;

namespace NostrAuth.Core;

/// <summary>A freshly generated list keypair (docs/design.md: the config page can generate one).</summary>
/// <param name="Npub">The bech32 npub to configure as the list identity.</param>
/// <param name="Nsec">The bech32 nsec to import into a Nostr client for publishing the list.</param>
/// <param name="PubkeyHex">The pubkey in hex, as AllowlistSync wants it.</param>
public sealed record NostrKeypair(string Npub, string Nsec, string PubkeyHex);

/// <summary>
/// Bech32 npub/nsec helpers for the allowlist keypair: keypair generation for
/// the config page, and hex → npub conversion for showing authorized users.
/// </summary>
public static class NostrKeys
{
    /// <summary>Generates a fresh secp256k1 keypair.</summary>
    public static NostrKeypair Generate()
    {
        var secret = ECPrivKey.Create(RandomNumberGenerator.GetBytes(32));
        return new(
            secret.CreateXOnlyPubKey().ToNIP19(),
            secret.ToNIP19(),
            Convert.ToHexStringLower(secret.CreateXOnlyPubKey().ToBytes()));
    }

    /// <summary>Encodes a hex pubkey as its bech32 npub.</summary>
    public static string ToNip19Npub(string pubkeyHex)
        => NostrExtensions.ParsePubKey(pubkeyHex).ToNIP19();
}
