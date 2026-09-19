using System.Net;
using System.Text.Json;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Net;
using Microsoft.Extensions.Logging;
using NostrAuth.Core;

namespace Jellyfin.Plugin.NostrAuth.Auth;
/// <summary>
/// The login pipeline (docs/design.md, "Login endpoint"): verify the NIP-98
/// event, then the allowlist decision (not_in_allowlist / allowlist_stale /
/// allowlist_unavailable), then provision the user and mint the session.
/// Server-policy rejections from the session manager (device restrictions,
/// session caps) map to 401 auth_rejected. All bodies are pre-serialized
/// camelCase so the response contract is independent of the host's JSON
/// options.
/// </summary>
public sealed class NostrLoginService : INostrLoginService
{
    private static readonly JsonSerializerOptions JsonOptions = NostrAuthJson.CamelCase;

    private readonly INip98Verifier _verifier;
    private readonly Func<AllowlistSnapshot?> _snapshotProvider;
    private readonly Func<long> _maxListAgeSeconds;
    private readonly Func<bool> _failClosed;
    private readonly IUserProvisioner _provisioner;
    private readonly ISessionMinter _sessionMinter;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NostrLoginService> _logger;

    /// <summary>Initializes a new instance of the <see cref="NostrLoginService"/> class.</summary>
    public NostrLoginService(
        INip98Verifier verifier,
        Func<AllowlistSnapshot?> snapshotProvider,
        Func<long> maxListAgeSeconds,
        Func<bool> failClosed,
        IUserProvisioner provisioner,
        ISessionMinter sessionMinter,
        TimeProvider timeProvider,
        ILogger<NostrLoginService> logger)
    {
        _verifier = verifier;
        _snapshotProvider = snapshotProvider;
        _maxListAgeSeconds = maxListAgeSeconds;
        _failClosed = failClosed;
        _provisioner = provisioner;
        _sessionMinter = sessionMinter;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LoginOutcome> LoginAsync(LoginRequest request)
    {
        var now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();

        var verification = _verifier.Verify(
            request.AuthorizationHeader,
            request.RequestUrl,
            request.HttpMethod,
            request.Body,
            now);
        if (!verification.Accepted)
        {
            return Reject(HttpStatusCode.Unauthorized, verification.Reason!);
        }

        var evaluator = new AllowlistEvaluator(_snapshotProvider, _maxListAgeSeconds());
        var decision = evaluator.Check(verification.PubkeyHex!, now);
        if (!decision.Allowed)
        {
            return decision.Reason == AllowlistDecision.AllowlistUnavailable && _failClosed()
                ? Reject(HttpStatusCode.ServiceUnavailable, decision.Reason!)
                : Reject(HttpStatusCode.Unauthorized, decision.Reason!);
        }

        try
        {
            var (user, created) = await _provisioner.ResolveOrCreateAsync(verification.PubkeyHex!);
            _logger.LogInformation(
                "Nostr login for {Npub} resolved {Action} user {UserId} ({UserName})",
                verification.PubkeyHex,
                created ? "created" : "existing",
                user.Id,
                user.Name);

            var result = await _sessionMinter.MintAsync(
                new SessionLoginParameters(
                    request.App,
                    request.AppVersion,
                    request.DeviceId,
                    request.DeviceName,
                    request.RemoteEndPoint,
                    user.Name),
                user.Id);

            return new(HttpStatusCode.OK, JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (SecurityException ex)
        {
            _logger.LogWarning(ex, "Nostr login rejected by server policy");
            return Reject(HttpStatusCode.Unauthorized, "auth_rejected");
        }
        catch (AuthenticationException ex)
        {
            _logger.LogWarning(ex, "Nostr login rejected by authentication");
            return Reject(HttpStatusCode.Unauthorized, "auth_rejected");
        }
    }

    private static LoginOutcome Reject(HttpStatusCode statusCode, string reason)
        => new(statusCode, JsonSerializer.Serialize(new { reason }, JsonOptions));
}
