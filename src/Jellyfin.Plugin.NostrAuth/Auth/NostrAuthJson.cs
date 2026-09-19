using System.Text.Json;

namespace Jellyfin.Plugin.NostrAuth.Auth;

/// <summary>The serializer for plugin-returned JSON bodies (camelCase, independent of the host's options).</summary>
public static class NostrAuthJson
{
    /// <summary>Gets the camelCase serializer options.</summary>
    public static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
