using Farseer.Common.MongoDb;

namespace Farseer.TerraformBackend.WebApi;

public class ApplicationConfiguration(IConfigurationRoot configurationRoot)
    : WebApiConfiguration(configurationRoot)
{
    public MongoDbConfiguration MongoDbConfiguration =>
        new()
        {
            ConnectionString = ConfigurationRoot.GetConnectionString(TryGetSection("MongoDb:ConnectionStringName")?.Get<string>() ?? string.Empty) ?? string.Empty,
            DatabaseName = TryGetSection("MongoDb:DatabaseName").Get<string>() ?? string.Empty
        };
}
