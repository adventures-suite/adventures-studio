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
    }
}

/// <summary>Composes the fail-closed Production host.</summary>
public sealed class ProductionCompanionApiFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Production");
}

/// <summary>Attempts the forbidden deterministic adapter selection in Production.</summary>
public sealed class InvalidProductionCompanionApiFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("Companion:DeterministicMode", "true");
    }
}
