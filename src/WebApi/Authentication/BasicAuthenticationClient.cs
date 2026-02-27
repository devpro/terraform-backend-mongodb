using System.Security.Principal;

namespace Devpro.TerraformBackend.WebApi.Authentication;

public class BasicAuthenticationClient : IIdentity
{
    public const string AuthenticationScheme = "Basic";

    public string? AuthenticationType { get; init; }

    public bool IsAuthenticated { get; init; }

    public string? Name { get; init; }
}
