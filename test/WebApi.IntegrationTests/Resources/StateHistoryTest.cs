using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

/// <summary>
/// The history path had no coverage at all.
/// <para>
/// <c>StateRepository.CreateAsync</c> only writes to <c>tf_state_history</c> on an update, never on the first write for a name, since there is nothing yet to diff against.
/// Both halves of that rule need a test, or a change that breaks either one passes silently.
/// </para>
/// </summary>
[Trait("Category", "IntegrationTests")]
public class StateHistoryTest(TestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const string Tenant = TestCredentials.Tenant;

    [Fact]
    public async Task StateResource_UpdatedASecondTime_WritesATfStateHistoryEntry()
    {
        // Arrange
        var client = CreateClient(true);
        var name = UniqueStateName();
        TrackState(Tenant, name);

        // Act
        await client.PostAsync($"/{Tenant}/state/{name}",
            new StringContent("""{"version":4,"serial":1}""", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        await client.PostAsync($"/{Tenant}/state/{name}",
            new StringContent("""{"version":4,"serial":2}""", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        // Assert
        var entries = await FindHistoryEntriesAsync(name);
        entries.Should().HaveCount(1, "the second write is the one that has something to diff against");
        entries[0]["upgrade"].AsString.Should().Contain("serial",
            "the patch must describe the change between the two writes");
    }

    [Fact]
    public async Task StateResource_CreatedOnce_WritesNoTfStateHistoryEntry()
    {
        // Arrange
        var client = CreateClient(true);
        var name = UniqueStateName();
        TrackState(Tenant, name);

        // Act: a single write has no previous version to diff against
        await client.PostAsync($"/{Tenant}/state/{name}",
            new StringContent("""{"version":4,"serial":1}""", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        // Assert
        var entries = await FindHistoryEntriesAsync(name);
        entries.Should().BeEmpty();
    }

    private async Task<List<BsonDocument>> FindHistoryEntriesAsync(string name)
    {
        var history = Factory.Services.GetRequiredService<IMongoDatabase>()
            .GetCollection<BsonDocument>("tf_state_history");
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("tenant", Tenant),
            Builders<BsonDocument>.Filter.Eq("name", name));
        return await history.Find(filter).ToListAsync(TestContext.Current.CancellationToken);
    }
}
