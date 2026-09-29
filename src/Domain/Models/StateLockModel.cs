using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace Devpro.TerraformBackend.Domain.Models;

/// <summary>
/// A Terraform state lock, one per tenant and state name.
/// <para>
/// The lock ID is a plain field and <c>_id</c> is left to MongoDB, because every request names its tenant and
/// state: a lock ID used as <c>_id</c> would have to be unique across every tenant and state, and a reused one
/// would fail to lock a state nothing else holds.
/// </para>
/// </summary>
[BsonNoId]
[BsonIgnoreExtraElements]
public class StateLockModel
{
    /// <summary>
    /// Terraform state lock ID.
    /// </summary>
    [BsonElement("lock_id")]
    [JsonPropertyName("ID")]
    public string Id { get; set; } = null!;

    public string Tenant { get; set; } = string.Empty;

    /// <summary>
    /// Name of the Terraform state.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Terraform operation.
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Terraform info.
    /// </summary>
    public string Info { get; set; } = string.Empty;

    /// <summary>
    /// Terraform state lock owner.
    /// </summary>
    public string Who { get; set; } = string.Empty;

    /// <summary>
    /// Terraform version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Terraform state lock timestamp.
    /// </summary>
    public string Created { get; set; } = string.Empty;

    /// <summary>
    /// Terraform path.
    /// </summary>
    public string Path { get; set; } = string.Empty;
}
