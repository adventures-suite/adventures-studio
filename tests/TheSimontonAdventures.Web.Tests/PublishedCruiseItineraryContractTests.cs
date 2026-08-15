using System.Reflection;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Imports;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the dormant provider-neutral cruise itinerary boundary.</summary>
public sealed class PublishedCruiseItineraryContractTests
{
    /// <summary>Ensures every future provider operation starts with Creator scope.</summary>
    [Fact]
    public void ProviderOperations_RequireCreatorIdFirst()
    {
        var operations = typeof(IPublishedCruiseItineraryProvider).GetMethods();

        Assert.NotEmpty(operations);
        Assert.All(operations, operation =>
        {
            var parameters = operation.GetParameters();
            Assert.NotEmpty(parameters);
            Assert.Equal(typeof(CreatorId), parameters[0].ParameterType);
        });
    }

    /// <summary>Ensures the safe default returns no data and performs deterministic work only.</summary>
    [Fact]
    public async Task UnavailableProvider_ReturnsNoSailingsOrDetails()
    {
        var now = new DateTimeOffset(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);
        var provider = new UnavailablePublishedCruiseItineraryProvider(
            new FixedTimeProvider(now));
        var creatorId = new CreatorId("creator_test");
        var reference = new PublishedCruiseSailingReference("sailing-123");

        var matches = await provider.SearchSailingsAsync(
            creatorId,
            new PublishedCruiseSailingSearch(
                "Example Cruise Line",
                "Example Ship",
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 2, 1)));
        var sailing = await provider.GetSailingAsync(creatorId, reference);
        var freshness = await provider.GetFreshnessAsync(creatorId, reference);

        Assert.Empty(matches);
        Assert.Null(sailing);
        Assert.False(freshness.IsAvailable);
        Assert.Null(freshness.SourceUpdatedAtUtc);
        Assert.Equal(now, freshness.CheckedAtUtc);
    }

    /// <summary>Ensures the dormant boundary rejects absent Creator scope.</summary>
    [Fact]
    public async Task UnavailableProvider_RejectsMissingCreatorScope()
    {
        var provider = new UnavailablePublishedCruiseItineraryProvider(TimeProvider.System);
        var search = new PublishedCruiseSailingSearch(
            "Example Cruise Line",
            "Example Ship",
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 2, 1));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.SearchSailingsAsync(default, search));
    }

    /// <summary>Ensures searches remain bounded and opaque references remain bounded.</summary>
    [Fact]
    public void ValueObjects_RejectMalformedProviderInput()
    {
        Assert.Throws<ArgumentException>(() => new PublishedCruiseSailingReference(" "));
        Assert.Throws<ArgumentException>(() => new PublishedCruiseSailingSearch(
            " Example Cruise Line",
            "Example Ship",
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 2, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PublishedCruiseSailingSearch(
            "Example Cruise Line",
            "Example Ship",
            new DateOnly(2027, 1, 1),
            new DateOnly(2028, 1, 3)));
    }

    /// <summary>Ensures the application contract cannot expose provider infrastructure types.</summary>
    [Fact]
    public void ProviderContract_DoesNotExposeInfrastructureTypes()
    {
        var signatureAssemblies = typeof(IPublishedCruiseItineraryProvider)
            .GetMethods()
            .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)
                .Append(method.ReturnType))
            .Select(type => type.Assembly.GetName().Name ?? string.Empty);

        Assert.DoesNotContain(signatureAssemblies, assembly =>
            assembly.StartsWith("System.Net.Http", StringComparison.Ordinal)
            || assembly.StartsWith("Dapper", StringComparison.Ordinal)
            || assembly.StartsWith("Microsoft.Data.SqlClient", StringComparison.Ordinal));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
