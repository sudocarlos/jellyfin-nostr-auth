namespace Jellyfin.Plugin.NostrAuth.Tests.TestSupport;

/// <summary>A TimeProvider pinned to a fixed instant, for code whose behavior is relative to "now".</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => now;
}
