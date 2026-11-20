using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.JsonDiffPatch;
using System.Text.Json.JsonDiffPatch.Diffs.Formatters;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories;
using Devpro.TerraformBackend.Infrastructure.MongoDb.Serialization;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.History;

/// <summary>
/// Computes the changes queued in <c>tf_state_history_queue</c> and writes them to <c>tf_state_history</c>, one entry at a time.
/// <para>
/// The diff runs here rather than in the request, so a state write never waits for it,
/// and one entry at a time per process bounds the memory the diffs take together.
/// The cost of a single diff is bounded by <see cref="StateHistoryOptions.MaxDiffComparisons"/>, checked before the diff starts.
/// </para>
/// <para>
/// An entry that cannot be computed is set aside with the <c>failed</c> status and an <c>error</c> naming the reason,
/// and the same reason goes to the log, to the span and to the <c>tfbackend.history.failed</c> counter.
/// </para>
/// </summary>
public class StateHistoryProcessor(
    StateHistoryQueueRepository queueRepository,
    StateHistoryRepository historyRepository,
    StateRepository stateRepository,
    StateHistoryOptions options,
    ILogger<StateHistoryProcessor> logger)
{
    private const int CandidateBatchSize = 20;

    /// <summary>
    /// Computes the next entry this worker can claim, restricted to one state when a tenant and a name are given.
    /// Returns false when there is nothing to claim.
    /// </summary>
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken, string? tenant = null, string? name = null)
    {
        var now = DateTime.UtcNow;
        await SetAbandonedAsideAsync(now, tenant, name, cancellationToken);

        var entry = await ClaimNextAsync(now, tenant, name, cancellationToken);
        if (entry == null) return false;

        await ProcessAsync(entry, cancellationToken);
        return true;
    }

    private async Task<BsonDocument?> ClaimNextAsync(DateTime now, string? tenant, string? name, CancellationToken cancellationToken)
    {
        var candidates = await queueRepository.FindClaimableAsync(now, options, tenant, name, CandidateBatchSize, cancellationToken);
        foreach (var candidate in candidates)
        {
            // only the first entry of a state is claimed, so an entry is never computed while an earlier one of the same state is in progress
            var first = await queueRepository.FindFirstOfStateAsync(candidate["tenant"].AsString, candidate["name"].AsString, cancellationToken);
            if (first == null || first["_id"] != candidate["_id"]) continue;

            var claimed = await queueRepository.TryClaimAsync(candidate, now, cancellationToken);
            if (claimed != null) return claimed;
        }

        return null;
    }

    private async Task ProcessAsync(BsonDocument entry, CancellationToken cancellationToken)
    {
        var id = entry["_id"].AsObjectId;
        var tenant = entry["tenant"].AsString;
        var name = entry["name"].AsString;
        var serial = entry["serial"].ToInt64();
        var savedAt = entry["saved_at"].ToUniversalTime();

        using var activity = StateHistoryTelemetry.ActivitySource.StartActivity("compute state history");
        activity?.SetTag("tfbackend.tenant", tenant);
        activity?.SetTag("tfbackend.state.name", name);
        activity?.SetTag("tfbackend.state.serial", serial);
        activity?.SetTag("tfbackend.history.queue_entry_id", id.ToString());
        activity?.SetTag("tfbackend.history.attempt", entry["attempts"].ToInt32());

        try
        {
            // the state is read before a later entry is looked for: a write landing between the two then shows up as that later entry
            var current = await stateRepository.FindDocumentAsync(tenant, name, cancellationToken);
            if (current == null || current["_id"] != entry["state_id"])
            {
                // a tenant rename in an application sharing the database moves the state and keeps its _id, but leaves its queue entries behind:
                // computed where they are, they would lose the change, or write it to tf_state_history under a tenant the state left
                var moved = await stateRepository.FindDocumentByIdAsync(entry["state_id"].AsObjectId, cancellationToken);
                if (moved != null)
                {
                    var newTenant = moved["tenant"].AsString;
                    var newName = moved["name"].AsString;
                    logger.LogInformation(
                        "History entries of state {Tenant}/{Name} follow the state to {NewTenant}/{NewName}",
                        tenant, name, newTenant, newName);
                    await queueRepository.MoveStateAsync(entry["state_id"].AsObjectId, newTenant, newName, cancellationToken);
                    await queueRepository.ReleaseAsync(id, countAttempt: false, CancellationToken.None);
                    return;
                }
            }

            var after = await queueRepository.FindNextValueAsync(entry, cancellationToken)
                ?? (current != null && current["_id"] == entry["state_id"] ? current["value"] : null);

            if (after == null)
            {
                var error = $"History entry not computed for state {tenant}/{name} at serial {serial}: " +
                    "the state was deleted, or deleted and created again, before the change was computed, " +
                    "so the version that followed serial " + serial + " no longer exists.";
                logger.LogWarning(
                    "History entry not computed for state {Tenant}/{Name} at serial {Serial}: " +
                    "the state was deleted, or deleted and created again, before the change was computed, so the version that followed no longer exists. " +
                    "Queue entry {QueueEntryId} in tf_state_history_queue keeps the replaced version with the status failed.",
                    tenant, name, serial, id);
                await SetAsideAsync(id, "state_deleted", error, activity, null);
                return;
            }

            var beforeJson = BsonToJsonConverter.Convert(entry["value"]);
            var afterJson = BsonToJsonConverter.Convert(after);

            var cost = DiffCostEstimator.Estimate(JsonNode.Parse(beforeJson), JsonNode.Parse(afterJson));
            activity?.SetTag("tfbackend.history.estimated_comparisons", cost.Comparisons);
            if (cost.Comparisons > options.MaxDiffComparisons)
            {
                await SetAsideTooExpensiveAsync(id, tenant, name, serial, savedAt, cost, activity);
                return;
            }

            var patch = GenerateJsonDiff(beforeJson, afterJson);
            if (patch != null)
            {
                await historyRepository.CreateAsync(id, tenant, name, savedAt, patch, cancellationToken);
            }

            await queueRepository.DeleteAsync(id, CancellationToken.None);
            StateHistoryTelemetry.Computed.Add(1);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // stopping is not a failed attempt
            await queueRepository.ReleaseAsync(id, countAttempt: false, CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            logger.LogError(exception,
                "History entry for state {Tenant}/{Name} at serial {Serial} could not be computed, attempt {Attempt} of {MaxAttempts}. " +
                "Queue entry {QueueEntryId} goes back to the queue.",
                tenant, name, serial, entry["attempts"].ToInt32(), options.MaxAttempts, id);
            await queueRepository.ReleaseAsync(id, countAttempt: true, CancellationToken.None);
        }
    }

    private async Task SetAsideTooExpensiveAsync(ObjectId id, string tenant, string name, long serial, DateTime savedAt, DiffCost cost,
        Activity? activity)
    {
        var estimatedMegabytes = cost.EstimatedBytes / (1024 * 1024);
        var error =
            $"History entry not computed for state {tenant}/{name}: the change from serial {serial}, saved at {savedAt:O}, " +
            $"needs an estimated {cost.Comparisons} array item comparisons, about {estimatedMegabytes} MB of memory, " +
            $"above the limit of {options.MaxDiffComparisons} set by History:MaxDiffComparisons (environment variable History__MaxDiffComparisons). " +
            $"The largest part is the array at {cost.LargestArrayPath}: {cost.LargestArrayBeforeLength} items before and {cost.LargestArrayAfterLength} after, " +
            $"of which {cost.LargestArrayBeforeDiffering} and {cost.LargestArrayAfterDiffering} differ once the items both share at the start and at the end are left out. " +
            "The state is saved and served as usual, only this history entry is missing. " +
            $"Queue entry {id} in tf_state_history_queue keeps the replaced version with the status failed: " +
            "raising the limit, then setting the status of the entry back to pending and its attempts to 0, computes it.";

        logger.LogError(
            "History entry not computed for state {Tenant}/{Name}: the change from serial {Serial}, saved at {SavedAt}, " +
            "needs an estimated {EstimatedComparisons} array item comparisons, about {EstimatedMegabytes} MB of memory, " +
            "above the limit of {MaxDiffComparisons} set by History:MaxDiffComparisons (environment variable History__MaxDiffComparisons). " +
            "The largest part is the array at {ArrayPath}: {BeforeLength} items before and {AfterLength} after, " +
            "of which {BeforeDiffering} and {AfterDiffering} differ once the items both share at the start and at the end are left out. " +
            "The state is saved and served as usual, only this history entry is missing. " +
            "Queue entry {QueueEntryId} in tf_state_history_queue keeps the replaced version with the status failed: " +
            "raising the limit, then setting the status of the entry back to pending and its attempts to 0, computes it.",
            tenant, name, serial, savedAt.ToString("O"), cost.Comparisons, estimatedMegabytes, options.MaxDiffComparisons,
            cost.LargestArrayPath, cost.LargestArrayBeforeLength, cost.LargestArrayAfterLength,
            cost.LargestArrayBeforeDiffering, cost.LargestArrayAfterDiffering, id);

        await SetAsideAsync(id, "diff_too_expensive", error, activity, new StateHistoryDiffTooExpensiveException(error));
    }

    /// <summary>
    /// Sets aside the entries no attempt ever finished, before claiming, so they are reported once rather than retried forever.
    /// </summary>
    private async Task SetAbandonedAsideAsync(DateTime now, string? tenant, string? name, CancellationToken cancellationToken)
    {
        foreach (var entry in await queueRepository.FindAbandonedAsync(now, options, tenant, name, cancellationToken))
        {
            var id = entry["_id"].AsObjectId;
            var entryTenant = entry["tenant"].AsString;
            var entryName = entry["name"].AsString;
            var serial = entry["serial"].ToInt64();
            var attempts = entry["attempts"].ToInt32();

            var error =
                $"History entry not computed for state {entryTenant}/{entryName} at serial {serial}: " +
                $"{attempts} attempts started and none finished within {options.ClaimTimeout}, " +
                "which is what happens when the process stops or runs out of memory during the diff. " +
                "The state is saved and served as usual, only this history entry is missing. " +
                $"Queue entry {id} in tf_state_history_queue keeps the replaced version with the status failed: " +
                "setting its status back to pending and its attempts to 0 computes it again.";
            logger.LogError(
                "History entry not computed for state {Tenant}/{Name} at serial {Serial}: " +
                "{Attempts} attempts started and none finished within {ClaimTimeout}, " +
                "which is what happens when the process stops or runs out of memory during the diff. " +
                "The state is saved and served as usual, only this history entry is missing. " +
                "Queue entry {QueueEntryId} in tf_state_history_queue keeps the replaced version with the status failed: " +
                "setting its status back to pending and its attempts to 0 computes it again.",
                entryTenant, entryName, serial, attempts, options.ClaimTimeout, id);

            await SetAsideAsync(id, "abandoned", error, null, null);
        }
    }

    private async Task SetAsideAsync(ObjectId id, string reason, string error, Activity? activity, Exception? exception)
    {
        if (activity != null)
        {
            activity.SetStatus(ActivityStatusCode.Error, error);
            activity.SetTag("tfbackend.history.failure_reason", reason);
            if (exception != null) activity.AddException(exception);
        }

        StateHistoryTelemetry.Failed.Add(1, new KeyValuePair<string, object?>("reason", reason));
        await queueRepository.MarkFailedAsync(id, error, CancellationToken.None);
    }

    private static JsonNode? GenerateJsonDiff(string first, string second)
    {
        var diffNode = JsonDiffPatcher.Diff(first, second, new JsonPatchDeltaFormatter());
        return diffNode switch
        {
            null or JsonObject { Count: 0 } or JsonArray { Count: 0 } => null,
            _ => diffNode
        };
    }
}

/// <summary>
/// Carries the reason a change was set aside to the span, as an exception event, without ever being thrown.
/// </summary>
public sealed class StateHistoryDiffTooExpensiveException(string message) : Exception(message);
