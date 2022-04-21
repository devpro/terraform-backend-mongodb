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
    }
}
