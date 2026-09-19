using Jellyfin.Plugin.NostrAuth.Auth;
using Jellyfin.Plugin.NostrAuth.Sync;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using NostrAuth.Core;

namespace Jellyfin.Plugin.NostrAuth.Controllers;

/// <summary>
/// The Nostr login endpoint (docs/design.md, "Login endpoint"). Anonymous: the
/// NIP-98 signature in the Authorization header is the only authenticator, and
/// the endpoint must be reachable from the login screen. Jellyfin applies its
/// default policy only to endpoints marked [Authorize] (no fallback policy), so
/// the anonymous route is the absence of [Authorize]; [AllowAnonymous] states
/// that intent explicitly.
/// </summary>
[ApiController]
[Route("NostrAuth")]
public class NostrAuthController : ControllerBase
{
    /// <summary>Rejects oversized bodies before reading them (fail-closed, 401 invalid_event).</summary>
    private const long MaxBodyBytes = 64 * 1024;

    private readonly INostrLoginService _loginService;
    private readonly INostrStatusProvider _statusProvider;

    /// <summary>Initializes a new instance of the <see cref="NostrAuthController"/> class.</summary>
    /// <param name="loginService">The login pipeline.</param>
    /// <param name="statusProvider">The dashboard-facing allowlist status.</param>
    public NostrAuthController(INostrLoginService loginService, INostrStatusProvider statusProvider)
    {
        _loginService = loginService;
        _statusProvider = statusProvider;
    }

    /// <summary>
    /// Logs in with a NIP-98 signed event. 200 returns an
    /// AuthenticationResult-shaped body; 401 a { reason } body; 503 only when
    /// failClosed is set and the allowlist cannot be fetched.
    /// </summary>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <response code="200">User authenticated.</response>
    /// <response code="401">NIP-98 event or allowlist authorization rejected.</response>
    /// <response code="503">failClosed is on and the allowlist is unavailable.</response>
    /// <returns>The login outcome.</returns>
    [HttpPost("Login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(CancellationToken cancellationToken)
    {
        if ((Request.ContentLength ?? 0) > MaxBodyBytes)
        {
            return Content("{\"reason\":\"invalid_event\"}", "application/json", Encoding.UTF8);
        }

        var body = await ReadBodyAsync(cancellationToken);

        var clientMetadata = ParseClientMetadata(body);
        var loginRequest = new LoginRequest(
            Request.Headers.Authorization.ToString(),
            BuildRequestUrl(),
            Request.Method,
            body,
            clientMetadata.ClientName ?? "Nostr Client",
            clientMetadata.ClientVersion ?? "unknown",
            clientMetadata.DeviceId ?? "nostr",
            clientMetadata.ClientName ?? "Nostr Client",
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        var outcome = await _loginService.LoginAsync(loginRequest);
        return new ContentResult
        {
            Content = outcome.JsonBody,
            ContentType = "application/json",
            StatusCode = (int)outcome.StatusCode
        };
    }

    /// <summary>
    /// The allowlist state for the dashboard preview: decrypted authorized
    /// npubs, last fetch time, event age, staleness, and the plaintext-list
    /// warning.
    /// </summary>
    /// <response code="200">Status returned.</response>
    /// <returns>The allowlist status.</returns>
    [HttpGet("Status")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public IActionResult Status()
        => new ContentResult
        {
            Content = JsonSerializer.Serialize(_statusProvider.GetStatus(), NostrAuthJson.CamelCase),
            ContentType = "application/json"
        };

    /// <summary>
    /// Generates a fresh list keypair. Nothing is persisted — the owner
    /// configures the returned npub/nsec themselves and imports the nsec into
    /// a Nostr client for publishing the list.
    /// </summary>
    /// <response code="200">Keypair returned.</response>
    /// <returns>The fresh list keypair.</returns>
    [HttpPost("GenerateKeypair")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public IActionResult GenerateKeypair()
        => new ContentResult
        {
            Content = JsonSerializer.Serialize(NostrKeys.Generate(), NostrAuthJson.CamelCase),
            ContentType = "application/json"
        };

    /// <summary>
    /// The absolute request URL, exactly as the client saw it — NIP-98 binds
    /// the u tag to this string byte-for-byte, scheme and query included.
    /// </summary>
    private string BuildRequestUrl()
        => $"{Request.Scheme}://{Request.Host}{Request.PathBase}{Request.Path}{Request.QueryString}";

    /// <summary>Reads the raw request body once; null when there is none.</summary>
    private async Task<string?> ReadBodyAsync(CancellationToken cancellationToken)
    {
        if (Request.ContentLength is not > 0)
        {
            return null;
        }

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: false);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <summary>Parses the optional { deviceId, clientName, clientVersion } body; informational only.</summary>
    private static (string? DeviceId, string? ClientName, string? ClientVersion) ParseClientMetadata(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return (null, null, null);
        }

        try
        {
            var json = System.Text.Json.JsonDocument.Parse(body).RootElement;
            return (
                json.TryGetProperty("deviceId", out var deviceId) ? deviceId.GetString() : null,
                json.TryGetProperty("clientName", out var clientName) ? clientName.GetString() : null,
                json.TryGetProperty("clientVersion", out var clientVersion) ? clientVersion.GetString() : null);
        }
        catch (System.Text.Json.JsonException)
        {
            // The body is informational; an unparsable one just yields defaults.
            return (null, null, null);
        }
    }
}
