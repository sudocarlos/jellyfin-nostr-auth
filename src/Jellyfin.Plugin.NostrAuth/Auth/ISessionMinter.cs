using MediaBrowser.Controller.Authentication;

namespace Jellyfin.Plugin.NostrAuth.Auth;

/// <summary>
/// Everything a Jellyfin session needs beyond the user id. Client metadata
/// comes from the login request body (informational only, never used for
/// authorization).
/// </summary>
/// <param name="App">The client name.</param>
/// <param name="AppVersion">The client version.</param>
/// <param name="DeviceId">The device id.</param>
/// <param name="DeviceName">The device name.</param>
/// <param name="RemoteEndPoint">The normalized client IP for the activity log.</param>
/// <param name="UserName">The resolved Jellyfin username.</param>
public sealed record SessionLoginParameters(
    string App,
    string AppVersion,
    string DeviceId,
    string DeviceName,
    string RemoteEndPoint,
    string UserName);

/// <summary>Mints a Jellyfin session for a resolved user (SSO-plugin pattern).</summary>
public interface ISessionMinter
{
    /// <summary>
    /// Authenticates the resolved user directly — without enforcing password —
    /// and returns the same AuthenticationResult shape the built-in login
    /// returns.
    /// </summary>
    Task<AuthenticationResult> MintAsync(SessionLoginParameters parameters, Guid userId);
}
