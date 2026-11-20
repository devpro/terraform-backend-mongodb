using System;
using System.Threading.Tasks;
using Devpro.TerraformBackend.Domain.Models;
using Devpro.TerraformBackend.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
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
    /// Reads the open counter and the user in one aggregation, the user joined with <c>$unionWith</c>.
    /// <para>
    /// Two separate reads would add a database request to every cached authentication, which is every Terraform request after the first.
    /// The user comes back wrapped in a <c>user</c> field, so that it cannot be confused with the counter.
    /// </para>
    /// </summary>
    public async Task<(int Failures, UserModel? User)> GetFailureCountAndUserAsync(string username, string remoteAddress)
    {
        var now = DateTime.UtcNow;

        // raw BSON stages reference the stored field names: remote_address and expires_at, not the BsonElement-mapped properties
        var pipeline = new BsonDocument[]
        {
            new("$match", new BsonDocument
            {
                ["username"] = username,
                ["remote_address"] = remoteAddress,
                ["expires_at"] = new BsonDocument("$gt", now)
            }),
            new("$limit", 1),
            new("$project", new BsonDocument { ["_id"] = 0, ["failures"] = 1 }),
            new("$unionWith", new BsonDocument
            {
                ["coll"] = UserRepository.Collection,
                ["pipeline"] = new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument("username", username)),
                    new BsonDocument("$limit", 1),
                    new BsonDocument("$replaceWith", new BsonDocument("user", "$$ROOT"))
                }
            })
        };

        var documents = await GetCollection<BsonDocument>()
            .Aggregate(PipelineDefinition<BsonDocument, BsonDocument>.Create(pipeline))
            .ToListAsync();

        var failures = 0;
        UserModel? user = null;
        foreach (var document in documents)
        {
            if (document.TryGetValue("user", out var stored))
            {
                user = BsonSerializer.Deserialize<UserModel>(stored.AsBsonDocument);
            }
            else
            {
                failures = document["failures"].ToInt32();
            }
        }

        return (failures, user);
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
