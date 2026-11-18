using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.History;

/// <summary>
/// The span and the counters of the history computation, exported by the OpenTelemetry auto-instrumentation once
/// <c>OTEL_DOTNET_AUTO_TRACES_ADDITIONAL_SOURCES</c> and <c>OTEL_DOTNET_AUTO_METRICS_ADDITIONAL_SOURCES</c> name <see cref="SourceName"/>.
/// <para>
/// Tags carry the tenant, the state name and the serial, never a value of the state, since a state holds every secret of its workspace.
/// </para>
/// </summary>
public static class StateHistoryTelemetry
{
    public const string SourceName = "Devpro.TerraformBackend.History";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> Computed = Meter.CreateCounter<long>(
        "tfbackend.history.computed",
        description: "Queue entries whose change was computed and written to tf_state_history");

    public static readonly Counter<long> Failed = Meter.CreateCounter<long>(
        "tfbackend.history.failed",
        description: "Changes that never reach tf_state_history, by reason");
}
