using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi;
using Devpro.TerraformBackend.WebApi.Authentication;
using Devpro.TerraformBackend.WebApi.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.UnitTests.DependencyInjection;

/// <summary>
/// A threshold that cannot lock anything out should fail at startup rather than as a runtime surprise.
/// <para>
/// <c>Authentication:MaxFailedAttempts</c> below 1 refuses a pair on its first attempt, and
/// <c>Authentication:LockoutSeconds</c> at or below zero expires a lockout before it withholds anything: both
/// silently defeat the brute-force protection this method exists to configure.
/// </para>
/// </summary>
[Trait("Category", "UnitTests")]
public class AuthenticationServiceCollectionExtensionsTest
{
    [Fact]
    public void AddCredentialAuthentication_WithZeroMaxFailedAttempts_Throws()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:MaxFailedAttempts"] = "0"
        });
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddCredentialAuthentication(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*MaxFailedAttempts*");
    }

    [Fact]
    public void AddCredentialAuthentication_WithZeroLockoutSeconds_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:LockoutSeconds"] = "0"
        });
        var services = new ServiceCollection();

        var act = () => services.AddCredentialAuthentication(configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage("*LockoutSeconds*");
    }

    [Fact]
    public void AddCredentialAuthentication_WithDefaultConfiguration_RegistersTheAuthenticator()
    {
        // Arrange: no overrides, so ApplicationConfiguration falls back to its documented defaults, which must
        // pass validation on their own
        var configuration = BuildConfiguration([]);
        var services = new ServiceCollection();

        // Act
        services.AddCredentialAuthentication(configuration);

        // Assert
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(ICredentialAuthenticator));
    }

    [Theory]
    [InlineData("Network:KnownProxies:0", "10.0.0.300", "*KnownProxies*10.0.0.300*")]
    [InlineData("Network:KnownNetworks:0", "10.42.0.0/99", "*KnownNetworks*10.42.0.0/99*")]
    public void AddTrustedProxies_WithUnparseableEntry_Throws(string key, string value, string expectedMessage)
    {
        // Arrange: a skipped entry would clear the loopback default and trust nothing, with no sign of it
        var configuration = BuildConfiguration(new Dictionary<string, string?> { [key] = value });
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddTrustedProxies(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage(expectedMessage);
    }

    private static ApplicationConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        var configurationRoot = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new ApplicationConfiguration(configurationRoot);
    }
}
