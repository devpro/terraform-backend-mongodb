using Kalosyni.Common.Runtime;
using Kalosyni.TerraformBackend.Infrastructure.MongoDb;

namespace Kalosyni.TerraformBackend.WebApi
{
    public class ApplicationConfiguration : ConfigurationBase
    {
        public ApplicationConfiguration(IConfigurationRoot configurationRoot)
            : base(configurationRoot)
        {
        }

        public bool IsSwaggerEnabled => TryGetSection("Application:IsSwaggerEnabled").Get<bool>();

        public bool IsHttpsRedirectionEnabled => TryGetSection("Application:IsHttpsRedirectionEnabled").Get<bool>();

        public MongoDbConfiguration MongoDbConfiguration =>
            new()
            {
                ConnectionString = ConfigurationRoot.GetConnectionString(TryGetSection("MongoDb:ConnectionStringName").Get<string>()),
                DatabaseName = TryGetSection("MongoDb:DatabaseName").Get<string>()
            };
    }
}
