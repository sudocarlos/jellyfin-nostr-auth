using Jellyfin.Plugin.NostrAuth.Auth;
using Jellyfin.Plugin.NostrAuth.Tests.TestSupport;
using Xunit;

namespace Jellyfin.Plugin.NostrAuth.Tests;

/// <summary>
/// npub → Jellyfin user provisioning (docs/design.md, "npub → Jellyfin user"),
/// against the real NostrUserProvisioner with an in-memory user directory and
/// link store.
/// </summary>
public class UserMappingTests
{
    private readonly FakeUserDirectory _directory = new();
    private readonly FakeUserLinks _links = new();

    [Fact]
    public void creates_user_on_first_login_for_new_npub()
    {
        var (user, created) = Provision(Hex("userAuthorized"));
        Assert.True(created);
        Assert.NotEqual(default, user.Id);
    }

    [Fact]
    public void reuses_existing_user_on_second_login()
    {
        var (first, _) = Provision(Hex("userAuthorized"));
        var (second, created) = Provision(Hex("userAuthorized"));
        Assert.False(created);
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public void created_user_has_unusable_password()
    {
        var (user, _) = Provision(Hex("userAuthorized"));
        Assert.True(HasUnusablePassword(user));
    }

    [Fact]
    public void created_user_authentication_provider_id_points_to_plugin()
    {
        var (user, _) = Provision(Hex("userAuthorized"));
        Assert.Equal(NostrUserProvisioner.AuthenticationProviderId, user.AuthenticationProviderId);
    }

    // -- helpers --

    private (UserRecord User, bool Created) Provision(string pubkeyHex)
        => Provisioner().ResolveOrCreateAsync(pubkeyHex).GetAwaiter().GetResult();

    private NostrUserProvisioner Provisioner() => new(_directory, _links);

    private static bool HasUnusablePassword(UserRecord user)
        => user.UnusablePassword is { Length: 64 } password && password.All(Uri.IsHexDigit);

    private static string Hex(string keyName) => Fixtures.PubkeyHex(Fixtures.Allowlist, keyName);

    private sealed class FakeUserDirectory : IUserDirectory
    {
        private readonly Dictionary<Guid, UserRecord> _users = [];

        public Task<UserRecord?> FindByIdAsync(Guid userId)
            => Task.FromResult(_users.TryGetValue(userId, out var user) ? user : (UserRecord?)null);

        public Task<UserRecord?> FindByNameAsync(string name)
            => Task.FromResult<UserRecord?>(_users.Values.FirstOrDefault(u => u.Name == name));

        public Task<UserRecord> CreateUserAsync(string name)
        {
            var user = new UserRecord(Guid.NewGuid(), name, null, null);
            _users[user.Id] = user;
            return Task.FromResult(user);
        }

        public Task ChangePasswordAsync(Guid userId, string password)
        {
            if (_users.TryGetValue(userId, out var user))
            {
                _users[userId] = user with { UnusablePassword = password };
            }

            return Task.CompletedTask;
        }

        public Task UpdateAsync(UserRecord user)
        {
            _users[user.Id] = user;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserLinks : INostrUserLinks
    {
        private readonly Dictionary<string, Guid> _links = [];

        public Guid? GetLinkedUser(string userPubkeyHex)
            => _links.TryGetValue(userPubkeyHex, out var userId) ? userId : null;

        public void LinkUser(string userPubkeyHex, Guid userId) => _links[userPubkeyHex] = userId;
    }
}
