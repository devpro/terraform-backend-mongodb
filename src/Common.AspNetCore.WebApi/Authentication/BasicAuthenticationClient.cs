using System.Security.Principal;

namespace Devpro.Common.AspNetCore.WebApi.Authentication;

public class BasicAuthenticationClient : IIdentity
{
    public string? AuthenticationType { get; init; }

    public bool IsAuthenticated { get; init; }

    public string? Name { get; init; }
}
