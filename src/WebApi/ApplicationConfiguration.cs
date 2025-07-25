using Devpro.Common.MongoDb;

namespace Devpro.TerraformBackend.WebApi;

public class ApplicationConfiguration(IConfigurationRoot configurationRoot)
    : WebApiConfiguration(configurationRoot)
{
    public MongoDbConfiguration MongoDbConfiguration =>
        new()
        {
            ConnectionString = ConfigurationRoot.GetConnectionString(TryGetSection<string>("MongoDb:ConnectionStringName")) ?? string.Empty,
            DatabaseName = TryGetSection<string>("MongoDb:DatabaseName")
        };
}
