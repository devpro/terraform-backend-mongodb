using Microsoft.AspNetCore.Hosting;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;

/// <summary>
/// The configuration every test host in this suite applies, whether it is served in-memory or over Kestrel.
/// <para>
/// Pushing the database name into the host is the load-bearing part: resolving a name and not supplying it leaves the host free to fall back to <c>appsettings.Development.json</c>, which is the exact failure <see cref="TestDatabaseGuard"/> exists to prevent.
/// The guard runs here, against the name that is about to be pushed, so it always vouches for the name the run will really use.
/// </para>
/// <para>
/// <c>UseSetting</c> also carries the Scalar feature flag, rather than <c>Environment.SetEnvironmentVariable</c>, which is process-wide.
/// A variable that is never reset leaks into every later test in the process and makes the suite order-dependent.
/// </para>
/// </summary>
internal static class TestHostConfiguration
{
    public static void Apply(IWebHostBuilder builder)
    {
        var databaseName = IntegrationTestDatabase.Name;
        TestDatabaseGuard.EnsureTestDatabaseName(databaseName);

        builder.UseSetting("DatabaseSettings:DatabaseName", databaseName);
        builder.UseSetting("Features:IsScalarEnabled", "true");
    }
}
