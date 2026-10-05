using Microsoft.AspNetCore.Hosting;
using Withywoods.AspNetCore.Mvc.Testing;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;

/// <summary>
/// Kestrel-hosted instance for the scenario tests, which drive the real <c>terraform</c> CLI over a socket and therefore cannot use the in-memory host.
/// <para>
/// It carries the same configuration as <see cref="TestWebApplicationFactory"/>, because a scenario run writes a real Terraform state and has exactly as much business in the maintainer's database as any other test.
/// </para>
/// </summary>
public class TestKestrelWebAppFactory : KestrelWebAppFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        TestHostConfiguration.Apply(builder);
        base.ConfigureWebHost(builder);
    }
}
