using System.Text.Json;
using NostrAuth.Core;

namespace Jellyfin.Plugin.NostrAuth.Sync;

/// <summary>
/// The dashboard-facing allowlist state (docs/design.md, config preview):
/// decrypted authorized npubs, last fetch time, event age, staleness, and the
/// plaintext-list warning.
/// </summary>
/// <param name="Configured">Whether the required list identity/key/relays are set.</param>
/// <param name="HasSnapshot">Whether any valid list event has been fetched.</param>
/// <param name="Stale">Whether the cached event is past max-age (only meaningful when HasSnapshot).</param>
/// <param name="PublicListWarning">Whether the cached event is plaintext (effectively public list).</param>
/// <param name="LastFetchedAtUnix">When the winning event was fetched, null before the first sync.</param>
/// <param name="EventCreatedAtUnix">The winning event's created_at.</param>
/// <param name="AuthorizedNpubs">The authorized users as bech32 npubs.</param>
/// <param name="Relays">The configured relays.</param>
/// <param name="ListKind">The configured allowlist event kind.</param>
/// <param name="PollIntervalMinutes">The configured re-fetch cadence.</param>
/// <param name="MaxListAgeHours">The configured staleness bound.</param>
/// <param name="FailClosed">The configured availability policy.</param>
public sealed record NostrStatus(
    bool Configured,
    bool HasSnapshot,
    bool Stale,
    bool PublicListWarning,
    long? LastFetchedAtUnix,
    long? EventCreatedAtUnix,
    IReadOnlyList<string> AuthorizedNpubs,
    string[] Relays,
    int ListKind,
    int PollIntervalMinutes,
    int MaxListAgeHours,
    bool FailClosed);

/// <summary>Projects the plugin's allowlist state for the dashboard.</summary>
public interface INostrStatusProvider
{
    /// <summary>Builds the current status.</summary>
    NostrStatus GetStatus();
}

/// <summary>
/// The config-backed status provider: combines plugin configuration, the
/// cached allowlist, and the same max-age policy the login evaluator enforces.
/// </summary>
public sealed class NostrStatusProvider : INostrStatusProvider
{
    private readonly Func<Configuration.PluginConfiguration?> _config;
    private readonly Func<AllowlistSnapshot?> _snapshot;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="NostrStatusProvider"/> class.</summary>
    public NostrStatusProvider(
        Func<Configuration.PluginConfiguration?> config,
        Func<AllowlistSnapshot?> snapshot,
        TimeProvider timeProvider)
    {
        _config = config;
        _snapshot = snapshot;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public NostrStatus GetStatus()
    {
        var config = _config();
        var snapshot = _snapshot();
        var now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var configured = config is not null
            && !string.IsNullOrWhiteSpace(config.ListNpub)
            && !string.IsNullOrWhiteSpace(config.ListNsec)
            && config.Relays.Length > 0;

        var status = new NostrStatus(
            Configured: configured,
            HasSnapshot: snapshot is not null,
            Stale: snapshot is { } s
                && now - s.EventCreatedAtUnix > (config?.MaxListAgeHours ?? 0) * 3600L,
            PublicListWarning: snapshot?.PublicListWarning ?? false,
            LastFetchedAtUnix: snapshot?.RetrievedAtUnix,
            EventCreatedAtUnix: snapshot?.EventCreatedAtUnix,
            AuthorizedNpubs: snapshot is null
                ? []
                : [.. snapshot.AuthorizedPubkeysHex.Select(NostrKeys.ToNip19Npub)],
            Relays: config?.Relays ?? [],
            ListKind: config?.ListKind ?? 10000,
            PollIntervalMinutes: config?.PollIntervalMinutes ?? 10,
            MaxListAgeHours: config?.MaxListAgeHours ?? 24,
            FailClosed: config?.FailClosed ?? false);

        return status;
    }
}
