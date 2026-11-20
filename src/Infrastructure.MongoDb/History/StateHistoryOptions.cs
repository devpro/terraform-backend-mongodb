using System;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.History;

public sealed class StateHistoryOptions
{
    /// <summary>
    /// The estimated array item comparisons above which a change is set aside rather than computed, see <see cref="DiffCostEstimator"/>.
    /// </summary>
    public long MaxDiffComparisons { get; init; } = 4_000_000;

    /// <summary>
    /// How long a claimed entry belongs to the worker that claimed it, after which another worker takes it over.
    /// </summary>
    public TimeSpan ClaimTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Claims of one entry before it is set aside, so that a change whose diff kills the process does not kill it again on every restart.
    /// </summary>
    public int MaxAttempts { get; init; } = 3;
}
