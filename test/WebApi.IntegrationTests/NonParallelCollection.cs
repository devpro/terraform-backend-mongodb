using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests;

/// <summary>
/// Collection for tests that measure how long something takes, and therefore cannot share the machine with the rest of the suite.
/// <para>
/// The suite runs test classes in parallel, which is right for everything that asserts on a result and wrong for anything that asserts on a duration: the `tfbeadm` tests alone spawn `bash`, `htpasswd` and `mongosh`, and that load lands squarely on whatever is being timed.
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class NonParallelCollection
{
    public const string Name = "Non-parallel";
}
