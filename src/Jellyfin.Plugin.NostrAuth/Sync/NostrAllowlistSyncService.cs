using System.Globalization;
using System.Text.Json;
using NNostr.Client;
using NNostr.Client.Protocols;
using NostrAuth.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NostrAuth.Sync;

/// <summary>
/// Background allowlist sync (docs/design.md, "allowlist sync"): on each poll
/// interval, requests the configured list events from every configured relay,
/// feeds all candidates through <see cref="AllowlistSync"/>, and updates the
/// cache with the newest signature-valid list. A relay failure or a poll with
/// no valid event leaves the last cache in place (fail-open by default; the
/// evaluator's max-age policy gates staleness).
/// </summary>
public sealed class NostrAllowlistSyncService : BackgroundService
{
    private const string SubscriptionId = "jellyfin-allowlist";
    private static readonly TimeSpan EoseTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PostEoseGrace = TimeSpan.FromSeconds(2);

    private readonly AllowlistCache _cache;
    private readonly ILogger<NostrAllowlistSyncService> _logger;

    /// <summary>Initializes a new instance of the <see cref="NostrAllowlistSyncService"/> class.</summary>
    public NostrAllowlistSyncService(AllowlistCache cache, ILogger<NostrAllowlistSyncService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Allowlist sync failed; the last known list stays in effect until max-age");
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, Plugin.Instance?.Configuration.PollIntervalMinutes ?? 10));
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task PollOnceAsync(CancellationToken stoppingToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null
            || string.IsNullOrWhiteSpace(config.ListNpub)
            || string.IsNullOrWhiteSpace(config.ListNsec)
            || config.Relays.Length == 0)
        {
            _logger.LogInformation("Nostr allowlist is not configured yet; skipping sync");
            return;
        }

        var listNpubHex = Convert.ToHexStringLower(config.ListNpub.FromNIP19Npub().ToBytes());
        var sync = new AllowlistSync(config.ListNsec, listNpubHex);

        // Two filters: kind 10000 by author (replaceable, author+kind addresses
        // it) and kind 30078 narrowed to the plugin's d tag, so other app-data
        // events from the same key never reach the merge.
        var filters = new NostrSubscriptionFilter[]
        {
            new()
            {
                Authors = [listNpubHex],
                Kinds = [AllowlistSync.KindPrivateList]
            },
            new()
            {
                Authors = [listNpubHex],
                Kinds = [AllowlistSync.KindAppData],
                ExtensionData = new Dictionary<string, JsonElement>
                {
                    ["#d"] = JsonSerializer.SerializeToElement(new[] { AllowlistSync.AppDataDTag })
                }
            }
        };

        var candidates = new List<NostrEvent>();
        foreach (var relay in config.Relays)
        {
            try
            {
                candidates.AddRange(await FetchFromRelayAsync(relay, filters, stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fetching the allowlist from {Relay} failed", relay);
            }
        }

        var result = sync.Merge(candidates);
        if (result.EventCreatedAtUnix is not { } createdAt)
        {
            _logger.LogWarning("No valid allowlist event found on any relay; keeping the last known list");
            return;
        }

        _cache.Update(new AllowlistSnapshot(
            result.AuthorizedPubkeysHex,
            createdAt,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            result.PublicListWarning));

        if (result.PublicListWarning)
        {
            _logger.LogWarning(
                "The allowlist event carries plaintext p-tags: the list is effectively public. Republish it as a NIP-51 private list.");
        }

        _logger.LogInformation(
            "Allowlist updated: {Count} authorized pubkeys, event created at {CreatedAt}",
            result.AuthorizedPubkeysHex.Count,
            DateTimeOffset.FromUnixTimeSeconds(createdAt).ToString("o", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Connects to one relay, requests the list events, and stops after the
    /// relay's EOSE plus a short grace window for events that raced it.
    /// </summary>
    private static async Task<IReadOnlyList<NostrEvent>> FetchFromRelayAsync(string relay, NostrSubscriptionFilter[] filters, CancellationToken stoppingToken)
    {
        using var client = new NostrClient(new Uri(relay));
        var received = new List<NostrEvent>();
        client.EventsReceived += (_, payload) =>
        {
            lock (received)
            {
                received.AddRange(payload.events);
            }
        };

        await client.Connect(stoppingToken);
        await client.CreateSubscription(SubscriptionId, filters, stoppingToken);

        var eose = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.EoseReceived += (_, subscriptionId) =>
        {
            if (subscriptionId == SubscriptionId)
            {
                eose.TrySetResult();
            }
        };

        try
        {
            await eose.Task.WaitAsync(EoseTimeout, stoppingToken);
            await Task.Delay(PostEoseGrace, stoppingToken);
        }
        catch (TimeoutException)
        {
            // The relay never signaled EOSE; whatever arrived is still valid input.
        }

        await client.Disconnect();
        lock (received)
        {
            return received.ToArray();
        }
    }
}
