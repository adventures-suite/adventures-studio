using System.Net;
using System.Text.Json;
using AdventuresSuite.Companion.Contracts;

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
        Assert.Equal(
            new[] { "/v1/companion/adventures", "/v1/companion/adventures/{adventureId}" },
            paths.EnumerateObject().Select(value => value.Name).Order(StringComparer.Ordinal));
        var operation = paths.GetProperty("/v1/companion/adventures").GetProperty("get");
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

        var detail = paths.GetProperty("/v1/companion/adventures/{adventureId}").GetProperty("get");
        Assert.Equal("GetCompanionAdventure", detail.GetProperty("operationId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(detail.GetProperty("summary").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(detail.GetProperty("description").GetString()));
        var detailResponses = detail.GetProperty("responses");
        foreach (var status in new[] { "200", "304", "400", "401", "403", "404", "500" })
            Assert.True(detailResponses.TryGetProperty(status, out _), $"Missing detail response {status}.");
        var detailParameter = Assert.Single(detail.GetProperty("parameters").EnumerateArray());
        Assert.Equal("adventureId", detailParameter.GetProperty("name").GetString());
        Assert.Equal("path", detailParameter.GetProperty("in").GetString());
        Assert.True(detailParameter.GetProperty("required").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(detailParameter.GetProperty("description").GetString()));
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
        var health = JsonSerializer.Deserialize(
            await response.Content.ReadAsStringAsync(),
            CompanionJsonSerializerContext.Default.CompanionHealthDto);
        Assert.NotNull(health);
        Assert.Equal("Healthy", health.Status);
        Assert.Equal("AdventuresSuite.Api", health.Service);
        Assert.Equal("Disabled", health.ActivationState);
        Assert.Equal("1111111111111111111111111111111111111111", health.ReleaseSha);

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
        var detail = await _client.GetAsync("/v1/companion/adventures/adv_demo_italy_2026");
        Assert.Equal(HttpStatusCode.Unauthorized, detail.StatusCode);
    }

    /// <summary>Ensures a production attempt to select deterministic identities and fixtures fails startup.</summary>
    [Fact]
    public void ProductionRejectsDeterministicAdapterConfiguration()
    {
        using var invalidFactory = new InvalidProductionCompanionApiFactory();
        var error = Assert.Throws<InvalidOperationException>(() => invalidFactory.CreateClient());
        Assert.Contains("deterministic Companion adapter", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Ensures Production cannot infer the disabled activation gate from a default.</summary>
    [Fact]
    public void ProductionRequiresExplicitDisabledActivationMode()
    {
        using var invalidFactory = new MissingActivationModeCompanionApiFactory();
        var error = Assert.Throws<InvalidOperationException>(() => invalidFactory.CreateClient());
        Assert.Contains("Companion:ActivationMode", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Ensures Production health cannot report an ambiguous or mutable release identity.</summary>
    [Fact]
    public void ProductionRequiresExactReleaseSha()
    {
        using var invalidFactory = new MissingReleaseShaCompanionApiFactory();
        var error = Assert.Throws<InvalidOperationException>(() => invalidFactory.CreateClient());
        Assert.Contains("Deployment:CommitSha", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Ensures Production never infers an authoritative provider.</summary>
    [Fact]
    public void ProductionRequiresExplicitProjectionProvider()
    {
        using var invalidFactory = new MissingProjectionProviderCompanionApiFactory();
        var error = Assert.Throws<InvalidOperationException>(() => invalidFactory.CreateClient());
        Assert.Contains("Companion:ProjectionProvider", error.ToString(), StringComparison.Ordinal);
    }
}
