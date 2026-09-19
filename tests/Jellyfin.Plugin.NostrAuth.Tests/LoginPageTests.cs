using System.Net;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.NostrAuth.Auth;
using Jellyfin.Plugin.NostrAuth.Controllers;
using Jellyfin.Plugin.NostrAuth.Sync;
using Jellyfin.Plugin.NostrAuth.Tests.TestSupport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using MediaBrowser.Controller.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NostrAuth.Core;
using Xunit;

namespace Jellyfin.Plugin.NostrAuth.Tests;

/// <summary>
/// The pre-login pages (docs/design.md, "Login page snippet"): the plugin
/// serves the standalone login page and its vendored nostr-tools bundle
/// anonymously, and both are embedded in the plugin assembly.
/// </summary>
public class LoginPageTests
{
    [Fact]
    public async Task serves_login_page_anonymously_with_module_script()
    {
        var (html, _) = await GetAsync("/NostrAuth/LoginPage");
        Assert.Contains("Sign in with Nostr", html);
        // The page must import the vendored bundle relatively, must NOT use
        // nip98.hashPayload (the documented interop landmine), and must
        // compute the payload hash itself.
        Assert.Contains("from './nostr.mjs'", html);
        Assert.Matches(new Regex(@"sha256", RegexOptions.IgnoreCase), html);
        Assert.DoesNotContain("hashPayload", html);
    }

    [Fact]
    public async Task serves_vendored_nostr_bundle_with_javascript_content_type()
    {
        var (bundle, contentType) = await GetAsync("/NostrAuth/nostr.mjs");
        Assert.StartsWith("text/javascript", contentType);
        // Minified bundle of nostr-tools: the surfaces the page imports.
        Assert.Contains("BunkerSigner", bundle);
        Assert.Contains("createNostrConnectURI", bundle);
        Assert.Contains("parseBunkerInput", bundle);
        Assert.Contains("generateSecretKey", bundle);
    }

    // -- helpers --

    private static async Task<(string Body, string? ContentType)> GetAsync(string path)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(NostrAuthController).Assembly);

        // The page/status endpoints never touch these; the controller needs them
        // constructed, so provide inert stubs.
        builder.Services.AddSingleton<INip98Verifier>(new Nip98Verifier());
        builder.Services.AddSingleton<IUserProvisioner>(new StubProvisioner());
        builder.Services.AddSingleton<ISessionMinter>(new StubSessionMinter());
        builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(DateTimeOffset.UtcNow));
        builder.Services.AddSingleton<INostrStatusProvider>(new NostrStatusProvider(
            () => null,
            () => null,
            new FixedTimeProvider(DateTimeOffset.UtcNow)));
        builder.Services.AddSingleton<INostrLoginService>(sp => new NostrLoginService(
            sp.GetRequiredService<INip98Verifier>(),
            () => null,
            () => 24 * 3600,
            () => false,
            sp.GetRequiredService<IUserProvisioner>(),
            sp.GetRequiredService<ISessionMinter>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<NostrLoginService>>()));
        var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (
            await response.Content.ReadAsStringAsync(),
            response.Content.Headers.ContentType?.ToString() ?? string.Empty);
    }

    private sealed class StubProvisioner : IUserProvisioner
    {
        public Task<(UserRecord User, bool Created)> ResolveOrCreateAsync(string userPubkeyHex)
            => Task.FromResult((new UserRecord(Guid.NewGuid(), "x", null, null), true));
    }

    private sealed class StubSessionMinter : ISessionMinter
    {
        public Task<AuthenticationResult> MintAsync(SessionLoginParameters parameters, Guid userId)
            => Task.FromResult(new AuthenticationResult());
    }
}
