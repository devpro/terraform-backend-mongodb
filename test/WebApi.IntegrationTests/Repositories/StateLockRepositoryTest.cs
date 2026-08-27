using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.Domain.Repositories;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Repositories;

[Trait("Category", "IntegrationTests")]
public class StateLockRepositoryTest(TestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const string Tenant = TestCredentials.Tenant;

    [Fact]
    public async Task StateLockRepository_CreateOnAlreadyLockedState_ReturnsNull()
    {
        // Arrange
        var name = UniqueStateName();
        var firstLock = StateLockFaker.Generate();
        firstLock.Tenant = Tenant;
        firstLock.Name = name;
        var secondLock = StateLockFaker.Generate();
        secondLock.Tenant = Tenant;
        secondLock.Name = name;
        // the lock is deleted by the assertions below, but a failure before that must not leave it holding
        // the unique index against the next run
        TrackState(Tenant, name);

        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IStateLockRepository>();

        // Act & Assert
        var created = await repository.CreateAsync(firstLock);
        created.Should().NotBeNull();

        // simulates a concurrent run winning the race between the controller's lock check and the insert
        var conflicting = await repository.CreateAsync(secondLock);
        conflicting.Should().BeNull();

        var deleted = await repository.DeleteAsync(firstLock);
        deleted.Should().BeTrue();
    }

    /// <summary>
    /// The sequential test above simulates the race by comment, this one actually runs it.
    /// <para>
    /// The 2026-07-10 fix made <see cref="Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories.StateLockRepository.CreateAsync"/> insert first and map the duplicate-key error, rather than checking for an existing lock and inserting as two separate steps.
    /// That closes the race between the check and the insert, but nothing before this test ever ran two inserts at the same time to prove it.
    /// A check-then-insert bug only shows up under real concurrency, never when the calls happen one after another.
    /// </para>
    /// </summary>
    [Fact]
    public async Task StateLockRepository_ConcurrentCreate_OnlyOneWins()
    {
        // Arrange
        var name = UniqueStateName();
        var locks = Enumerable.Range(0, 8)
            .Select(_ =>
            {
                var stateLock = StateLockFaker.Generate();
                stateLock.Tenant = Tenant;
                stateLock.Name = name;
                return stateLock;
            })
            .ToList();
        // a failure part-way through the race must not leave a lock holding the unique index against the
        // next run
        TrackState(Tenant, name);

        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IStateLockRepository>();

        // Act: races every insert against the same {tenant, name} unique index at once
        var results = await Task.WhenAll(locks.Select(repository.CreateAsync));

        // Assert
        results.Count(result => result != null).Should().Be(1, "exactly one concurrent run must win the lock");
        var winner = results.Single(result => result != null)!;

        var stored = await repository.FindOneAsync(Tenant, name);
        stored.Should().NotBeNull();
        stored!.Id.Should().Be(winner.Id);

        var deleted = await repository.DeleteAsync(winner);
        deleted.Should().BeTrue();
    }
}
