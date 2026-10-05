using System;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;

/// <summary>
/// The MongoDB database this suite runs against: <c>DatabaseSettings__DatabaseName</c> when it is set, and <see cref="DefaultName"/> otherwise.
/// <para>
/// Defaulted rather than required.
/// An IDE sets test environment variables once for the whole solution, so a variable that every suite reads can only ever give them all the same database, and refusing to run without it turns one misconfiguration into a suite-wide failure.
/// Defaulting picks something safe instead.
/// </para>
/// <para>
/// This does not weaken <see cref="TestDatabaseGuard"/>.
/// What the guard exists to prevent is the *silent* fallback to the host's own <c>appsettings.Development.json</c>, that is <c>tfbackend_dev</c>, the database the maintainer browses and works in.
/// The name resolved here is pushed into the host's configuration by <see cref="TestHostConfiguration"/> precisely so that fallback cannot happen, and an explicitly configured <c>dev</c> or <c>prod</c> name still fails fast.
/// </para>
/// </summary>
public static class IntegrationTestDatabase
{
    public const string DefaultName = "tfbackend_integrationtests";

    public const string ConnectionStringVariable = "DatabaseSettings__ConnectionString";

    public const string DatabaseNameVariable = "DatabaseSettings__DatabaseName";

    private const string DefaultConnectionString = "mongodb://localhost:27017";

    public static string Name =>
        Environment.GetEnvironmentVariable(DatabaseNameVariable) is { Length: > 0 } value ? value : DefaultName;

    /// <summary>
    /// The connection string the fixture uses to seed and verify directly, outside any host.
    /// Kept in step with what the host itself resolves, so both reach the same server.
    /// </summary>
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionStringVariable) is { Length: > 0 } value ? value : DefaultConnectionString;
}
