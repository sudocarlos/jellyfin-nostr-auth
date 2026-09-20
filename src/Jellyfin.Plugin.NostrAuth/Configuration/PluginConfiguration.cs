using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.NostrAuth.Configuration;

/// <summary>One npub → Jellyfin user mapping, persisted in plugin configuration.</summary>
public class NostrUserLink
{
    /// <summary>Gets or sets the authorized user's Nostr pubkey (hex).</summary>
    public string PubkeyHex { get; set; } = string.Empty;

    /// <summary>Gets or sets the id of the provisioned Jellyfin user.</summary>
    public Guid UserId { get; set; }
}

/// <summary>
/// Plugin settings (docs/design.md, "Plugin configuration"). The list nsec is
/// the only key material held server-side; it decrypts the private allowlist
/// and is trivially rotatable.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the identity that authors the allowlist (bech32 npub, required).</summary>
    public string ListNpub { get; set; } = string.Empty;

    /// <summary>Gets or sets the allowlist keypair secret (bech32 nsec, required).</summary>
    public string ListNsec { get; set; } = string.Empty;

    /// <summary>Gets or sets the relays used to fetch the list (required).</summary>
    public string[] Relays { get; set; } = [];

    /// <summary>Gets or sets the allowlist event kind: 10000 (NIP-51 private list) or 30078 (NIP-78 app data).</summary>
    public int ListKind { get; set; } = 10000;

    /// <summary>Gets or sets the allowlist re-fetch cadence in minutes.</summary>
    public int PollIntervalMinutes { get; set; } = 10;

    /// <summary>Gets or sets the cache age in hours beyond which new logins are refused.</summary>
    public int MaxListAgeHours { get; set; } = 24;

    /// <summary>Gets or sets a value indicating whether logins are refused when the list cannot be fetched.</summary>
    public bool FailClosed { get; set; }

    /// <summary>
    /// Gets or sets the absolute server URL as viewers reach it (e.g.
    /// https://media.example.com/jellyfin). Used as the NIP-98 u tag binding
    /// instead of the request URL — set it when a reverse proxy or
    /// address-override setup makes the server's request view differ from
    /// what viewers actually use.
    /// </summary>
    public string PublishedServerUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the npub → Jellyfin user mappings resolved by the provisioner.</summary>
    public List<NostrUserLink> Users { get; set; } = [];
}
