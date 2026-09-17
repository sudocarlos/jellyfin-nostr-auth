using Jellyfin.Plugin.NostrAuth.Tests.Contracts;
using Jellyfin.Plugin.NostrAuth.Tests.TestSupport;
using Xunit;

namespace Jellyfin.Plugin.NostrAuth.Tests;

/// <summary>
/// npub → Jellyfin user provisioning (docs/design.md, "npub → Jellyfin user").
/// All tests are Skip-stubs until the implementation exists.
/// </summary>
public class UserMappingTests
{
    private static IUserProvisioner Provisioner => throw new NotImplementedException("wire IUserProvisioner implementation");

    [Fact(Skip = "pending implementation")]
    public void creates_user_on_first_login_for_new_npub()
    {
        var (user, created) = Provision(Hex("userAuthorized"));
        Assert.True(created);
        Assert.NotEqual(default, user.Id);
    }

    [Fact(Skip = "pending implementation")]
    public void reuses_existing_user_on_second_login()
    {
        var (first, _) = Provision(Hex("userAuthorized"));
        var (second, created) = Provision(Hex("userAuthorized"));
        Assert.False(created);
        Assert.Equal(first.Id, second.Id);
    }

    [Fact(Skip = "pending implementation")]
    public void created_user_has_unusable_password()
    {
        var (user, _) = Provision(Hex("userAuthorized"));
        Assert.True(HasUnusablePassword(user));
    }

    [Fact(Skip = "pending implementation")]
    public void created_user_authentication_provider_id_points_to_plugin()
    {
        var (user, _) = Provision(Hex("userAuthorized"));
        Assert.Equal("Jellyfin.Plugin.NostrAuth", user.AuthenticationProviderId);
    }

    // -- helpers --

    private (UserRecord User, bool Created) Provision(string pubkeyHex)
        => throw new NotImplementedException("wire IUserProvisioner");

    private static bool HasUnusablePassword(UserRecord user)
        => throw new NotImplementedException("assert password cannot authenticate");

    private static string Hex(string keyName) => Fixtures.PubkeyHex(Fixtures.Allowlist, keyName);
}
