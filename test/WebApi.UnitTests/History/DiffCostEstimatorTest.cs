using System.Linq;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Devpro.TerraformBackend.Infrastructure.MongoDb.History;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.UnitTests.History;

[Trait("Category", "UnitTests")]
public class DiffCostEstimatorTest
{
    [Fact]
    public void Estimate_ArrayReplacedEntirely_CountsEveryPair()
    {
        var cost = Estimate(Ints(0, 100), Ints(100, 100));

        cost.Comparisons.Should().Be(10_000);
        cost.EstimatedBytes.Should().Be(10_000 * DiffCostEstimator.BytesPerComparison);
        cost.LargestArrayPath.Should().Be("$.items");
        cost.LargestArrayBeforeDiffering.Should().Be(100);
        cost.LargestArrayAfterDiffering.Should().Be(100);
    }

    [Fact]
    public void Estimate_ItemAppended_LeavesOutTheSharedStart()
    {
        var cost = Estimate(Ints(0, 1000), Ints(0, 1001));

        cost.Comparisons.Should().Be(0, "the diff leaves out the 1,000 items both arrays start with");
    }

    [Fact]
    public void Estimate_ItemChangedInTheMiddle_LeavesOutTheSharedStartAndEnd()
    {
        var before = Enumerable.Range(0, 1000).ToArray();
        var after = before.Select(x => x == 500 ? -1 : x).ToArray();

        var cost = Estimate(Items(before), Items(after));

        cost.Comparisons.Should().Be(1);
        cost.LargestArrayPath.Should().Be("$.items");
    }

    [Fact]
    public void Estimate_NestedArray_NamesItsPath()
    {
        var before = JsonNode.Parse($$"""{"resources":[{"name":"a","instances":[{{string.Join(",", Enumerable.Range(0, 50))}}]}]}""");
        var after = JsonNode.Parse($$"""{"resources":[{"name":"a","instances":[{{string.Join(",", Enumerable.Range(50, 50))}}]}]}""");

        var cost = DiffCostEstimator.Estimate(before, after);

        cost.LargestArrayPath.Should().Be("$.resources[0].instances");
        cost.LargestArrayComparisons.Should().Be(2_500);
        cost.Comparisons.Should().Be(2_501, "the outer array holds one differing item on each side");
    }

    [Fact]
    public void Estimate_IdenticalDocuments_CostsNothing()
    {
        var cost = Estimate(Ints(0, 100), Ints(0, 100));

        cost.Comparisons.Should().Be(0);
    }

    private static DiffCost Estimate(string before, string after) =>
        DiffCostEstimator.Estimate(JsonNode.Parse(before), JsonNode.Parse(after));

    private static string Ints(int start, int count) => Items(Enumerable.Range(start, count).ToArray());

    private static string Items(int[] items) => $$"""{"items":[{{string.Join(",", items)}}]}""";
}
