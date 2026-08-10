using AdventuresSuite.Companion.SqlServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AdventuresSuite.Api.Tests;

/// <summary>Verifies SQL readiness fails closed without leaking provider details.</summary>
public sealed class CompanionReadinessTests
{
    /// <summary>Converts every bounded provider failure into the same safe 503 contract.</summary>
    [Theory]
    [InlineData(CompanionSqlReadinessFailureCategory.Connection)]
    [InlineData(CompanionSqlReadinessFailureCategory.Authentication)]
    [InlineData(CompanionSqlReadinessFailureCategory.Timeout)]
    [InlineData(CompanionSqlReadinessFailureCategory.Probe)]
    public async Task SqlFailure_ReturnsGenericUnhealthy(
        CompanionSqlReadinessFailureCategory category)
    {
        await using var factory = new ReadinessFactory(
            new ThrowingReadinessProbe(category));
        using var response = await factory.CreateClient().GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("\"status\":\"Unhealthy\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain(category.ToString(), body, StringComparison.Ordinal);
        Assert.DoesNotContain("database", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A missing SQL provider is unavailable rather than implicitly healthy.</summary>
    [Fact]
    public async Task MissingSqlProbe_ReturnsGenericUnhealthy()
    {
        await using var factory = new ReadinessFactory(null);
        using var response = await factory.CreateClient().GetAsync("/health/ready");
        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    /// <summary>Request cancellation remains cancellation and is never sanitized as provider failure.</summary>
    [Fact]
    public async Task Probe_PreservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var probe = new CancelingReadinessProbe();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            probe.IsReadyAsync(cancellation.Token));
    }
}

internal sealed class ReadinessFactory(ICompanionSqlReadinessProbe? probe) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("Companion:DeterministicMode", "true");
        builder.UseSetting("Companion:ActivationMode", "Disabled");
        builder.UseSetting("Companion:ProjectionProvider", "Sql");
        builder.UseSetting("Deployment:CommitSha", "7777777777777777777777777777777777777777");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICompanionSqlReadinessProbe>();
            if (probe is not null) services.AddSingleton(probe);
        });
    }
}

internal sealed class ThrowingReadinessProbe(CompanionSqlReadinessFailureCategory category)
    : ICompanionSqlReadinessProbe
{
    public Task<bool> IsReadyAsync(CancellationToken cancellationToken = default) =>
        throw new CompanionSqlReadinessException(category);
}

internal sealed class CancelingReadinessProbe : ICompanionSqlReadinessProbe
{
    public Task<bool> IsReadyAsync(CancellationToken cancellationToken = default) =>
        Task.FromCanceled<bool>(cancellationToken);
}
