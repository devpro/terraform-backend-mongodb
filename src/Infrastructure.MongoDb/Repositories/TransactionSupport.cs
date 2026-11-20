using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories;

/// <summary>
/// Tells whether the server runs transactions, which a replica set and a sharded cluster do and a standalone server refuses.
/// <para>
/// Asked once, from the server itself, rather than read from configuration that could disagree with it.
/// A failed check is not remembered, nor the answer of a replica set member not yet initiated, so such a server is asked again at the next write.
/// </para>
/// </summary>
public sealed class TransactionSupport(IMongoClient client)
{
    private volatile StrongBox? _isSupported;

    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken)
    {
        if (_isSupported is { } known) return known.Value;

        var hello = await client.GetDatabase("admin")
            .RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: cancellationToken);

        // a replica set member not yet initiated reports no setName, which must not be remembered as a standalone server
        if (hello.TryGetValue("isreplicaset", out var isReplicaSet) && isReplicaSet.ToBoolean()) return false;

        var isSupported = hello.Contains("setName")
            || (hello.TryGetValue("msg", out var message) && message == "isdbgrid");

        _isSupported = new StrongBox(isSupported);
        return isSupported;
    }

    private sealed record StrongBox(bool Value);
}
