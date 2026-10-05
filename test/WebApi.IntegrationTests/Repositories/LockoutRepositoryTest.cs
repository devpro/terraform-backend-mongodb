using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.Domain.Repositories;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Repositories;

[Trait("Category", "IntegrationTests")]
public class LockoutRepositoryTest(TestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task LockoutRepository_RecordFailure_CountsUpFromZero()
    {
        // Arrange
        var username = UniqueUsername();
        TrackLockout(username);
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILockoutRepository>();

        // Act & Assert
        (await repository.GetFailureCountAsync(username, "1.2.3.4")).Should().Be(0);
        (await repository.RecordFailureAsync(username, "1.2.3.4", TimeSpan.FromMinutes(5))).Should().Be(1);
        (await repository.RecordFailureAsync(username, "1.2.3.4", TimeSpan.FromMinutes(5))).Should().Be(2);
        (await repository.GetFailureCountAsync(username, "1.2.3.4")).Should().Be(2);
    }

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task LockoutRepository_Clear_ResetsTheCounter()
    {
        // Arrange
        var username = UniqueUsername();
        TrackLockout(username);
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILockoutRepository>();
        await repository.RecordFailureAsync(username, "1.2.3.4", TimeSpan.FromMinutes(5));

        // Act
        await repository.ClearAsync(username, "1.2.3.4");

        // Assert
        (await repository.GetFailureCountAsync(username, "1.2.3.4")).Should().Be(0);
    }

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task LockoutRepository_ScopesByAddress_SoOneAddressDoesNotLockOutAnother()
    {
        // Arrange
        var username = UniqueUsername();
        TrackLockout(username);
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILockoutRepository>();

        // Act
        await repository.RecordFailureAsync(username, "1.2.3.4", TimeSpan.FromMinutes(5));
        await repository.RecordFailureAsync(username, "1.2.3.4", TimeSpan.FromMinutes(5));

        // Assert: a lockout scoped to the username alone would show a count here too
        (await repository.GetFailureCountAsync(username, "5.6.7.8")).Should().Be(0);
    }

    /// <summary>
    /// The sequential tests above prove the counter moves; this one proves it moves exactly once per failure under real concurrency, which a check-then-write implementation would not.
    /// </summary>
    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task LockoutRepository_ConcurrentRecordFailure_CountsEveryFailureExactlyOnce()
    {
        // Arrange
        var username = UniqueUsername();
        TrackLockout(username);
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILockoutRepository>();

        // Act: races twenty failures for the same pair at once
        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => repository.RecordFailureAsync(username, "1.2.3.4", TimeSpan.FromMinutes(5))));

        // Assert: twenty distinct counter values, one through twenty, proves no increment was lost to a race
        results.Order().Should().Equal(Enumerable.Range(1, 20));
        (await repository.GetFailureCountAsync(username, "1.2.3.4")).Should().Be(20);
    }

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task LockoutRepository_RecordFailureAfterExpiry_StartsAFreshWindowRatherThanExtending()
    {
        // Arrange
        var username = UniqueUsername();
        TrackLockout(username);
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILockoutRepository>();
        // a window that has already elapsed by the time the next failure arrives
        await repository.RecordFailureAsync(username, "1.2.3.4", TimeSpan.Zero);

        // Act
        var afterExpiry = await repository.RecordFailureAsync(username, "1.2.3.4", TimeSpan.FromMinutes(5));

        // Assert: restarted at one rather than continuing from the expired window's count
        afterExpiry.Should().Be(1);
    }

    private static string UniqueUsername() => $"lockout-test-{Guid.NewGuid():N}";

    private void TrackLockout(string username) =>
        TrackDocumentsWhere("auth_lockout", Builders<BsonDocument>.Filter.Eq("username", username));
}
