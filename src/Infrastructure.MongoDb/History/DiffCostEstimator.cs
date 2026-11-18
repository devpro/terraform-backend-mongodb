using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.History;

/// <summary>
/// Estimates the work of the history diff before it starts, since a running diff can be neither interrupted nor bounded in memory.
/// <para>
/// SystemTextJson.JsonDiffPatch leaves out the items two arrays share at their start and at their end,
/// then compares every remaining item of one array with every remaining item of the other.
/// Measured on .NET 10, one comparison allocates about 70 bytes: 2,000 integers replaced take 280 MB, 8,000 take 4.4 GB.
/// </para>
/// <para>
/// Arrays are paired the way the diff pairs them, by property name inside an object and by position inside the remaining part of an array,
/// so a nested array is counted even where the diff would match its parent differently, which errs on the side of a higher estimate.
/// </para>
/// </summary>
public static class DiffCostEstimator
{
    public const int BytesPerComparison = 70;

    public static DiffCost Estimate(JsonNode? before, JsonNode? after)
    {
        var cost = new DiffCost();
        Visit(before, after, ["$"], cost);
        return cost;
    }

    private static void Visit(JsonNode? before, JsonNode? after, List<string> path, DiffCost cost)
    {
        switch (before, after)
        {
            case (JsonObject beforeObject, JsonObject afterObject):
                foreach (var (key, value) in beforeObject)
                {
                    if (!afterObject.TryGetPropertyValue(key, out var other)) continue;
                    path.Add("." + key);
                    Visit(value, other, path, cost);
                    path.RemoveAt(path.Count - 1);
                }
                break;

            case (JsonArray beforeArray, JsonArray afterArray):
                var head = 0;
                while (head < beforeArray.Count && head < afterArray.Count
                    && JsonNode.DeepEquals(beforeArray[head], afterArray[head]))
                {
                    head++;
                }

                var tail = 0;
                while (tail < beforeArray.Count - head && tail < afterArray.Count - head
                    && JsonNode.DeepEquals(beforeArray[beforeArray.Count - 1 - tail], afterArray[afterArray.Count - 1 - tail]))
                {
                    tail++;
                }

                var beforeDiffering = beforeArray.Count - head - tail;
                var afterDiffering = afterArray.Count - head - tail;
                cost.Add(path, beforeArray.Count, afterArray.Count, beforeDiffering, afterDiffering);

                for (var i = 0; i < Math.Min(beforeDiffering, afterDiffering); i++)
                {
                    path.Add($"[{head + i}]");
                    Visit(beforeArray[head + i], afterArray[head + i], path, cost);
                    path.RemoveAt(path.Count - 1);
                }
                break;
        }
    }
}

/// <summary>
/// The estimated work of one diff, and the array that accounts for the largest part of it, which is what an operator needs to find the cause.
/// </summary>
public sealed class DiffCost
{
    public long Comparisons { get; private set; }

    public long EstimatedBytes => Comparisons * DiffCostEstimator.BytesPerComparison;

    public string LargestArrayPath { get; private set; } = "$";

    public int LargestArrayBeforeLength { get; private set; }

    public int LargestArrayAfterLength { get; private set; }

    public int LargestArrayBeforeDiffering { get; private set; }

    public int LargestArrayAfterDiffering { get; private set; }

    public long LargestArrayComparisons { get; private set; }

    internal void Add(List<string> path, int beforeLength, int afterLength, int beforeDiffering, int afterDiffering)
    {
        var comparisons = (long)beforeDiffering * afterDiffering;
        Comparisons += comparisons;
        if (comparisons <= LargestArrayComparisons) return;

        LargestArrayComparisons = comparisons;
        LargestArrayPath = Join(path);
        LargestArrayBeforeLength = beforeLength;
        LargestArrayAfterLength = afterLength;
        LargestArrayBeforeDiffering = beforeDiffering;
        LargestArrayAfterDiffering = afterDiffering;
    }

    private static string Join(List<string> path)
    {
        var builder = new StringBuilder();
        foreach (var segment in path) builder.Append(segment);
        return builder.ToString();
    }
}
