using System;

namespace Devpro.TerraformBackend.Domain.Exceptions;

/// <summary>
/// A state that cannot be stored because it exceeds the MongoDB document limit.
/// <para>
/// The limit is permanent: the state is stored as a queryable BSON document that other applications read, and every way around the limit makes it opaque.
/// The message names the limit because the write happens during a <c>terraform apply</c>, after the infrastructure has changed, where an unexplained 500 is the worst possible answer.
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
