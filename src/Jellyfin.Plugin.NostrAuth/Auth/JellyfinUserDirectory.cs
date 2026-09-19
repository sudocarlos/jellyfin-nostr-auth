using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Common.Extensions;
using JellyfinUser = Jellyfin.Database.Implementations.Entities.User;

namespace Jellyfin.Plugin.NostrAuth.Auth;

/// <summary>
/// The Jellyfin-backed user directory: maps between Jellyfin's
/// <c>User</c> entities and the plugin's <see cref="UserRecord"/> seam.
/// </summary>
public sealed class JellyfinUserDirectory : IUserDirectory
{
    private readonly IUserManager _userManager;

    /// <summary>Initializes a new instance of the <see cref="JellyfinUserDirectory"/> class.</summary>
    public JellyfinUserDirectory(IUserManager userManager)
    {
        _userManager = userManager;
    }

    /// <inheritdoc />
    public Task<UserRecord?> FindByIdAsync(Guid userId)
        => Task.FromResult(Map(_userManager.GetUserById(userId)));

    /// <inheritdoc />
    public Task<UserRecord?> FindByNameAsync(string name)
        => Task.FromResult(Map(_userManager.GetUserByName(name)));

    /// <inheritdoc />
    public async Task<UserRecord> CreateUserAsync(string name)
        => Map(await _userManager.CreateUserAsync(name))
           ?? throw new InvalidOperationException("The created user is missing.");

    /// <inheritdoc />
    public Task ChangePasswordAsync(Guid userId, string password)
        => _userManager.ChangePassword(userId, password);

    /// <inheritdoc />
    public async Task UpdateAsync(UserRecord user)
    {
        var jellyfinUser = _userManager.GetUserById(user.Id)
            ?? throw new ArgumentException($"User {user.Id} does not exist.");

        jellyfinUser.AuthenticationProviderId = user.AuthenticationProviderId ?? string.Empty;
        await _userManager.UpdateUserAsync(jellyfinUser);
    }

    private static UserRecord? Map(JellyfinUser? user)
        => user is null
            ? null
            : new(
                user.Id,
                user.Username ?? string.Empty,
                null,
                user.AuthenticationProviderId ?? string.Empty);
}
