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
        var authenticator = CreateAuthenticator(users, new InMemoryLockoutRepository());

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
        var lockouts = new InMemoryLockoutRepository();
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
        var authenticator = CreateAuthenticator(users, new InMemoryLockoutRepository());
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

    /// <summary>
    /// Accepts one username and password, and holds every verify for a while, standing in for the cost of BCrypt.
    /// </summary>
    private sealed class SlowUserRepository(UserModel user, string password, TimeSpan? delay = null) : IUserRepository
    {
        private int _verifyCount;

        public int VerifyCount => _verifyCount;

        public async Task<UserModel?> CheckAuthentication(string username, string candidate)
        {
            Interlocked.Increment(ref _verifyCount);
            await Task.Delay(delay ?? TimeSpan.FromMilliseconds(200));
            return username == user.Username && candidate == password ? user : null;
        }
    }

    /// <summary>
    /// Keeps the counters of <c>auth_lockout</c> in memory, incrementing atomically as the MongoDB pipeline update does.
    /// </summary>
    private sealed class InMemoryLockoutRepository : ILockoutRepository
    {
        private readonly Dictionary<string, int> _failures = [];

        public Task<int> GetFailureCountAsync(string username, string remoteAddress)
        {
            lock (_failures)
            {
                return Task.FromResult(_failures.GetValueOrDefault(Key(username, remoteAddress)));
            }
        }

        public Task<int> RecordFailureAsync(string username, string remoteAddress, TimeSpan lockoutDuration)
        {
            lock (_failures)
            {
                var key = Key(username, remoteAddress);
                _failures[key] = _failures.GetValueOrDefault(key) + 1;
                return Task.FromResult(_failures[key]);
            }
        }

        public Task ClearAsync(string username, string remoteAddress)
        {
            lock (_failures)
            {
                _failures.Remove(Key(username, remoteAddress));
                return Task.CompletedTask;
            }
        }

        private static string Key(string username, string remoteAddress) => $"{username}|{remoteAddress}";
    }
}
