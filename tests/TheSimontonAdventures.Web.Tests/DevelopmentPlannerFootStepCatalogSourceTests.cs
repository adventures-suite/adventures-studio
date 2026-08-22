using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the reviewed Development FootStep catalog and its trust boundaries.</summary>
public sealed class DevelopmentPlannerFootStepCatalogSourceTests
{
    private static readonly DateOnly RetrievalDate = new(2026, 8, 21);

    /// <summary>Real-world cards have stable identities, explicit ownership, and reviewable primary-source evidence.</summary>
    [Fact]
    public async Task ListAsync_RealWorldCatalog_HasUniqueVersionedSourceEvidence()
    {
        var source = CreateSource();

        var items = await source.ListAsync(new("creator_demo_01"), "en-US");
        var realWorld = items.Where(item => item.SourceClasses.Contains("real-world-curated")).ToArray();

        Assert.Equal(23, realWorld.Length);
        Assert.Equal(23, realWorld.Select(item => (item.Id, item.Version)).Distinct().Count());
        Assert.All(realWorld, item =>
        {
            Assert.Equal(new CreatorId("creator_tsa_01"), item.OwnerCreatorId);
            Assert.NotEmpty(item.Sources);
            Assert.All(item.Sources, evidence =>
            {
                Assert.Equal(Uri.UriSchemeHttps, evidence.Url.Scheme);
                Assert.Equal(RetrievalDate, evidence.RetrievedOn);
                Assert.True(evidence.ReviewedOn >= evidence.RetrievedOn);
                Assert.True(evidence.ReviewAfter > evidence.ReviewedOn);
            });
        });
    }

    /// <summary>The launch slice includes all requested kinds and broad planning facets without claiming a booking.</summary>
    [Fact]
    public async Task ListAsync_RealWorldCatalog_IsDiverseAndKeepsStaysSuggestionOnly()
    {
        var items = await CreateSource().ListAsync(new("creator_demo_01"), "en-US");
        var realWorld = items.Where(item => item.SourceClasses.Contains("real-world-curated")).ToArray();

        Assert.Contains(realWorld, item => item.Kind == "destination");
        Assert.Contains(realWorld, item => item.Kind == "activity");
        Assert.Contains(realWorld, item => item.Kind == "accommodation");
        Assert.Contains(realWorld, item => item.Places.Contains("italy"));
        Assert.Contains(realWorld, item => item.Places.Contains("spain"));
        Assert.Contains(realWorld, item => item.Places.Contains("greece"));
        Assert.Contains(realWorld, item => item.Places.Contains("key-west"));
        Assert.Contains(realWorld, item => item.Places.Contains("eastern-caribbean"));
        Assert.Contains(realWorld, item => item.BudgetBands.Contains("budget"));
        Assert.Contains(realWorld, item => item.BudgetBands.Contains("moderate"));
        Assert.Contains(realWorld, item => item.BudgetBands.Contains("premium"));
        Assert.Contains(realWorld, item => item.Paces.Contains("unhurried"));
        Assert.Contains(realWorld, item => item.Paces.Contains("active"));
        Assert.Contains(realWorld, item => item.Accessibility.Count > 0);

        var stays = realWorld.Where(item => item.Kind == "accommodation").ToArray();
        Assert.All(stays, item =>
        {
            Assert.Null(item.DestinationDraft);
            Assert.Null(item.ActivityDraft);
            Assert.Contains("no availability or price claim", item.Freshness, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("before booking", item.Summary, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>Catalog lookup fails closed when the customer Creator or locale boundary is absent.</summary>
    [Fact]
    public async Task ListAsync_MissingCreatorOrLocale_ReturnsNoContent()
    {
        var source = CreateSource();

        Assert.Empty(await source.ListAsync(default, "en-US"));
        Assert.Empty(await source.ListAsync(new("creator_demo_01"), string.Empty));
    }

    /// <summary>Existing filters can match real geography, kind, transport, budget, access, activity, and interest metadata together.</summary>
    [Fact]
    public async Task ListAsync_ContextualMetadata_SupportsCombinedMatching()
    {
        var items = await CreateSource().ListAsync(new("creator_demo_01"), "en-US");

        var match = Assert.Single(items, item =>
            item.ContextKinds.Contains(PlannerFootStepContextKind.Day)
            && item.Places.Contains("key-west")
            && item.Kind == "activity"
            && item.TransportationModes.Contains("bicycle")
            && item.BudgetBands.Contains("budget")
            && item.Accessibility.Contains("beach-wheelchair-loan")
            && item.Categories.Contains("history"));

        Assert.Equal("footstep_activity_fort_zachary_taylor", match.Id);
    }

    /// <summary>U.S. motorcycle ideas cover major rallies and varied routes without asserting event or road status.</summary>
    [Fact]
    public async Task ListAsync_UnitedStatesMotorcycleCatalog_IsSourcedFilterableAndAdvisory()
    {
        var items = await CreateSource().ListAsync(new("creator_demo_01"), "en-US");
        var motorcycle = items.Where(item =>
            item.SourceClasses.Contains("real-world-curated")
            && item.TransportationModes.Contains("motorcycle")).ToArray();

        Assert.Equal(10, motorcycle.Length);
        Assert.Contains(motorcycle, item => item.Id == "footstep_activity_sturgis_motorcycle_rally");
        Assert.Contains(motorcycle, item => item.Id == "footstep_route_historic_route_66_motorcycle");
        Assert.Equal(3, motorcycle.Count(item => item.Places.Contains("colorado")));
        Assert.Contains(motorcycle, item => item.BudgetBands.Contains("budget"));
        Assert.Contains(motorcycle, item => item.BudgetBands.Contains("premium"));
        Assert.Contains(motorcycle, item => item.Paces.Contains("unhurried"));
        Assert.Contains(motorcycle, item => item.Paces.Contains("active"));
        Assert.All(motorcycle, item =>
        {
            Assert.Null(item.DestinationDraft);
            Assert.Null(item.ActivityDraft);
            Assert.Contains("recheck", item.Freshness, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(item.Accessibility);
            Assert.NotEmpty(item.Sources);
        });

        var coloradoDayRide = Assert.Single(motorcycle, item =>
            item.ContextKinds.Contains(PlannerFootStepContextKind.Day)
            && item.Places.Contains("colorado")
            && item.Kind == "route-pattern"
            && item.BudgetBands.Contains("free")
            && item.Accessibility.Contains("altitude-review-required")
            && item.Categories.Contains("short-ride"));

        Assert.Equal("footstep_route_peak_to_peak_motorcycle", coloradoDayRide.Id);
    }

    private static DevelopmentPlannerFootStepCatalogSource CreateSource() =>
        new(TestContentServiceFactory.CreateHostEnvironment());
}
