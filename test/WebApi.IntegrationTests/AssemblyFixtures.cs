using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Xunit;

// Seeds the suite's database once for the whole run and verifies on the way out that the run left it as it found it.
// Registered at assembly level so it is initialised before the first test class builds a host, and disposed after the last one has released its factory.
[assembly: AssemblyFixture(typeof(TestDatabaseFixture))]
