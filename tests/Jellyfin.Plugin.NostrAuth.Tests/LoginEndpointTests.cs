using System.Net;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.NostrAuth.Auth;
using Jellyfin.Plugin.NostrAuth.Sync;
using Jellyfin.Plugin.NostrAuth.Tests.TestSupport;
using Jellyfin.Plugin.NostrAuth.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using MediaBrowser.Controller.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NNostr.Client;
using NNostr.Client.Protocols;
using NostrAuth.Core;
using NNostrEvent = NNostr.Client.NostrEvent;
using NostrEventTag = NNostr.Client.NostrEventTag;
using NostrAuth.Core;
using Xunit;

namespace Jellyfin.Plugin.NostrAuth.Tests;

/// <summary>
/// Login endpoint HTTP contract (docs/design.md, "Login endpoint"), exercised
/// end-to-end through the real NostrAuthController in an in-memory ASP.NET
/// Core host: the real Nip98Verifier and AllowlistEvaluator against the
/// committed fixtures, fakes for the Jellyfin-backed seams.
/// </summary>
public class LoginEndpointTests
{
    private const string LoginUrl = "https://jellyfin.example.com/NostrAuth/Login";
    private const long MaxAgeSeconds = 24 * 3600;
    private const long FixtureEventCreatedAt = 1760000000; // fixture epoch used by generate.mjs

    [Fact]
    public async Task returns_200_authentication_result_for_valid_login()
    {
        var c = Nip98Case("valid event within freshness window is accepted");
        var response = await LoginAsync(c, "{\"deviceId\":\"test-device\"}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(response.Body);
        var root = json.RootElement;
        Assert.True(root.TryGetProperty("user", out _));
        Assert.True(root.TryGetProperty("accessToken", out _));
        Assert.True(root.TryGetProperty("sessionInfo", out _));
    }

    [Fact]
    public async Task returns_401_expired_event_with_reason()
    {
        var c = Nip98Case("created_at older than 60s is rejected");
        var response = await LoginAsync(c, null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal("expired_event", json.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task returns_401_not_in_allowlist_for_valid_signature_from_non_allowed_pubkey()
    {
        var c = Nip98Case("valid event from a non-allowlisted pubkey passes NIP-98 but is rejected by authorization");
        var response = await LoginAsync(c, "{\"deviceId\":\"test-device\"}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal("not_in_allowlist", json.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task probe_reports_the_canonical_login_url()
    {
        await using var app = CreateApp(1760000000);
        await app.StartAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("https://jellyfin.example.com/NostrAuth/LoginEndpoint");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(LoginUrl, json.RootElement.GetProperty("url").GetString());
    }

    [Fact]
    public async Task rejects_u_tag_when_published_server_url_overrides_the_request_view()
    {
        await using var app = CreateApp(1760000000, publishedServerUrl: "https://published.example.com");
        await app.StartAsync();
        var client = app.GetTestClient();

        // The fixture event is bound to the fixture's login URL; with the
        // override the server binds to the published URL instead.
        var c = Nip98Case("valid event within freshness window is accepted");
        using var request = new HttpRequestMessage(HttpMethod.Post, LoginUrl);
        request.Headers.TryAddWithoutValidation("Authorization", c.GetProperty("authorization").GetString());
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("url_mismatch", json.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task accepts_u_tag_bound_to_the_published_server_url()
    {
        await using var app = CreateApp(1760000000, publishedServerUrl: "https://published.example.com/");
        await app.StartAsync();
        var client = app.GetTestClient();

        var c = Nip98Case("valid event within freshness window is accepted");
        var ev = await SignedNip98Event(
            now: c.GetProperty("now").GetInt64(),
            u: "https://published.example.com/NostrAuth/Login",
            body: "{\"deviceId\":\"test-device\"}");
        var header = "Nostr " + Convert.ToBase64String(
            JsonSerializer.SerializeToUtf8Bytes(ev, NostrEventJson.Options));

        using var request = new HttpRequestMessage(HttpMethod.Post, LoginUrl);
        request.Headers.TryAddWithoutValidation("Authorization", header);
        request.Content = new StringContent("{\"deviceId\":\"test-device\"}", Encoding.UTF8, "application/json");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // -- helpers --

    /// <summary>
    /// POSTs the fixture case to the real controller. The request goes out
    /// with the fixture's absolute URL so the NIP-98 u tag binds byte-for-byte;
    /// the in-memory server honors the URI's scheme and host.
    /// </summary>
    private static async Task<HttpResponse> LoginAsync(JsonElement c, string? body)
    {
        var now = c.GetProperty("now").GetInt64();
        await using var app = CreateApp(now);
        await app.StartAsync();
        var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, LoginUrl);
        request.Headers.TryAddWithoutValidation("Authorization", c.GetProperty("authorization").GetString());
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request);
        return new(response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static WebApplication CreateApp(long now, string? publishedServerUrl = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(NostrAuthController).Assembly);

        var snapshot = new AllowlistSnapshot(
            [Fixtures.PubkeyHex(Fixtures.Allowlist, "userAuthorized")],
            FixtureEventCreatedAt,
            FixtureEventCreatedAt);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.FromUnixTimeSeconds(now));
        var stubProvisioner = new StubProvisioner();
        var stubMinter = new StubSessionMinter();

        builder.Services.AddSingleton<INip98Verifier>(new Nip98Verifier());
        builder.Services.AddSingleton<IUserProvisioner>(stubProvisioner);
        builder.Services.AddSingleton<ISessionMinter>(stubMinter);
        builder.Services.AddSingleton<TimeProvider>(timeProvider);
        builder.Services.AddSingleton<Func<string?>>(() => publishedServerUrl);
        builder.Services.AddSingleton<INostrStatusProvider>(new NostrStatusProvider(
            () => null,
            () => snapshot,
            timeProvider));
        builder.Services.AddSingleton<INostrLoginService>(sp => new NostrLoginService(
            sp.GetRequiredService<INip98Verifier>(),
            () => snapshot,
            () => MaxAgeSeconds,
            () => false,
            sp.GetRequiredService<IUserProvisioner>(),
            sp.GetRequiredService<ISessionMinter>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<NostrLoginService>>()));

        var app = builder.Build();
        app.MapControllers();
        return app;
    }

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

    private sealed record HttpResponse(HttpStatusCode StatusCode, string Body);

    private sealed class StubProvisioner : IUserProvisioner
    {
        public Task<(UserRecord User, bool Created)> ResolveOrCreateAsync(string userPubkeyHex)
            => Task.FromResult((new UserRecord(
                Guid.NewGuid(),
                "nostr-user",
                null,
                NostrUserProvisioner.AuthenticationProviderId), true));
    }

    private sealed class StubSessionMinter : ISessionMinter
    {
        public Task<AuthenticationResult> MintAsync(SessionLoginParameters parameters, Guid userId)
            => Task.FromResult(new AuthenticationResult
            {
                User = new MediaBrowser.Model.Dto.UserDto { Id = userId, Name = parameters.UserName },
                SessionInfo = new MediaBrowser.Model.Dto.SessionInfoDto { UserId = userId },
                AccessToken = "test-access-token",
                ServerId = "test-server-id"
            });
    }

    // A NIP-98 kind-27235 event signed in C# with the fixture userAuthorized
    // key — mirrors what the login page produces.
    private static async Task<NNostrEvent> SignedNip98Event(long now, string u, string body)
    {
        var nsec = Fixtures.KeyField(Fixtures.Allowlist, "userAuthorized", "sk");
        var payloadHash = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        var ev = new NNostrEvent
        {
            Kind = 27235,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(now),
            Tags =
            [
                new NostrEventTag { TagIdentifier = "u", Data = [u] },
                new NostrEventTag { TagIdentifier = "method", Data = ["POST"] },
                new NostrEventTag { TagIdentifier = "payload", Data = [payloadHash] }
            ],
            Content = string.Empty
        };
        await ev.ComputeIdAndSignAsync(NIP19.FromNIP19Nsec(nsec));
        return ev;
    }
}
