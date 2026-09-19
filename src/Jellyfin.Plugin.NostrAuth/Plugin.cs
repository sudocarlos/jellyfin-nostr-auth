using Jellyfin.Plugin.NostrAuth.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.NostrAuth;

/// <summary>
/// The Nostr Auth plugin (docs/design.md): replaces username/password login
/// with NIP-98 Nostr authentication for the web client, authorized by the
/// server owner's private Nostr allowlist.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>
{
    /// <summary>Initializes a new instance of the <see cref="Plugin"/> class.</summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Nostr Auth";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("a8514093-e868-42c5-8428-22543919e92a");

    /// <summary>Gets the current plugin instance.</summary>
    public static Plugin? Instance { get; private set; }
}
