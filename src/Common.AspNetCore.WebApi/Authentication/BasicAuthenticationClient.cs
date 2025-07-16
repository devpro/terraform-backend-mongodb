using System.Security.Principal;

namespace Devpro.Common.AspNetCore.WebApi.Authentication;

public class BasicAuthenticationClient : IIdentity
{
    public string? AuthenticationType { get; set; }

    public bool IsAuthenticated { get; set; }

    public string? Name { get; set; }
}
