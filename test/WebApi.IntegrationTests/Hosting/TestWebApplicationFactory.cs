using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;

/// <summary>
/// In-memory host for the resource and repository tests, pointed at the suite's own database.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        TestHostConfiguration.Apply(builder);
        base.ConfigureWebHost(builder);
    }
}
