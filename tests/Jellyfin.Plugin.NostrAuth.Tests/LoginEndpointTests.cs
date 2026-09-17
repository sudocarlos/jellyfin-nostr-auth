using System.Net;
using System.Text.Json;
using Jellyfin.Plugin.NostrAuth.Tests.Contracts;
using Jellyfin.Plugin.NostrAuth.Tests.TestSupport;
using Xunit;

namespace Jellyfin.Plugin.NostrAuth.Tests;

/// <summary>
/// Login endpoint HTTP contract (docs/design.md, "Login endpoint").
/// All tests are Skip-stubs until the implementation exists.
/// </summary>
public class LoginEndpointTests
{
    private const string LoginUrl = "https://jellyfin.example.com/NostrAuth/Login";

    private static Task<HttpResponse> LoginAsync(string authHeader, string? body)
        => throw new NotImplementedException("wire endpoint test client");

    [Fact(Skip = "pending implementation")]
    public async Task returns_200_authentication_result_for_valid_login()
    {
        var c = Nip98Case("valid event within freshness window is accepted");
        var response = await LoginAsync(c.Header(), "{\"deviceId\":\"test-device\"}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(response.Body);
        var root = json.RootElement;
        Assert.True(root.TryGetProperty("user", out _));
        Assert.True(root.TryGetProperty("accessToken", out _));
        Assert.True(root.TryGetProperty("sessionInfo", out _));
    }

    [Fact(Skip = "pending implementation")]
    public async Task returns_401_expired_event_with_reason()
    {
        var c = Nip98Case("created_at older than 60s is rejected");
        var response = await LoginAsync(c.Header(), null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal("expired_event", json.RootElement.GetProperty("reason").GetString());
    }

    [Fact(Skip = "pending implementation")]
    public async Task returns_401_not_in_allowlist_for_valid_signature_from_non_allowed_pubkey()
    {
        var c = Nip98Case("valid event from a non-allowlisted pubkey passes NIP-98 but is rejected by authorization");
        var response = await LoginAsync(c.Header(), "{\"deviceId\":\"test-device\"}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal("not_in_allowlist", json.RootElement.GetProperty("reason").GetString());
    }

    // -- helpers --

    private static JsonElement Nip98Case(string name)
    {
        foreach (var c in Fixtures.Nip98.GetProperty("cases").EnumerateArray())
        {
            if (c.GetProperty("name").GetString() == name)
            {
                return c;
            }
        }
        throw new InvalidOperationException($"nip98 case '{name}' not found");
    }
}
