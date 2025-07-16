using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Devpro.TerraformBackend.Domain.Models;

public class UserModel
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string Username { get; set; } = null!;

    [BsonElement("password_hash")]
    public string PasswordHash { get; set; } = null!;

    public string Tenant { get; set; } = null!;
}
