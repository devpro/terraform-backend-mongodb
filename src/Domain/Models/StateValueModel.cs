using MongoDB.Bson.Serialization.Attributes;

namespace Farseer.TerraformBackend.Domain.Models;

public class StateValueModel
{
    public int Version { get; set; } = 0;

    [BsonElement("terraform_version")]
    public string TerraformVersion { get; set; } = string.Empty;

    public int Serial { get; set; } = 0;

    public string Lineage { get; set; } = string.Empty;

    public object Outputs { get; set; } = new { };

    public object[] Resources { get; set; } = [];
}
