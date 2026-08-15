using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AdventuresSuite.Api.Tests;

/// <summary>Composes the explicitly enabled deterministic Test host.</summary>
public sealed class CompanionApiFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("Companion:DeterministicMode", "true");
        builder.UseSetting("Authentication:CompanionApi:Mode", "Closed");
        builder.UseSetting("Companion:ActivationMode", "Disabled");
        builder.UseSetting("Companion:ProjectionProvider", "Closed");
        builder.UseSetting("Deployment:CommitSha", "1111111111111111111111111111111111111111");
    }
}

/// <summary>Composes the fail-closed Production host.</summary>
public sealed class ProductionCompanionApiFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("Authentication:CompanionApi:Mode", "Closed");
        builder.UseSetting("Companion:ActivationMode", "Disabled");
        builder.UseSetting("Companion:ProjectionProvider", "Closed");
        builder.UseSetting("Deployment:CommitSha", "2222222222222222222222222222222222222222");
    }
}

/// <summary>Attempts the forbidden deterministic adapter selection in Production.</summary>
public sealed class InvalidProductionCompanionApiFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("Authentication:CompanionApi:Mode", "Closed");
        builder.UseSetting("Companion:DeterministicMode", "true");
        builder.UseSetting("Companion:ActivationMode", "Disabled");
        builder.UseSetting("Companion:ProjectionProvider", "Closed");
        builder.UseSetting("Deployment:CommitSha", "3333333333333333333333333333333333333333");
    }
}

/// <summary>Attempts to start Production without an explicit product activation mode.</summary>
public sealed class MissingActivationModeCompanionApiFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("Authentication:CompanionApi:Mode", "Closed");
        builder.UseSetting("Deployment:CommitSha", "4444444444444444444444444444444444444444");
        builder.UseSetting("Companion:ProjectionProvider", "Closed");
    }
}

/// <summary>Attempts to start Production without an immutable release identity.</summary>
public sealed class MissingReleaseShaCompanionApiFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("Authentication:CompanionApi:Mode", "Closed");
        builder.UseSetting("Companion:ActivationMode", "Disabled");
        builder.UseSetting("Companion:ProjectionProvider", "Closed");
    }
}

/// <summary>Attempts to start Production without an explicit projection provider.</summary>
public sealed class MissingProjectionProviderCompanionApiFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("Authentication:CompanionApi:Mode", "Closed");
        builder.UseSetting("Companion:ActivationMode", "Disabled");
        builder.UseSetting("Deployment:CommitSha", "6666666666666666666666666666666666666666");
    }
}

/// <summary>Composes real bearer transport validation with projections deliberately closed.</summary>
public sealed class BearerCompanionApiFactory(SecurityKey signingKey) : WebApplicationFactory<Program>
{
    /// <summary>Gets the exact fictional issuer used by transport tests.</summary>
    public const string Issuer = "https://identity.example.test/companion/v2.0";
    /// <summary>Gets the exact fictional audience used by transport tests.</summary>
    public const string Audience = "api://companion-test";

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Authentication:CompanionApi:Mode", "Bearer");
        builder.UseSetting("Authentication:CompanionApi:Issuer", Issuer);
        builder.UseSetting("Authentication:CompanionApi:Audience", Audience);
        builder.UseSetting("Companion:ActivationMode", "Disabled");
        builder.UseSetting("Companion:ProjectionProvider", "Closed");
        builder.UseSetting("Deployment:CommitSha", "7777777777777777777777777777777777777777");
        builder.ConfigureTestServices(services => services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            options =>
            {
                var metadata = new OpenIdConnectConfiguration { Issuer = Issuer };
                metadata.SigningKeys.Add(signingKey);
                options.ConfigurationManager =
                    new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
            }));
    }
}

/// <summary>Attempts to combine bearer transport validation with authoritative projections prematurely.</summary>
public sealed class InvalidBearerSqlCompanionApiFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Authentication:CompanionApi:Mode", "Bearer");
        builder.UseSetting("Authentication:CompanionApi:Issuer", BearerCompanionApiFactory.Issuer);
        builder.UseSetting("Authentication:CompanionApi:Audience", BearerCompanionApiFactory.Audience);
        builder.UseSetting("Companion:ActivationMode", "Disabled");
        builder.UseSetting("Companion:ProjectionProvider", "Sql");
        builder.UseSetting("Deployment:CommitSha", "8888888888888888888888888888888888888888");
    }
}

/// <summary>Attempts to activate deterministic composition outside the Test environment.</summary>
public sealed class InvalidDeterministicEnvironmentCompanionApiFactory(
    string environment, string authenticationMode, string projectionProvider)
    : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("Companion:DeterministicMode", "true");
        builder.UseSetting("Authentication:CompanionApi:Mode", authenticationMode);
        builder.UseSetting("Companion:ActivationMode", "Disabled");
        builder.UseSetting("Companion:ProjectionProvider", projectionProvider);
        builder.UseSetting("Deployment:CommitSha", "9999999999999999999999999999999999999999");
    }
}
