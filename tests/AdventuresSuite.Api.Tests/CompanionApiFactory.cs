using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AdventuresSuite.Api.Tests;

/// <summary>Composes the explicitly enabled deterministic Test host.</summary>
public sealed class CompanionApiFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("Companion:DeterministicMode", "true");
        builder.UseSetting("Companion:ActivationMode", "Disabled");
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
        builder.UseSetting("Companion:ActivationMode", "Disabled");
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
        builder.UseSetting("Companion:DeterministicMode", "true");
        builder.UseSetting("Companion:ActivationMode", "Disabled");
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
        builder.UseSetting("Deployment:CommitSha", "4444444444444444444444444444444444444444");
    }
}

/// <summary>Attempts to start Production without an immutable release identity.</summary>
public sealed class MissingReleaseShaCompanionApiFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("Companion:ActivationMode", "Disabled");
    }
}
