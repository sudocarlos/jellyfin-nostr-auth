using NostrAuth.Core;

namespace Jellyfin.Plugin.NostrAuth.Sync;

/// <summary>
/// Thread-safe holder of the last merged allowlist snapshot. The evaluator
/// reads through <see cref="Snapshot"/>; the sync host replaces the reference
/// atomically. Relay outages simply leave the last snapshot in place — the
/// evaluator's max-age policy governs whether it may still be honored.
/// </summary>
public sealed class AllowlistCache
{
    private volatile AllowlistSnapshot? _snapshot;

    /// <summary>Gets the last merged snapshot, or null when nothing has been fetched yet.</summary>
    public AllowlistSnapshot? Snapshot => _snapshot;

    /// <summary>Replaces the cached snapshot.</summary>
    public void Update(AllowlistSnapshot snapshot) => _snapshot = snapshot;
}
