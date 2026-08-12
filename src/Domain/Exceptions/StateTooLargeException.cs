using System;

namespace Devpro.TerraformBackend.Domain.Exceptions;

/// <summary>
/// A state that cannot be stored because it exceeds the MongoDB document limit.
/// <para>
/// The limit is a permanent property of the design rather than a defect. The state is stored as a queryable
/// BSON document, which is the premise of the project and a contract other applications read, so every way
/// around the limit gives up the thing that makes the data worth storing. The work is therefore to fail
/// cleanly at the ceiling, not to escape it.
/// </para>
/// <para>
/// Failing cleanly matters more here than the message suggests: the write happens during a
/// <c>terraform apply</c>, after the lock has been taken and after the real infrastructure has already
/// changed, which is the worst moment to lose a state write to an unexplained 500.
/// </para>
/// </summary>
public class StateTooLargeException(long maximumSizeInBytes, Exception innerException)
    : Exception(
        $"The state exceeds the maximum document size of {maximumSizeInBytes} bytes "
        + $"({maximumSizeInBytes / (1024 * 1024)} MB), which is the limit of the underlying storage.",
        innerException)
{
    public long MaximumSizeInBytes { get; } = maximumSizeInBytes;
}
