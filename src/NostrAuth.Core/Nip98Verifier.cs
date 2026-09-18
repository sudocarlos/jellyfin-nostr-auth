using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NNostr.Client;

namespace NostrAuth.Core;

/// <summary>Result of verifying a NIP-98 Authorization header.</summary>
public record Nip98Result(bool Accepted, string? Reason, string? PubkeyHex)
{
    public const string InvalidEvent = "invalid_event";
    public const string ExpiredEvent = "expired_event";
    public const string UrlMismatch = "url_mismatch";
    public const string MethodMismatch = "method_mismatch";

    public static Nip98Result Reject(string reason) => new(false, reason, null);
    public static Nip98Result Accept(string pubkeyHex) => new(true, null, pubkeyHex);
}

/// <summary>Verifies NIP-98 signed HTTP auth events (kind 27235).</summary>
public interface INip98Verifier
{
    /// <param name="authHeader">Raw Authorization header, e.g. "Nostr &lt;base64 event&gt;".</param>
    /// <param name="requestUrl">The request's absolute URL (NIP-98 u tag must equal this exactly).</param>
    /// <param name="httpMethod">HTTP method of the request.</param>
    /// <param name="requestBody">Raw request body as string, or null; hashed only when the event carries a payload tag.</param>
    /// <param name="nowUnixSeconds">Verifier time; the freshness window is relative to this.</param>
    Nip98Result Verify(string? authHeader, string requestUrl, string httpMethod, string? requestBody, long nowUnixSeconds);
}

/// <summary>
/// Verifies NIP-98 events per NIP-98 and the spec (docs/design.md "Login is a
/// NIP-98 event"): kind 27235, created_at within 60 s of verifier time,
/// exact u/method tag equality, optional payload sha256, valid BIP-340
/// signature over a recomputed event id.
/// </summary>
public sealed class Nip98Verifier : INip98Verifier
{
    private const int FreshnessWindowSeconds = 60;

    public Nip98Result Verify(string? authHeader, string requestUrl, string httpMethod, string? requestBody, long nowUnixSeconds)
    {
        if (authHeader is null || !authHeader.StartsWith("Nostr ", StringComparison.Ordinal))
        {
            return Nip98Result.Reject(Nip98Result.InvalidEvent);
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader["Nostr ".Length..]));
            var ev = JsonSerializer.Deserialize<NostrEvent>(json, NostrEventJson.Options);
            if (ev is null || !ev.HasCompleteFields())
            {
                return Nip98Result.Reject(Nip98Result.InvalidEvent);
            }

            if (ev.Kind != 27235)
            {
                return Nip98Result.Reject(Nip98Result.InvalidEvent);
            }

            var createdAt = ev.CreatedAt?.ToUnixTimeSeconds() ?? 0;
            if (Math.Abs(nowUnixSeconds - createdAt) > FreshnessWindowSeconds)
            {
                return Nip98Result.Reject(Nip98Result.ExpiredEvent);
            }

            if (ev.TagValue("u") != requestUrl)
            {
                return Nip98Result.Reject(Nip98Result.UrlMismatch);
            }

            if (ev.TagValue("method") != httpMethod)
            {
                return Nip98Result.Reject(Nip98Result.MethodMismatch);
            }

            var payloadHash = ev.TagValue("payload");
            if (payloadHash is not null)
            {
                var expected = requestBody is null
                    ? ""
                    : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(requestBody)));
                if (!string.Equals(payloadHash, expected, StringComparison.OrdinalIgnoreCase))
                {
                    return Nip98Result.Reject(Nip98Result.InvalidEvent);
                }
            }

            if (!ev.Verify())
            {
                return Nip98Result.Reject(Nip98Result.InvalidEvent);
            }

            return Nip98Result.Accept(ev.PublicKey.ToLowerInvariant());
        }
        catch (Exception)
        {
            return Nip98Result.Reject(Nip98Result.InvalidEvent);
        }
    }
}
