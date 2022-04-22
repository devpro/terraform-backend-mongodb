using System.Text.Json.Serialization;

namespace Kalosyni.TerraformBackend.Domain.Models
{
    public class StateLockModel
    {
        /// <summary>
        /// Terraform state lock ID.
        /// </summary>
        [JsonPropertyName("ID")]
        public string Id { get; set; } = string.Empty;

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
}
