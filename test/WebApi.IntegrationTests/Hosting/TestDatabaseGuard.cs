using System;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;

/// <summary>
/// Fail-fast check that a suite hosting the real <c>WebApi</c> in-process is pointed at a dedicated test database rather than at real data.
/// <para>
/// This exists because the failure it prevents is silent and destructive.
/// The in-process host runs as <c>Development</c>, so when the database name is not pushed into its configuration the host falls straight back to <c>src/WebApi/appsettings.Development.json</c>, that is <c>tfbackend_dev</c>.
/// Nothing errors: the suite simply creates, updates and deletes documents in the database the maintainer works in, and the <c>admin</c> account it authenticates as is shared with that real work.
/// When that account drifts to another tenant, every authenticating test fails with a bare <c>401</c>.
/// </para>
/// </summary>
public static class TestDatabaseGuard
{
    /// <summary>
    /// Substrings that mark a database as a real, non-throwaway one.
    /// Deliberately a denylist of "this is somebody's data" markers rather than an allowlist of blessed names: a new suite pointing at <c>tfbackend_something_new</c> should just work, while <c>tfbackend_dev</c> must never be the accidental default.
    /// </summary>
    private static readonly string[] ProtectedDatabaseMarkers = ["dev", "prod", "staging", "preprod"];

    /// <summary>
    /// Throws unless the name the run will actually use looks like a throwaway test database.
    /// Call this before the host is built, so the run stops with an actionable message instead of writing to the wrong database.
    /// </summary>
    public static void EnsureTestDatabaseName(string? databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException(
                "No test database name was resolved, so this run would fall back to the host's own " +
                $"appsettings.Development.json database (tfbackend_dev) and write to real data. Leave " +
                $"{IntegrationTestDatabase.DatabaseNameVariable} unset to use the default " +
                $"({IntegrationTestDatabase.DefaultName}), or set it to another dedicated database.");
        }

        foreach (var marker in ProtectedDatabaseMarkers)
        {
            if (databaseName.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{IntegrationTestDatabase.DatabaseNameVariable} is set to '{databaseName}', which looks " +
                    "like a real database rather than a throwaway one. This suite creates and deletes " +
                    $"documents, so point it at a dedicated database such as {IntegrationTestDatabase.DefaultName}.");
            }
        }
    }
}
