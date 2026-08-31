using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Devpro.TerraformBackend.Domain.Models;

/// <summary>
/// A consecutive-failure counter for one username and source address pair.
/// Stored in <c>auth_lockout</c> rather than in the process, so a lockout holds across every replica of the
/// deployment rather than resetting on the pod an attacker happens to land on.
/// </summary>
public class LockoutModel
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Overrides the global camelCase convention, matching <see cref="UserModel.PasswordHash"/>'s
    /// <c>password_hash</c> and the snake_case field names in the Terraform state stored in <c>tf_state</c>
    /// itself, rather than the camelCase envelope fields (<c>tenant</c>, <c>name</c>, <c>createdAt</c>) around
    /// it, which are this app's own and stay as they are.
    /// </summary>
    [BsonElement("remote_address")]
    public string RemoteAddress { get; set; } = string.Empty;

    public int Failures { get; set; }

    /// <summary>
    /// When this counter stops applying.
    /// Set once, when the first failure of a window is recorded, and never extended by a later failure in the
    /// same window: the window is a fixed duration from the first failure, not a sliding one.
    /// A TTL index on this field is what actually deletes the document; a query still filters on it directly,
    /// since the TTL monitor only sweeps roughly once a minute and must not be the only thing standing between
    /// an expired window and a caller reading it as still active.
    /// </summary>
    [BsonElement("expires_at")]
    public DateTime ExpiresAt { get; set; }
}
