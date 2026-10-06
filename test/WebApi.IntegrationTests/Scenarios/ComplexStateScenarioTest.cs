using System.Collections.Generic;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Scenarios;

/// <summary>
/// A real <c>terraform apply</c> against a state shape nothing in this repository models.
/// <para>
/// Every other scenario and resource test either drives a state made of strings and small integers, or posts a payload built by hand.
/// This one drives <c>samples/complex-state</c>, whose <c>terraform_data.complex_state</c> resource carries a deeply nested object with maps,
/// a list of objects, booleans, a null and an integer beyond <c>Int64</c>, through the real Terraform CLI.
/// </para>
/// <para>
/// It checks three things the other tests do not put together: the document lands in MongoDB with its nested fields queryable by an aggregation,
/// exactly the kind of query <c>liveship</c> would run, the full lifecycle (create, update, no-op, destroy) works against that shape,
/// and an update to it produces exactly one correctly-computed <c>tf_state_history</c> entry while a no-op produces none.
/// </para>
/// </summary>
public class ComplexStateScenarioTest(TestKestrelWebAppFactory kestrelWebAppFactory, ITestOutputHelper testOutputHelper)
    : ScenarioBase(kestrelWebAppFactory, testOutputHelper)
{
    protected override string ScenarioPath => "samples/complex-state";

    [Fact]
    public async Task Terraform_ComplicatedState_IsQueryableAndHistoryIsComputed()
    {
        await ExecuteTerraformAsync("init",
            expectedOutput: "Terraform has been successfully initialized!");

        await ExecuteTerraformAsync("plan",
            expectedOutput: "Plan: 4 to add, 0 to change, 0 to destroy.");

        // Act: the first write, nothing exists yet to diff against
        await ExecuteTerraformAsync("apply -auto-approve",
            expectedOutput: "Apply complete! Resources: 4 added, 0 changed, 0 destroyed.");

        // Assert: the nested payload is queryable by resource type and name, the way a consuming application reads tf_state rather than through this API
        var created = await FindComplexStateAttributesAsync();
        created.Should().NotBeNull("terraform_data.complex_state must be findable by an attribute query");
        created!["metadata"]["region"].AsString.Should().Be("eu-west-3");
        created["metadata"]["revision"].AsInt32.Should().Be(1);
        created["metadata"]["tags"].AsBsonArray.Should().Contain(new BsonString("critical"));
        created["network"]["enabled"].AsBoolean.Should().BeTrue();
        created["network"]["disabled_feature"].Should().Be(BsonNull.Value);
        created["network"]["subnets"].AsBsonArray[0]["public"].AsBoolean.Should().BeTrue();
        created["network"]["subnets"].AsBsonArray[1]["capacity"].AsInt32.Should().Be(65534);
        created["scaling"]["target"].AsDouble.Should().Be(12.5);
        // the same shape as the H2 finding, produced by a real apply rather than a constructed payload
        created["scaling"]["identifier"].BsonType.Should().Be(BsonType.Decimal128);
        created["scaling"]["identifier"].AsDecimal128.Should().Be(Decimal128.Parse("123456789012345678901234567890"));

        var historyAfterCreate = await FindHistoryEntriesAsync();
        historyAfterCreate.Should().BeEmpty("a first write has nothing to diff against");

        // Act: a real update, driven by a Terraform variable rather than a second HTTP call
        await ExecuteTerraformAsync("apply -auto-approve -var revision=2",
            expectedOutput: "Apply complete! Resources: 0 added, 1 changed, 0 destroyed.");

        // Assert: the update landed and is queryable the same way
        var updated = await FindComplexStateAttributesAsync();
        updated.Should().NotBeNull();
        updated!["metadata"]["revision"].AsInt32.Should().Be(2);

        var historyAfterUpdate = await FindHistoryEntriesAsync();
        historyAfterUpdate.Should().HaveCount(1, "the update is the one write that has something to diff against");
        historyAfterUpdate[0]["upgrade"].AsString.Should().Contain("revision",
            "the patch must describe the field that actually changed");

        // Act: a no-op apply, which must not add a spurious history entry
        await ExecuteTerraformAsync("apply -auto-approve -var revision=2",
            expectedOutput: "Apply complete! Resources: 0 added, 0 changed, 0 destroyed.");

        var historyAfterNoOp = await FindHistoryEntriesAsync();
        historyAfterNoOp.Should().HaveCount(1, "a no-op apply has nothing to diff, so it must add nothing");

        await ExecuteTerraformAsync("destroy -auto-approve -var revision=2",
            expectedOutput: "Destroy complete! Resources: 4 destroyed.");
    }

    /// <summary>
    /// Finds <c>terraform_data.complex_state</c> by resource type and name, an aggregation over the array rather than a positional index.
    /// <para>
    /// The state's <c>resources</c> array is not in declaration order, Terraform wrote it alphabetically by resource type in this run,
    /// so indexing into it would be as fragile as the query pattern the storage contract exists to avoid.
    /// </para>
    /// </summary>
    private async Task<BsonDocument?> FindComplexStateAttributesAsync()
    {
        var collection = Factory.Services.GetRequiredService<IMongoDatabase>()
            .GetCollection<BsonDocument>("tf_state");

        var stages = new BsonDocument[]
        {
            new("$match", new BsonDocument
            {
                ["tenant"] = TestCredentials.Tenant,
                ["name"] = StateName
            }),
            new("$unwind", "$value.resources"),
            new("$match", new BsonDocument
            {
                ["value.resources.type"] = "terraform_data",
                ["value.resources.name"] = "complex_state"
            }),
            new("$unwind", "$value.resources.instances"),
            // terraform_data.input and .output are typed "any", so the JSON state wraps them as {value, type} rather than storing the payload directly;
            // "value" is the plain nested object that was assigned
            new("$replaceRoot", new BsonDocument("newRoot", "$value.resources.instances.attributes.output.value"))
        };

        return await collection.Aggregate<BsonDocument>(stages)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<BsonDocument>> FindHistoryEntriesAsync()
    {
        var history = Factory.Services.GetRequiredService<IMongoDatabase>()
            .GetCollection<BsonDocument>("tf_state_history");
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("tenant", TestCredentials.Tenant),
            Builders<BsonDocument>.Filter.Eq("name", StateName));
        return await history.Find(filter).ToListAsync(TestContext.Current.CancellationToken);
    }
}
