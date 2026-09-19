namespace Jellyfin.Plugin.NostrAuth.Auth;

/// <summary>
/// The user a Nostr login resolved to. Mirrors the fields the login flow needs
/// from Jellyfin's <c>User</c>; the Jellyfin-backed directory maps in both
/// directions.
/// </summary>
/// <param name="Id">The Jellyfin user id.</param>
/// <param name="Name">The Jellyfin username (the npub, for provisioned users).</param>
/// <param name="UnusablePassword">
/// The random password generated at creation — unknowable by design, so
/// password authentication fails. Null for reused users; the stored hash is
/// never read back.
/// </param>
/// <param name="AuthenticationProviderId">The provider id assigned to the user, or null when untouched.</param>
public sealed record UserRecord(
    Guid Id,
    string Name,
    string? UnusablePassword,
    string? AuthenticationProviderId);

/// <summary>Storage seam over Jellyfin's user database.</summary>
public interface IUserDirectory
{
    /// <summary>Looks up a user by id, or null when it does not exist.</summary>
    Task<UserRecord?> FindByIdAsync(Guid userId);

    /// <summary>Looks up a user by name, or null when absent.</summary>
    Task<UserRecord?> FindByNameAsync(string name);

    /// <summary>Creates a new user with the given name.</summary>
    Task<UserRecord> CreateUserAsync(string name);

    /// <summary>Sets the user's password to the given (intended-unusable) secret.</summary>
    Task ChangePasswordAsync(Guid userId, string password);

    /// <summary>Persists user fields this plugin owns (e.g. AuthenticationProviderId).</summary>
    Task UpdateAsync(UserRecord user);
}

/// <summary>The npub → Jellyfin user mapping, stored in plugin configuration.</summary>
public interface INostrUserLinks
{
    /// <summary>Gets the user id linked to an authorized pubkey, or null when never seen.</summary>
    Guid? GetLinkedUser(string userPubkeyHex);

    /// <summary>Persists the npub → user link for later logins.</summary>
    void LinkUser(string userPubkeyHex, Guid userId);
}

/// <summary>Maps an authorized npub to a Jellyfin user, creating it on first login.</summary>
public interface IUserProvisioner
{
    /// <summary>
    /// Resolves the Jellyfin user for an authorized pubkey, creating it if
    /// absent. Created users get an unusable random password and
    /// AuthenticationProviderId = plugin.
    /// </summary>
    Task<(UserRecord User, bool Created)> ResolveOrCreateAsync(string userPubkeyHex);
}
