using System.Security.Cryptography;
using NNostr.Client.Protocols;
using NostrAuth.Core;

namespace Jellyfin.Plugin.NostrAuth.Auth;

/// <summary>
/// Provisions Jellyfin users for authorized npubs (docs/design.md,
/// "npub → Jellyfin user, auto-created"): the npub → user mapping is stored in
/// plugin configuration and re-resolved on later logins; created users get an
/// unusable random password and an AuthenticationProviderId that resolves to
/// no password provider, so only this plugin can mint their sessions.
/// </summary>
public sealed class NostrUserProvisioner : IUserProvisioner
{
    /// <summary>
    /// The AuthenticationProviderId assigned to provisioned users. Deliberately
    /// names no IAuthenticationProvider, so password authentication fails
    /// closed for these users even if the random password were somehow known.
    /// </summary>
    public const string AuthenticationProviderId = "Jellyfin.Plugin.NostrAuth";

    private const int PasswordBytes = 32;

    private readonly IUserDirectory _directory;
    private readonly INostrUserLinks _links;

    /// <summary>Initializes a new instance of the <see cref="NostrUserProvisioner"/> class.</summary>
    public NostrUserProvisioner(IUserDirectory directory, INostrUserLinks links)
    {
        _directory = directory;
        _links = links;
    }

    /// <inheritdoc />
    public async Task<(UserRecord User, bool Created)> ResolveOrCreateAsync(string userPubkeyHex)
    {
        if (_links.GetLinkedUser(userPubkeyHex) is { } linkedUserId)
        {
            var linked = await _directory.FindByIdAsync(linkedUserId);
            if (linked is not null)
            {
                return (linked, false);
            }
        }

        var name = Nip19Npub(userPubkeyHex);
        var existing = await _directory.FindByNameAsync(name);
        if (existing is not null)
        {
            // The user already exists under the npub-derived name (e.g. the
            // config mapping was lost or was created outside this plugin).
            _links.LinkUser(userPubkeyHex, existing.Id);
            return (existing, false);
        }

        var created = await _directory.CreateUserAsync(name);
        var password = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(PasswordBytes));
        await _directory.ChangePasswordAsync(created.Id, password);
        created = created with
        {
            UnusablePassword = password,
            AuthenticationProviderId = AuthenticationProviderId
        };
        await _directory.UpdateAsync(created);
        _links.LinkUser(userPubkeyHex, created.Id);
        return (created, true);
    }

    /// <summary>Derives the bech32 npub username from a hex pubkey.</summary>
    private static string Nip19Npub(string userPubkeyHex)
        => NostrKeys.ToNip19Npub(userPubkeyHex);
}
