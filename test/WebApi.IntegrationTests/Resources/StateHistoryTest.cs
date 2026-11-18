using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.Infrastructure.MongoDb.History;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

/// <summary>
/// The history path: a state write queues the change in <c>tf_state_history_queue</c>, and the processor computes it into <c>tf_state_history</c>.
/// <para>
/// Only an update queues a change, never the first write for a name, since there is nothing yet to diff against.
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
        await PostStateAsync(client, name, """{"version":4,"serial":1}""");
        await PostStateAsync(client, name, """{"version":4,"serial":2}""");
        await ProcessHistoryAsync(Tenant, name);

        // Assert
        var entries = await FindHistoryEntriesAsync(name);
        entries.Should().HaveCount(1, "the second write is the one that has something to diff against");
        entries[0]["upgrade"].AsString.Should().Contain("serial",
            "the patch must describe the change between the two writes");
        (await FindQueueEntriesAsync(name)).Should().BeEmpty("a computed entry leaves the queue");
    }

    [Fact]
    public async Task StateResource_Updated_ReturnsBeforeTheHistoryIsComputed()
    {
        // Arrange
        var client = CreateClient(true);
        var name = UniqueStateName();
        TrackState(Tenant, name);

        // Act
        await PostStateAsync(client, name, """{"version":4,"serial":1}""");
        await PostStateAsync(client, name, """{"version":4,"serial":2}""");

        // Assert: the write is done, and its change waits in the queue with the version it replaced
        (await FindHistoryEntriesAsync(name)).Should().BeEmpty();
        var queued = await FindQueueEntriesAsync(name);
        queued.Should().ContainSingle();
        queued[0]["status"].AsString.Should().Be("pending");
        queued[0]["serial"].ToInt64().Should().Be(1);
        queued[0]["value"]["serial"].ToInt64().Should().Be(1, "an entry holds the replaced version, never the new one");
    }

    [Fact]
    public async Task StateResource_Updated_WritesCreatedAtAsSnakeCaseOnBothCollections()
    {
        // Arrange
        var client = CreateClient(true);
        var name = UniqueStateName();
        TrackState(Tenant, name);

        // Act
        await PostStateAsync(client, name, """{"version":4,"serial":1}""");
        await PostStateAsync(client, name, """{"version":4,"serial":2}""");
        await ProcessHistoryAsync(Tenant, name);

        // Assert: created_at, never createdAt, on the state itself and on the history entry the update wrote, since consuming applications query the documents by that name
        var state = await FindStateAsync(name);
        state.Should().NotBeNull();
        state!.Contains("created_at").Should().BeTrue();
        state.Contains("createdAt").Should().BeFalse();

        var entries = await FindHistoryEntriesAsync(name);
        entries.Should().ContainSingle();
        entries[0].Contains("created_at").Should().BeTrue();
        entries[0].Contains("createdAt").Should().BeFalse();
        entries[0]["created_at"].ToUniversalTime().Should().Be(state["created_at"].ToUniversalTime(),
            "the history entry is dated by the write, not by the time the worker computed it");
    }

    [Fact]
    public async Task StateResource_CreatedOnce_WritesNoTfStateHistoryEntry()
    {
        // Arrange
        var client = CreateClient(true);
        var name = UniqueStateName();
        TrackState(Tenant, name);

        // Act: a single write has no previous version to diff against
        await PostStateAsync(client, name, """{"version":4,"serial":1}""");
        await ProcessHistoryAsync(Tenant, name);

        // Assert
        (await FindQueueEntriesAsync(name)).Should().BeEmpty();
        (await FindHistoryEntriesAsync(name)).Should().BeEmpty();
    }

    [Fact]
    public async Task StateResource_WrittenTwiceIdentically_QueuesNothing()
    {
        // Arrange
        var client = CreateClient(true);
        var name = UniqueStateName();
        TrackState(Tenant, name);

        // Act
        await PostStateAsync(client, name, """{"version":4,"serial":1}""");
        await PostStateAsync(client, name, """{"version":4,"serial":1}""");

        // Assert
        (await FindQueueEntriesAsync(name)).Should().BeEmpty("an identical write has no change to compute");
    }

    [Fact]
    public async Task StateResource_UpdatedSeveralTimesBeforeProcessing_WritesOneEntryPerChangeInOrder()
    {
        // Arrange
        var client = CreateClient(true);
        var name = UniqueStateName();
        TrackState(Tenant, name);

        // Act: three writes queue two changes before the processor runs, so each change has to find its new version in the next entry or in tf_state
        await PostStateAsync(client, name, """{"version":4,"serial":1,"step":"first"}""");
        await PostStateAsync(client, name, """{"version":4,"serial":2,"step":"second"}""");
        await PostStateAsync(client, name, """{"version":4,"serial":3,"step":"third"}""");
        await ProcessHistoryAsync(Tenant, name);

        // Assert
        var entries = (await FindHistoryEntriesAsync(name)).OrderBy(x => x["created_at"].ToUniversalTime()).ToList();
        entries.Should().HaveCount(2);
        entries[0]["upgrade"].AsString.Should().Contain("second").And.NotContain("third");
        entries[1]["upgrade"].AsString.Should().Contain("third").And.NotContain("first");
        (await FindQueueEntriesAsync(name)).Should().BeEmpty();
    }

    [Fact]
    public async Task HistoryProcessor_DiffAboveTheLimit_SetsTheEntryAsideAndKeepsTheState()
    {
        // Arrange
        var client = CreateClient(true);
        var name = UniqueStateName();
        TrackState(Tenant, name);
        var before = $$"""{"version":4,"serial":1,"items":[{{string.Join(",", Enumerable.Range(0, 100))}}]}""";
        var after = $$"""{"version":4,"serial":2,"items":[{{string.Join(",", Enumerable.Range(100, 100))}}]}""";
        await PostStateAsync(client, name, before);
        await PostStateAsync(client, name, after);

        // Act: 100 replaced items need 10,000 comparisons, above a limit of 1,000
        await HistoryQueue.DrainAsync(Factory.Services, Tenant, name, TestContext.Current.CancellationToken,
            new StateHistoryOptions { MaxDiffComparisons = 1_000 });

        // Assert: the state is saved and served, and only its history entry is set aside, with an error that says why and how to recover
        (await FindHistoryEntriesAsync(name)).Should().BeEmpty();
        var getResponse = await client.GetAsync($"/{Tenant}/state/{name}", TestContext.Current.CancellationToken);
        (await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("199");

        var queued = await FindQueueEntriesAsync(name);
        queued.Should().ContainSingle();
        queued[0]["status"].AsString.Should().Be("failed");
        var error = queued[0]["error"].AsString;
        error.Should().Contain($"{Tenant}/{name}");
        error.Should().Contain("10000 array item comparisons");
        error.Should().Contain("History__MaxDiffComparisons");
        error.Should().Contain("$.items: 100 items before and 100 after");
        error.Should().Contain("back to pending");

        // Act: a failed entry is ignored, and does not hold back the next change of the same state
        await PostStateAsync(client, name, """{"version":4,"serial":3,"items":[]}""");
        await ProcessHistoryAsync(Tenant, name);

        // Assert
        (await FindHistoryEntriesAsync(name)).Should().ContainSingle("the change after the failed one is computed");
        (await FindQueueEntriesAsync(name)).Should().ContainSingle(x => x["status"] == "failed", "a failed entry stays for inspection");
    }

    [Fact]
    public async Task HistoryProcessor_StateDeletedBeforeProcessing_SetsTheEntryAside()
    {
        // Arrange
        var client = CreateClient(true);
        var name = UniqueStateName();
        TrackState(Tenant, name);
        await PostStateAsync(client, name, """{"version":4,"serial":1}""");
        await PostStateAsync(client, name, """{"version":4,"serial":2}""");
        await client.DeleteAsync($"/{Tenant}/state/{name}", TestContext.Current.CancellationToken);

        // Act
        await ProcessHistoryAsync(Tenant, name);

        // Assert: the version that followed is gone, so the change cannot be computed
        (await FindHistoryEntriesAsync(name)).Should().BeEmpty();
        var queued = await FindQueueEntriesAsync(name);
        queued.Should().ContainSingle();
        queued[0]["status"].AsString.Should().Be("failed");
        queued[0]["error"].AsString.Should().Contain("deleted");
    }

    [Fact]
    public async Task HistoryProcessor_EntryClaimedByAStoppedWorker_IsComputedAgain()
    {
        // Arrange: a claim that expired, as a worker that stopped mid-diff leaves it
        var client = CreateClient(true);
        var name = UniqueStateName();
        TrackState(Tenant, name);
        await PostStateAsync(client, name, """{"version":4,"serial":1}""");
        await PostStateAsync(client, name, """{"version":4,"serial":2}""");
        await SetQueueEntryAsync(name, Builders<BsonDocument>.Update
            .Set("status", "processing")
            .Set("claimed_at", DateTime.UtcNow.AddHours(-1))
            .Set("attempts", 1));

        // Act
        await ProcessHistoryAsync(Tenant, name);

        // Assert
        (await FindHistoryEntriesAsync(name)).Should().ContainSingle();
        (await FindQueueEntriesAsync(name)).Should().BeEmpty();
    }

    [Fact]
    public async Task HistoryProcessor_EntryThatNeverFinishes_IsSetAsideAfterTheLastAttempt()
    {
        // Arrange: three expired claims, as a diff that kills the process every time leaves it
        var client = CreateClient(true);
        var name = UniqueStateName();
        TrackState(Tenant, name);
        await PostStateAsync(client, name, """{"version":4,"serial":1}""");
        await PostStateAsync(client, name, """{"version":4,"serial":2}""");
        await SetQueueEntryAsync(name, Builders<BsonDocument>.Update
            .Set("status", "processing")
            .Set("claimed_at", DateTime.UtcNow.AddHours(-1))
            .Set("attempts", 3));

        // Act
        await ProcessHistoryAsync(Tenant, name);

        // Assert
        (await FindHistoryEntriesAsync(name)).Should().BeEmpty();
        var queued = await FindQueueEntriesAsync(name);
        queued.Should().ContainSingle();
        queued[0]["status"].AsString.Should().Be("failed");
        queued[0]["error"].AsString.Should().Contain("3 attempts started and none finished");
    }

    [Fact]
    public async Task HistoryProcessor_EntryAlreadyWrittenByAnEarlierAttempt_IsNotDuplicated()
    {
        // Arrange: a worker that stopped after writing the history entry and before removing the queue entry
        var client = CreateClient(true);
        var name = UniqueStateName();
        TrackState(Tenant, name);
        await PostStateAsync(client, name, """{"version":4,"serial":1}""");
        await PostStateAsync(client, name, """{"version":4,"serial":2}""");
        var queued = (await FindQueueEntriesAsync(name)).Single();
        await Collection("tf_state_history").InsertOneAsync(new BsonDocument
        {
            ["_id"] = queued["_id"],
            ["tenant"] = Tenant,
            ["name"] = name,
            ["created_at"] = queued["saved_at"],
            ["upgrade"] = "[]"
        }, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await ProcessHistoryAsync(Tenant, name);

        // Assert
        (await FindHistoryEntriesAsync(name)).Should().ContainSingle();
        (await FindQueueEntriesAsync(name)).Should().BeEmpty();
    }

    private async Task PostStateAsync(HttpClient client, string name, string json)
    {
        var response = await client.PostAsync($"/{Tenant}/state/{name}",
            new StringContent(json, Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private IMongoCollection<BsonDocument> Collection(string name) =>
        Factory.Services.GetRequiredService<IMongoDatabase>().GetCollection<BsonDocument>(name);

    private static FilterDefinition<BsonDocument> StateFilter(string name) =>
        Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("tenant", Tenant),
            Builders<BsonDocument>.Filter.Eq("name", name));

    private async Task<List<BsonDocument>> FindHistoryEntriesAsync(string name) =>
        await Collection("tf_state_history").Find(StateFilter(name)).ToListAsync(TestContext.Current.CancellationToken);

    private async Task<List<BsonDocument>> FindQueueEntriesAsync(string name) =>
        await Collection("tf_state_history_queue").Find(StateFilter(name)).ToListAsync(TestContext.Current.CancellationToken);

    private async Task SetQueueEntryAsync(string name, UpdateDefinition<BsonDocument> update) =>
        await Collection("tf_state_history_queue").UpdateOneAsync(StateFilter(name), update,
            cancellationToken: TestContext.Current.CancellationToken);

    private async Task<BsonDocument?> FindStateAsync(string name) =>
        await Collection("tf_state").Find(StateFilter(name)).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
}
