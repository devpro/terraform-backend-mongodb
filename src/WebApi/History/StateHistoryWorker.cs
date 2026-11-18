using Devpro.TerraformBackend.Infrastructure.MongoDb.History;

namespace Devpro.TerraformBackend.WebApi.History;

/// <summary>
/// Drains <c>tf_state_history_queue</c> in the background, one entry at a time, so a state write returns as soon as the state is saved.
/// <para>
/// It wakes on <see cref="StateHistorySignal"/> after a write served by this process,
/// and polls for the writes of other replicas and for the entries a stopped worker left claimed.
/// </para>
/// </summary>
internal sealed class StateHistoryWorker(IServiceScopeFactory scopeFactory, StateHistorySignal signal, ILogger<StateHistoryWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            bool processed;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<StateHistoryProcessor>();
                processed = await processor.ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // an unhandled exception would stop the host, and with it every state write
                logger.LogError(exception, "The history queue could not be read, next attempt in {PollInterval}.", PollInterval);
                processed = false;
            }

            if (processed) continue;

            try
            {
                await signal.WaitAsync(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
