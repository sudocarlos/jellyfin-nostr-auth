using System.Net;
using System.Text.Json;

namespace Jellyfin.Plugin.NostrAuth.Auth;

/// <summary>The request as seen by the endpoint, bound to the NIP-98 verification inputs.</summary>
/// <param name="AuthorizationHeader">Raw Authorization header ("Nostr &lt;base64 event&gt;"), or null.</param>
/// <param name="RequestUrl">The request's absolute URL; the NIP-98 u tag must equal this exactly.</param>
/// <param name="HttpMethod">The HTTP method of the request.</param>
/// <param name="Body">The raw request body, or null; hashed only when the event carries a payload tag.</param>
/// <param name="App">The client name from the body, or a fallback.</param>
/// <param name="AppVersion">The client version from the body, or a fallback.</param>
/// <param name="DeviceId">The device id from the body, or a stable fallback.</param>
/// <param name="DeviceName">The device name (client name fallback).</param>
/// <param name="RemoteEndPoint">The normalized client IP.</param>
public sealed record LoginRequest(
    string? AuthorizationHeader,
    string RequestUrl,
    string HttpMethod,
    string? Body,
    string App,
    string AppVersion,
    string DeviceId,
    string DeviceName,
    string RemoteEndPoint);

/// <summary>HTTP status plus pre-serialized JSON body; the controller returns it verbatim so the response contract is independent of the host's JSON options.</summary>
/// <param name="StatusCode">The HTTP status to respond with.</param>
/// <param name="JsonBody">The JSON body to respond with.</param>
public sealed record LoginOutcome(HttpStatusCode StatusCode, string JsonBody);

/// <summary>Runs the full login pipeline: NIP-98 verification → allowlist policy → user provisioning → session mint.</summary>
public interface INostrLoginService
{
    /// <summary>Verifies the NIP-98 event, checks the allowlist, provisions the user, and mints the session.</summary>
    Task<LoginOutcome> LoginAsync(LoginRequest request);
}
