using Kalosyni.TerraformBackend.Infrastructure.MongoDb;

namespace Kalosyni.TerraformBackend.WebApi
{
    public class ApplicationConfiguration
    {
        private readonly IConfigurationRoot _configurationRoot;

        public ApplicationConfiguration(IConfigurationRoot configurationRoot)
        {
            _configurationRoot = configurationRoot;
        }

        public bool IsSwaggerEnabled => _configurationRoot.GetSection("Application:IsSwaggerEnabled").Get<bool>();

        public MongoDbConfiguration MongoDbConfiguration => new()
        {
            ConnectionString = _configurationRoot.GetConnectionString(_configurationRoot.GetSection("MongoDb:ConnectionStringName").Get<string>()),
            DatabaseName = _configurationRoot.GetSection("MongoDb:DatabaseName").Get<string>()
        };
    }
}
