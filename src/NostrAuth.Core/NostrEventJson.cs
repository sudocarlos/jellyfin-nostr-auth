using System.Text.Json;
using NNostr.Client;

namespace NostrAuth.Core;

/// <summary>Shared JSON options for parsing relay/NIP-98 event JSON into NNostr types.</summary>
public static class NostrEventJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}

public static class NostrEventExtensions
{
    /// <summary>First data value of the first tag with the given identifier, or null.</summary>
    public static string? TagValue(this NostrEvent ev, string identifier)
        => ev.Tags.FirstOrDefault(t => t.TagIdentifier == identifier)?.Data.FirstOrDefault();

    public static bool HasCompleteFields(this NostrEvent ev)
        => !string.IsNullOrEmpty(ev.Id)
           && !string.IsNullOrEmpty(ev.PublicKey)
           && !string.IsNullOrEmpty(ev.Signature)
           && ev.Tags is not null
           && ev.CreatedAt is not null;
}
