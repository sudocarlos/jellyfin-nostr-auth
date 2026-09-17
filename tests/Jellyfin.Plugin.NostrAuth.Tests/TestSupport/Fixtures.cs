using System.Text.Json;

namespace Jellyfin.Plugin.NostrAuth.Tests.TestSupport;

/// <summary>Loads generated fixtures from tests/fixtures (copied next to the test output).</summary>
public static class Fixtures
{
    public static JsonElement Load(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, name);
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    public static JsonElement Nip98 => Load("nip98.json");
    public static JsonElement Allowlist => Load("allowlist.json");

    public static string PubkeyHex(JsonElement keys, string name)
    {
        foreach (var k in keys.GetProperty("keys").EnumerateArray())
        {
            if (k.GetProperty("name").GetString() == name)
            {
                return k.GetProperty("pubkeyHex").GetString()!;
            }
        }
        throw new InvalidOperationException($"key '{name}' not in fixtures");
    }
}
