namespace Jellyfin.Plugin.NostrAuth.Auth;

/// <summary>
/// The config-backed npub → Jellyfin user mapping (docs/design.md: the
/// mapping is stored in plugin configuration and re-resolved on later
/// logins). Configuration mutations are rare (one per first login) and
/// serialized under a lock.
/// </summary>
public sealed class JellyfinNostrUserLinks : INostrUserLinks
{
    private readonly object _configLock = new();

    /// <inheritdoc />
    public Guid? GetLinkedUser(string userPubkeyHex)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return null;
        }

        lock (_configLock)
        {
            return config.Users
                .FirstOrDefault(u => string.Equals(u.PubkeyHex, userPubkeyHex, StringComparison.OrdinalIgnoreCase))
                ?.UserId;
        }
    }

    /// <inheritdoc />
    public void LinkUser(string userPubkeyHex, Guid userId)
    {
        var plugin = Plugin.Instance
            ?? throw new InvalidOperationException("The plugin is not initialized.");

        lock (_configLock)
        {
            var existing = plugin.Configuration.Users
                .FirstOrDefault(u => string.Equals(u.PubkeyHex, userPubkeyHex, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.UserId = userId;
            }
            else
            {
                plugin.Configuration.Users.Add(new Configuration.NostrUserLink
                {
                    PubkeyHex = userPubkeyHex,
                    UserId = userId
                });
            }
        }

        plugin.SaveConfiguration();
    }
}
