namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;

/// <summary>
/// The account every suite authenticates as, seeded and removed by <see cref="TestDatabaseFixture"/>.
/// <para>
/// These values used to be repeated in the test base, in two test classes and in the Terraform environment of
/// the scenario host, while the account itself was created by hand with <c>tfbeadm</c>. Owning the account
/// here is what ends that: the run creates what it needs in its own database and removes it afterwards, so
/// there is nothing left to drift.
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
    /// It exists so that tenant isolation can be asserted against a caller who authenticates perfectly well
    /// and still must not reach the first tenant's state. Testing isolation with one account only proves that
    /// a route naming an unclaimed tenant is refused, which is the half that was already covered.
    /// </para>
    /// </summary>
    public const string OtherUsername = "other-admin";

    public const string OtherPassword = "other123";

    public const string OtherTenant = "acme";
}
