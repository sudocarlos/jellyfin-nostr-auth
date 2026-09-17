using System.Text.Json;
using Jellyfin.Plugin.NostrAuth.Tests.Contracts;
using Jellyfin.Plugin.NostrAuth.Tests.TestSupport;
using Xunit;

namespace Jellyfin.Plugin.NostrAuth.Tests;

/// <summary>
/// NIP-98 verification behavior (docs/design.md, "Login is a NIP-98 event").
/// Cases and expected outcomes come verbatim from fixtures/nip98.json.
/// All tests are Skip-stubs until the implementation exists.
/// </summary>
public class Nip98VerificationTests
{
    private const string LoginUrl = "https://jellyfin.example.com/NostrAuth/Login";

    private static INip98Verifier Verifier => throw new NotImplementedException("wire INip98Verifier implementation");

    [Fact(Skip = "pending implementation")]
    public void accepts_valid_event_within_freshness_window()
    {
        var c = Case("valid event within freshness window is accepted");
        var result = Verifier.Verify(c.Header(), LoginUrl, "POST", Body(c), c.Now());
        Assert.True(result.Accepted);
        Assert.Equal(Hex("userAuthorized"), result.PubkeyHex);
    }

    [Fact(Skip = "pending implementation")]
    public void rejects_created_at_older_than_60_seconds()
    {
        var c = Case("created_at older than 60s is rejected");
        var result = Verifier.Verify(c.Header(), LoginUrl, "POST", Body(c), c.Now());
        Assert.False(result.Accepted);
        Assert.Equal(Nip98Result.ExpiredEvent, result.Reason);
    }

    [Fact(Skip = "pending implementation")]
    public void rejects_when_u_tag_does_not_match_request_url()
    {
        var c = Case("u tag not matching request URL is rejected");
        var result = Verifier.Verify(c.Header(), LoginUrl, "POST", Body(c), c.Now());
        Assert.False(result.Accepted);
        Assert.Equal(Nip98Result.UrlMismatch, result.Reason);
    }

    [Fact(Skip = "pending implementation")]
    public void rejects_when_method_tag_does_not_match_http_method()
    {
        var c = Case("method tag not matching HTTP method is rejected");
        var result = Verifier.Verify(c.Header(), LoginUrl, "POST", Body(c), c.Now());
        Assert.False(result.Accepted);
        Assert.Equal(Nip98Result.MethodMismatch, result.Reason);
    }

    [Fact(Skip = "pending implementation")]
    public void rejects_non_27235_kind()
    {
        var c = Case("non-27235 kind is rejected");
        var result = Verifier.Verify(c.Header(), LoginUrl, "POST", Body(c), c.Now());
        Assert.False(result.Accepted);
        Assert.Equal(Nip98Result.InvalidEvent, result.Reason);
    }

    [Fact(Skip = "pending implementation")]
    public void rejects_tampered_signature()
    {
        var c = Case("tampered signature is rejected");
        var result = Verifier.Verify(c.Header(), LoginUrl, "POST", Body(c), c.Now());
        Assert.False(result.Accepted);
        Assert.Equal(Nip98Result.InvalidEvent, result.Reason);
    }

    [Fact(Skip = "pending implementation")]
    public void accepts_payload_hash_matching_request_body()
    {
        var c = Case("payload hash matching request body is accepted");
        var result = Verifier.Verify(c.Header(), LoginUrl, "POST", Body(c), c.Now());
        Assert.True(result.Accepted);
    }

    [Fact(Skip = "pending implementation")]
    public void rejects_payload_hash_not_matching_request_body()
    {
        var c = Case("payload hash not matching request body is rejected");
        var result = Verifier.Verify(c.Header(), LoginUrl, "POST", "{\"deviceId\":\"different\"}", c.Now());
        Assert.False(result.Accepted);
        Assert.Equal(Nip98Result.InvalidEvent, result.Reason);
    }

    [Fact(Skip = "pending implementation")]
    public void valid_event_from_non_allowlisted_pubkey_still_passes_nip98_verification()
    {
        // NIP-98 verification is method-only; allowlist authorization is a separate layer.
        var c = Case("valid event from a non-allowlisted pubkey passes NIP-98 but is rejected by authorization");
        var result = Verifier.Verify(c.Header(), LoginUrl, "POST", Body(c), c.Now());
        Assert.True(result.Accepted);
        Assert.Equal(Hex("userUnauthorized"), result.PubkeyHex);
    }

    // -- fixture helpers --

    private static string Hex(string keyName) => Fixtures.PubkeyHex(Fixtures.Nip98, keyName);

    private static JsonElement Case(string name)
    {
        foreach (var c in Fixtures.Nip98.GetProperty("cases").EnumerateArray())
        {
            if (c.GetProperty("name").GetString() == name)
            {
                return c;
            }
        }
        throw new InvalidOperationException($"nip98 case '{name}' not found");
    }

    private static string? Body(JsonElement c)
        => c.TryGetProperty("requestBody", out var rb) ? rb.GetString() : null;
}

public static class FixtureExtensions
{
    public static long Now(this JsonElement c) => c.GetProperty("now").GetInt64();
    public static string Header(this JsonElement c) => c.GetProperty("authorization").GetString()!;
}
