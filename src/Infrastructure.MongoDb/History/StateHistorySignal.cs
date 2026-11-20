using System;
using System.Threading;
using System.Threading.Tasks;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.History;

/// <summary>
/// Wakes the history worker of this process as soon as a state write queues a change, so the change is not left waiting for the next poll.
/// <para>
/// A write served by another replica does not reach this signal, so the worker polls as well.
/// </para>
/// </summary>
public sealed class StateHistorySignal : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    public void Notify()
    {
        try
        {
            _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // already signalled, and one wake-up drains every entry
        }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _semaphore.WaitAsync(timeout, cancellationToken);

    public void Dispose() => _semaphore.Dispose();
}
