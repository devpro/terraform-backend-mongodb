using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Devpro.TerraformBackend.Domain.Models;

public class StateLockModel
{
    /// <summary>
    /// Terraform state lock ID.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    [JsonPropertyName("ID")]
    public string Id { get; set; } = null!;

    /// <summary>
    /// 
    /// </summary>
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
