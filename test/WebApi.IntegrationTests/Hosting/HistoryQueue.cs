using System;
using System.Threading;
using System.Threading.Tasks;
using Devpro.TerraformBackend.Infrastructure.MongoDb.History;
using Microsoft.Extensions.DependencyInjection;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;

/// <summary>
/// Computes the queued history of one state on demand, since the test hosts run without the background worker.
/// <para>
/// A worker running on its own would compute an entry whenever it got to it, possibly after the test that wrote it had already cleaned up,
/// and the history entry it then writes would outlive the test.
/// Draining one state only, never the whole queue, keeps a test from computing the entries of another test running at the same time.
/// </para>
/// </summary>
internal static class HistoryQueue
{
    public static async Task DrainAsync(IServiceProvider services, string tenant, string name, CancellationToken cancellationToken,
        StateHistoryOptions? options = null)
    {
        await using var scope = services.CreateAsyncScope();
        var processor = options == null
            ? scope.ServiceProvider.GetRequiredService<StateHistoryProcessor>()
            : ActivatorUtilities.CreateInstance<StateHistoryProcessor>(scope.ServiceProvider, options);
        while (await processor.ProcessNextAsync(cancellationToken, tenant, name))
        {
        }
    }
}
