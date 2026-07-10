using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.Domain.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Repositories;

[Trait("Category", "IntegrationTests")]
public class StateLockRepositoryTest(WebApplicationFactory<Program> factory)
    : IntegrationTestBase(factory)
{
    private const string Tenant = "dummy";

    [Fact]
    public async Task StateLockRepository_CreateOnAlreadyLockedState_ReturnsNull()
    {
        // Arrange
        var name = $"lockrace{Guid.NewGuid():N}";
        var firstLock = StateLockFaker.Generate();
        firstLock.Tenant = Tenant;
        firstLock.Name = name;
        var secondLock = StateLockFaker.Generate();
        secondLock.Tenant = Tenant;
        secondLock.Name = name;

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
}
