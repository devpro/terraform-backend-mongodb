namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;

/// <summary>
/// The account every suite authenticates as, seeded and removed by <see cref="TestDatabaseFixture"/>.
/// <para>
/// The tests and the Terraform environment of the scenario host read these values from here, and the run creates the account in its own database and removes it afterwards, so there is nothing to repeat and nothing to drift.
/// </para>
/// </summary>
public static class TestCredentials
{
    public const string Username = "admin";

    public const string Password = "admin123";

    public const string Tenant = "dummy";

    /// <summary>
    /// A second, fully valid account on a different tenant.
    /// <para>
    /// It exists so that tenant isolation can be asserted against a caller who authenticates perfectly well and still must not reach the first tenant's state.
    /// Testing isolation with one account only proves that a route naming an unclaimed tenant is refused, which is the easy half.
    /// </para>
    /// </summary>
    public const string OtherUsername = "other-admin";

    public const string OtherPassword = "other123";

    public const string OtherTenant = "acme";
}
