using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Devpro.TerraformBackend.Domain.Models;

/// <summary>
/// A consecutive-failure counter for one username and source address pair.
/// Stored in <c>auth_lockout</c> rather than in the process,
/// so a lockout holds across every replica of the deployment rather than resetting on the pod an attacker happens to land on.
/// </summary>
public class LockoutModel
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Overrides the global camelCase convention, so that a multi-word field reads like <c>user.password_hash</c> and <c>tf_state.created_at</c>.
    /// </summary>
    [BsonElement("remote_address")]
    public string RemoteAddress { get; set; } = string.Empty;

    public int Failures { get; set; }

    /// <summary>
    /// When this counter stops applying.
    /// Set by the first failure of a window and never extended, so the window is fixed rather than sliding.
    /// A TTL index on this field deletes the document, and a query still filters on it, because the TTL monitor sweeps only about once a minute.
    /// </summary>
    [BsonElement("expires_at")]
    public DateTime ExpiresAt { get; set; }
}
