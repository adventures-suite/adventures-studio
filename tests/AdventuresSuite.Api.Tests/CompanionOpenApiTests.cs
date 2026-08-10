using System.Net;
using System.Text.Json;

namespace AdventuresSuite.Api.Tests;

/// <summary>Verifies the generated OpenAPI and interactive-documentation gates.</summary>
public sealed class CompanionOpenApiTests(CompanionApiFactory factory)
    : IClassFixture<CompanionApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>Ensures OpenAPI 3.1 contains only the first fully documented read operation.</summary>
    [Fact]
    public async Task OpenApiContainsCompleteV1OperationMetadata()
    {
        using var response = await _client.GetAsync("/openapi/companion-v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.StartsWith("3.1", document.RootElement.GetProperty("openapi").GetString(), StringComparison.Ordinal);
        Assert.True(document.RootElement.GetProperty("components").GetProperty("securitySchemes")
            .TryGetProperty("companionOAuth", out var security));
        Assert.Equal("oauth2", security.GetProperty("type").GetString());

        var paths = document.RootElement.GetProperty("paths");
        var path = Assert.Single(paths.EnumerateObject());
        Assert.Equal("/v1/companion/adventures", path.Name);
        var operation = path.Value.GetProperty("get");
        Assert.Equal("ListCompanionAdventures", operation.GetProperty("operationId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("description").GetString()));
        var responses = operation.GetProperty("responses");
        foreach (var status in new[] { "200", "304", "400", "401", "403", "404", "500" })
            Assert.True(responses.TryGetProperty(status, out _), $"Missing response {status}.");
        var parameters = operation.GetProperty("parameters").EnumerateArray().ToArray();
        Assert.Equal(
            new HashSet<string>(["limit", "continuationToken", "includeCompleted"], StringComparer.Ordinal),
            parameters.Select(value => value.GetProperty("name").GetString()!).ToHashSet(StringComparer.Ordinal));
        Assert.All(parameters, parameter =>
            Assert.False(string.IsNullOrWhiteSpace(parameter.GetProperty("description").GetString())));
    }

    /// <summary>Ensures Scalar consumes the generated contract in Test.</summary>
    [Fact]
    public async Task ScalarIsAvailableInTest()
    {
        using var response = await _client.GetAsync("/scalar/companion");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("AdventuresCompanion API v1", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>Ensures the independent host exposes safe liveness and readiness probes.</summary>
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthProbesAreHealthyAndExcludedFromOpenApi(string route)
    {
        using var response = await _client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());

        using var openApiResponse = await _client.GetAsync("/openapi/companion-v1.json");
        using var document = JsonDocument.Parse(await openApiResponse.Content.ReadAsStringAsync());
        Assert.False(document.RootElement.GetProperty("paths").TryGetProperty(route, out _));
    }
}

/// <summary>Verifies fail-closed Production composition.</summary>
public sealed class CompanionProductionGateTests(ProductionCompanionApiFactory factory)
    : IClassFixture<ProductionCompanionApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>Ensures deterministic data and developer documentation cannot activate in Production.</summary>
    [Fact]
    public async Task ProductionKeepsFoundationActivationClosed()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/openapi/companion-v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/scalar/companion")).StatusCode);
        var response = await _client.GetAsync("/v1/companion/adventures");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Ensures a production attempt to select deterministic identities and fixtures fails startup.</summary>
    [Fact]
    public void ProductionRejectsDeterministicAdapterConfiguration()
    {
        using var invalidFactory = new InvalidProductionCompanionApiFactory();
        var error = Assert.Throws<InvalidOperationException>(() => invalidFactory.CreateClient());
        Assert.Contains("deterministic Companion adapter", error.ToString(), StringComparison.Ordinal);
    }
}
