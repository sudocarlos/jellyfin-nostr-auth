// Planned plugin-side contracts, expressed at the minimum surface needed to
// encode behavior expectations. These live in the test project until the
// Jellyfin plugin lands, then become real wiring. The core Nostr contracts
// (NIP-98 verification, allowlist sync/evaluation) are implemented in
// src/NostrAuth.Core and exercised directly.

namespace Jellyfin.Plugin.NostrAuth.Tests.Contracts;

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

/// <summary>Minimal HTTP response for endpoint contract tests.</summary>
public record HttpResponse(System.Net.HttpStatusCode StatusCode, string Body);
