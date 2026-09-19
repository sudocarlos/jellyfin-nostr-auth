using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;

namespace Jellyfin.Plugin.NostrAuth.Auth;

/// <summary>
/// The Jellyfin-backed session minter: builds an
/// <c>AuthenticationRequest</c> from the login's client metadata and hands it
/// to <see cref="ISessionManager.AuthenticateDirect"/>, which enforces device
/// access and session caps, issues the access token, and logs the session.
/// </summary>
public sealed class JellyfinSessionMinter : ISessionMinter
{
    private readonly ISessionManager _sessionManager;

    /// <summary>Initializes a new instance of the <see cref="JellyfinSessionMinter"/> class.</summary>
    public JellyfinSessionMinter(ISessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    /// <inheritdoc />
    public Task<AuthenticationResult> MintAsync(SessionLoginParameters parameters, Guid userId)
        => _sessionManager.AuthenticateDirect(new AuthenticationRequest
        {
            UserId = userId,
            Username = parameters.UserName,
            App = parameters.App,
            AppVersion = parameters.AppVersion,
            DeviceId = parameters.DeviceId,
            DeviceName = parameters.DeviceName,
            RemoteEndPoint = parameters.RemoteEndPoint
        });
}
