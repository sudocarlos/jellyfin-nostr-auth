using Jellyfin.Plugin.NostrAuth.Auth;
using Jellyfin.Plugin.NostrAuth.Sync;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NostrAuth.Core;

namespace Jellyfin.Plugin.NostrAuth;

/// <summary>
/// Registers the plugin's services with Jellyfin's DI container: the login
/// pipeline the controller resolves, the Jellyfin-backed seams behind it, and
/// the background allowlist sync host.
/// </summary>
public sealed class ServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton(TimeProvider.System);
        serviceCollection.AddSingleton<AllowlistCache>();
        serviceCollection.AddSingleton<INip98Verifier, Nip98Verifier>();

        serviceCollection.AddSingleton<IUserDirectory, JellyfinUserDirectory>();
        serviceCollection.AddSingleton<INostrUserLinks, JellyfinNostrUserLinks>();
        serviceCollection.AddSingleton<IUserProvisioner, NostrUserProvisioner>();
        serviceCollection.AddSingleton<ISessionMinter, JellyfinSessionMinter>();

        serviceCollection.AddSingleton<INostrLoginService>(sp => new NostrLoginService(
            sp.GetRequiredService<INip98Verifier>(),
            () => sp.GetRequiredService<AllowlistCache>().Snapshot,
            MaxListAgeSeconds,
            FailClosed,
            sp.GetRequiredService<IUserProvisioner>(),
            sp.GetRequiredService<ISessionMinter>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<NostrLoginService>>()));

        serviceCollection.AddHostedService<NostrAllowlistSyncService>();
    }

    private static long MaxListAgeSeconds()
    {
        var hours = Plugin.Instance?.Configuration.MaxListAgeHours;
        return (hours is > 0 ? hours.Value : 24) * 3600L;
    }

    private static bool FailClosed()
        => Plugin.Instance?.Configuration.FailClosed ?? false;
}
