using System;
using System.Threading.Tasks;
using Devpro.TerraformBackend.Domain.Models;
using Devpro.TerraformBackend.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.Repositories;

public class LockoutRepository : RepositoryBase, ILockoutRepository
{
    private readonly IMongoCollection<LockoutModel> _modelCollection;

    public LockoutRepository(IMongoDatabase mongoDatabase, ILogger<LockoutRepository> logger)
        : base(mongoDatabase, logger)
    {
        _modelCollection = GetCollection<LockoutModel>();
    }

    protected override string CollectionName => "auth_lockout";

    public async Task<int> GetFailureCountAsync(string username, string remoteAddress)
    {
        var now = DateTime.UtcNow;
        var counter = await _modelCollection
            .Find(x => x.Username == username && x.RemoteAddress == remoteAddress && x.ExpiresAt > now)
            .FirstOrDefaultAsync();
        return counter?.Failures ?? 0;
    }

    /// <summary>
    /// Increments or starts the counter in a single aggregation-pipeline update, evaluated server-side against one document under MongoDB's own document-level atomicity.
    /// <para>
    /// A read-then-write, or even a conditional update followed by a separate upsert, has a window between the two steps:
    /// under real concurrency, several callers can each observe "no open window" and each start their own, so all but one increment is lost.
    /// A brute force is exactly the concurrent case where that undercounts, which is why this is one round trip rather than two.
    /// </para>
    /// </summary>
    public async Task<int> RecordFailureAsync(string username, string remoteAddress, TimeSpan lockoutDuration)
    {
        var now = DateTime.UtcNow;
        var expiry = now + lockoutDuration;

        // this pipeline stage is raw BSON rather than a typed builder expression,
        // so it references the actual stored field names directly: remote_address and expires_at, not the BsonElement-mapped properties
        var stillOpen = new BsonDocument("$cond", new BsonDocument
        {
            ["if"] = new BsonDocument("$gt", new BsonArray { "$expires_at", now }),
            ["then"] = new BsonDocument("$add", new BsonArray { "$failures", 1 }),
            ["else"] = 1
        });
        var keepOrRenewExpiry = new BsonDocument("$cond", new BsonDocument
        {
            ["if"] = new BsonDocument("$gt", new BsonArray { "$expires_at", now }),
            ["then"] = "$expires_at",
            ["else"] = expiry
        });

        var pipeline = new EmptyPipelineDefinition<LockoutModel>().AppendStage<LockoutModel, LockoutModel, LockoutModel>(
            new BsonDocument("$set", new BsonDocument
            {
                ["username"] = username,
                ["remote_address"] = remoteAddress,
                ["failures"] = stillOpen,
                ["expires_at"] = keepOrRenewExpiry
            }));

        var updated = await _modelCollection.FindOneAndUpdateAsync(
            x => x.Username == username && x.RemoteAddress == remoteAddress,
            pipeline,
            new FindOneAndUpdateOptions<LockoutModel, LockoutModel> { IsUpsert = true, ReturnDocument = ReturnDocument.After });

        return updated.Failures;
    }

    public async Task ClearAsync(string username, string remoteAddress) =>
        await _modelCollection.DeleteOneAsync(x => x.Username == username && x.RemoteAddress == remoteAddress);
}
