using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.Domain.Models;
using Devpro.TerraformBackend.Domain.Repositories;
using Devpro.TerraformBackend.WebApi.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.UnitTests.Authentication;

/// <summary>
/// The lockout and the credential cache, against in-memory repositories,
/// so that the order of the count and the verify can be observed directly rather than inferred from response times.
/// </summary>
[Trait("Category", "UnitTests")]
public class ThrottledCredentialAuthenticatorTest
{
    private const int MaxFailedAttempts = 3;

    private static readonly IPAddress Address = IPAddress.Parse("203.0.113.9");

    [Fact]
    public async Task AuthenticateAsync_WithParallelWrongGuesses_VerifiesNoMoreThanTheAllowance()
    {
        // Arrange: every verify waits until all the guesses have started, which is the burst a count taken after the verify cannot stop
        var users = new SlowUserRepository(new UserModel { Username = "alice", PasswordHash = "x", Tenant = "t1" }, "right");
        var authenticator = CreateAuthenticator(users, new InMemoryLockoutRepository(users));

        // Act
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(index => authenticator.AuthenticateAsync("alice", $"wrong-{index}", Address)));

        // Assert
        users.VerifyCount.Should().Be(MaxFailedAttempts);
        outcomes.Count(x => x.Status == AuthenticationStatus.InvalidCredentials).Should().Be(MaxFailedAttempts);
        outcomes.Count(x => x.Status == AuthenticationStatus.LockedOut).Should().Be(20 - MaxFailedAttempts);
    }

    [Fact]
    public async Task AuthenticateAsync_AfterSuccess_ClearsTheCounter()
    {
        // Arrange
        var users = new SlowUserRepository(new UserModel { Username = "alice", PasswordHash = "x", Tenant = "t1" }, "right", TimeSpan.Zero);
        var lockouts = new InMemoryLockoutRepository(users);
        var authenticator = CreateAuthenticator(users, lockouts);
        await authenticator.AuthenticateAsync("alice", "wrong", Address);

        // Act
        var outcome = await authenticator.AuthenticateAsync("alice", "right", Address);

        // Assert: the success counted itself as an attempt before the verify, so a counter left behind would hold that reservation
        outcome.Status.Should().Be(AuthenticationStatus.Success);
        (await lockouts.GetFailureCountAsync("alice", Address.ToString())).Should().Be(0);
    }

    [Fact]
    public async Task AuthenticateAsync_WithAShiftedSeparator_DoesNotHitTheCacheOfAnotherCredential()
    {
        // Arrange: "carol" with "x y" is verified and cached
        var users = new SlowUserRepository(new UserModel { Username = "carol", PasswordHash = "x", Tenant = "t3" }, "x y", TimeSpan.Zero);
        var authenticator = CreateAuthenticator(users, new InMemoryLockoutRepository(users));
        (await authenticator.AuthenticateAsync("carol", "x y", Address)).Status.Should().Be(AuthenticationStatus.Success);

        // Act: the same characters split differently between the two fields
        var outcome = await authenticator.AuthenticateAsync("carol x", "y", Address);

        // Assert
        outcome.Status.Should().Be(AuthenticationStatus.InvalidCredentials);
    }

    private static ThrottledCredentialAuthenticator CreateAuthenticator(IUserRepository users, ILockoutRepository lockouts)
    {
        var configuration = new ApplicationConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:MaxFailedAttempts"] = MaxFailedAttempts.ToString(),
                ["Authentication:LockoutSeconds"] = "300"
            })
            .Build());
        return new ThrottledCredentialAuthenticator(users, lockouts,
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }), configuration,
            NullLogger<ThrottledCredentialAuthenticator>.Instance);
    }

    [Fact]
    public async Task AuthenticateAsync_WithACachedCredentialAfterATenantChange_ReturnsTheNewTenantWithoutAVerify()
    {
        // Arrange: "alice" is verified and cached in t1, then her tenant is renamed in the database
        var users = new SlowUserRepository(new UserModel { Username = "alice", PasswordHash = "x", Tenant = "t1" }, "right", TimeSpan.Zero);
        var authenticator = CreateAuthenticator(users, new InMemoryLockoutRepository(users));
        await authenticator.AuthenticateAsync("alice", "right", Address);
        users.Stored = new UserModel { Username = "alice", PasswordHash = "x", Tenant = "t1-renamed" };

        // Act
        var outcome = await authenticator.AuthenticateAsync("alice", "right", Address);

        // Assert: a tenant kept from the cache would let the old route tenant through the tenant filter
        outcome.Status.Should().Be(AuthenticationStatus.Success);
        outcome.User!.Tenant.Should().Be("t1-renamed");
        users.VerifyCount.Should().Be(1);
    }

    [Fact]
    public async Task AuthenticateAsync_WithACachedCredentialOfADeletedUser_IsRefused()
    {
        // Arrange
        var users = new SlowUserRepository(new UserModel { Username = "alice", PasswordHash = "x", Tenant = "t1" }, "right", TimeSpan.Zero);
        var authenticator = CreateAuthenticator(users, new InMemoryLockoutRepository(users));
        await authenticator.AuthenticateAsync("alice", "right", Address);
        users.Stored = null;

        // Act
        var outcome = await authenticator.AuthenticateAsync("alice", "right", Address);

        // Assert
        outcome.Status.Should().Be(AuthenticationStatus.InvalidCredentials);
    }

    [Fact]
    public async Task AuthenticateAsync_WithACachedCredentialAfterAPasswordChange_VerifiesAgain()
    {
        // Arrange: the old password is cached, then a new hash is stored, which the old password does not match
        var users = new SlowUserRepository(new UserModel { Username = "alice", PasswordHash = "x", Tenant = "t1" }, "right", TimeSpan.Zero);
        var authenticator = CreateAuthenticator(users, new InMemoryLockoutRepository(users));
        await authenticator.AuthenticateAsync("alice", "right", Address);
        users.Stored = new UserModel { Username = "alice", PasswordHash = "y", Tenant = "t1" };
        users.Password = "new";

        // Act
        var outcome = await authenticator.AuthenticateAsync("alice", "right", Address);

        // Assert
        outcome.Status.Should().Be(AuthenticationStatus.InvalidCredentials);
        users.VerifyCount.Should().Be(2);
    }

    [Fact]
    public async Task AuthenticateAsync_WithACachedCredential_MakesOneDatabaseRequestAndNoVerify()
    {
        // Arrange
        var users = new SlowUserRepository(new UserModel { Username = "alice", PasswordHash = "x", Tenant = "t1" }, "right", TimeSpan.Zero);
        var lockouts = new InMemoryLockoutRepository(users);
        var authenticator = CreateAuthenticator(users, lockouts);
        await authenticator.AuthenticateAsync("alice", "right", Address);
        var requestsBefore = lockouts.Requests;

        // Act
        var outcome = await authenticator.AuthenticateAsync("alice", "right", Address);

        // Assert: every Terraform request after the first takes this path, so a second request here doubles the database load of authentication
        outcome.Status.Should().Be(AuthenticationStatus.Success);
        (lockouts.Requests - requestsBefore).Should().Be(1);
        users.VerifyCount.Should().Be(1);
    }

    [Fact]
    public async Task AuthenticateAsync_WithACachedCredentialWhileLockedOut_IsRefused()
    {
        // Arrange: the right password is cached, then the same address reaches the allowance with wrong guesses
        var users = new SlowUserRepository(new UserModel { Username = "alice", PasswordHash = "x", Tenant = "t1" }, "right", TimeSpan.Zero);
        var authenticator = CreateAuthenticator(users, new InMemoryLockoutRepository(users));
        await authenticator.AuthenticateAsync("alice", "right", Address);
        for (var index = 0; index < MaxFailedAttempts; index++)
        {
            await authenticator.AuthenticateAsync("alice", $"wrong-{index}", Address);
        }

        // Act
        var outcome = await authenticator.AuthenticateAsync("alice", "right", Address);

        // Assert: a hit accepted before the lockout check would let a locked-out caller test guesses against the cache without limit
        outcome.Status.Should().Be(AuthenticationStatus.LockedOut);
    }

    /// <summary>
    /// Accepts one username and password, and holds every verify for a while, standing in for the cost of BCrypt.
    /// <c>Stored</c> and <c>Password</c> change between calls, as an update of the <c>user</c> collection does.
    /// </summary>
    private sealed class SlowUserRepository(UserModel user, string password, TimeSpan? delay = null) : IUserRepository
    {
        private int _verifyCount;

        public int VerifyCount => _verifyCount;

        public UserModel? Stored { get; set; } = user;

        public string Password { get; set; } = password;

        public async Task<UserModel?> CheckAuthentication(string username, string candidate)
        {
            Interlocked.Increment(ref _verifyCount);
            await Task.Delay(delay ?? TimeSpan.FromMilliseconds(200));
            return Stored is not null && username == Stored.Username && candidate == Password ? Stored : null;
        }
    }

    /// <summary>
    /// Keeps the counters of <c>auth_lockout</c> in memory, incrementing atomically as the MongoDB pipeline update does,
    /// and reads the user from <paramref name="users"/>, as the aggregation joins the <c>user</c> collection.
    /// <c>Requests</c> counts the calls, each one a database request in the MongoDB implementation.
    /// </summary>
    private sealed class InMemoryLockoutRepository(SlowUserRepository users) : ILockoutRepository
    {
        private readonly Dictionary<string, int> _failures = [];

        private int _requests;

        public int Requests => _requests;

        public Task<int> GetFailureCountAsync(string username, string remoteAddress)
        {
            Interlocked.Increment(ref _requests);
            lock (_failures)
            {
                return Task.FromResult(_failures.GetValueOrDefault(Key(username, remoteAddress)));
            }
        }

        public Task<(int Failures, UserModel? User)> GetFailureCountAndUserAsync(string username, string remoteAddress)
        {
            Interlocked.Increment(ref _requests);
            lock (_failures)
            {
                var user = users.Stored is not null && username == users.Stored.Username ? users.Stored : null;
                return Task.FromResult((_failures.GetValueOrDefault(Key(username, remoteAddress)), user));
            }
        }

        public Task<int> RecordFailureAsync(string username, string remoteAddress, TimeSpan lockoutDuration)
        {
            Interlocked.Increment(ref _requests);
            lock (_failures)
            {
                var key = Key(username, remoteAddress);
                _failures[key] = _failures.GetValueOrDefault(key) + 1;
                return Task.FromResult(_failures[key]);
            }
        }

        public Task ClearAsync(string username, string remoteAddress)
        {
            Interlocked.Increment(ref _requests);
            lock (_failures)
            {
                _failures.Remove(Key(username, remoteAddress));
                return Task.CompletedTask;
            }
        }

        private static string Key(string username, string remoteAddress) => $"{username}|{remoteAddress}";
    }
}
