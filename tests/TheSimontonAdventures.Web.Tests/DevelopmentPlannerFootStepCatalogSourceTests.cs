using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the reviewed Development FootStep catalog and its trust boundaries.</summary>
public sealed class DevelopmentPlannerFootStepCatalogSourceTests
{
    private static readonly DateOnly EarliestRetrievalDate = new(2026, 8, 21);

    /// <summary>Real-world cards have stable identities, explicit ownership, and reviewable primary-source evidence.</summary>
    [Fact]
    public async Task ListAsync_RealWorldCatalog_HasUniqueVersionedSourceEvidence()
    {
        var source = CreateSource();

        var items = await source.ListAsync(new("creator_demo_01"), "en-US");
        var realWorld = items.Where(item => item.SourceClasses.Contains("real-world-curated")).ToArray();

        Assert.Equal(64, realWorld.Length);
        Assert.Equal(64, realWorld.Select(item => (item.Id, item.Version)).Distinct().Count());
        Assert.All(realWorld, item =>
        {
            Assert.Equal(new CreatorId("creator_tsa_01"), item.OwnerCreatorId);
            Assert.NotEmpty(item.Sources);
            Assert.All(item.Sources, evidence =>
            {
                Assert.Equal(Uri.UriSchemeHttps, evidence.Url.Scheme);
                Assert.True(evidence.RetrievedOn >= EarliestRetrievalDate);
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
            && item.TransportationModes.Contains("motorcycle")
            && !item.Categories.Contains("us-southwest")
            && item.Kind != "journey-pattern").ToArray();

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

    /// <summary>National-park RV ideas expose trip-planning facets without asserting campsite or vehicle suitability.</summary>
    [Fact]
    public async Task ListAsync_NationalParkRvCatalog_IsSourcedFilterableAndAdvisory()
    {
        var items = await CreateSource().ListAsync(new("creator_demo_01"), "en-US");
        var rvTrips = items.Where(item =>
            item.SourceClasses.Contains("real-world-curated")
            && item.TransportationModes.Contains("rv")
            && !item.Categories.Contains("us-southwest")
            && item.Categories.Contains("national-park")).ToArray();

        Assert.Equal(10, rvTrips.Length);
        Assert.Contains(rvTrips, item => item.Id == "footstep_route_yellowstone_rv_loop");
        Assert.Contains(rvTrips, item => item.Id == "footstep_route_zion_bryce_rv");
        Assert.Contains(rvTrips, item => item.Places.Contains("olympic-national-park"));
        Assert.Contains(rvTrips, item => item.Places.Contains("acadia-national-park"));
        Assert.Contains(rvTrips, item => item.BudgetBands.Contains("budget"));
        Assert.Contains(rvTrips, item => item.BudgetBands.Contains("premium"));
        Assert.Contains(rvTrips, item => item.Seasons.Contains("winter"));
        Assert.All(rvTrips, item =>
        {
            Assert.Null(item.DestinationDraft);
            Assert.Null(item.ActivityDraft);
            Assert.Contains("no campsite availability", item.Freshness, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("vehicle-length-review-required", item.Accessibility);
            Assert.NotEmpty(item.Sources);
        });

        var shuttleBase = Assert.Single(rvTrips, item =>
            item.ContextKinds.Contains(PlannerFootStepContextKind.Destination)
            && item.Places.Contains("glacier-national-park")
            && item.Kind == "destination"
            && item.TransportationModes.Contains("shuttle")
            && item.Accessibility.Contains("boarding-review-required")
            && item.Categories.Contains("wildlife"));

        Assert.Equal("footstep_destination_glacier_rv_base", shuttleBase.Id);
    }

    /// <summary>Motorcycle Journey blueprints cover every launch rally and riding region without becoming plan state.</summary>
    [Fact]
    public async Task ListAsync_MotorcycleJourneyCatalog_CoversLaunchPlacesAndRemainsAdvisory()
    {
        var items = await CreateSource().ListAsync(new("creator_demo_01"), "en-US");
        var journeys = items.Where(item =>
            item.SourceClasses.Contains("real-world-curated")
            && item.Kind == "journey-pattern"
            && !item.Categories.Contains("us-southwest")
            && item.TransportationModes.Contains("motorcycle")).ToArray();

        Assert.Equal(7, journeys.Length);
        Assert.Contains(journeys, item => item.Places.Contains("sturgis"));
        Assert.Contains(journeys, item => item.Places.Contains("daytona-beach"));
        Assert.Contains(journeys, item => item.Places.Contains("lake-george") && item.Places.Contains("laconia"));
        Assert.Contains(journeys, item => item.Places.Contains("route-66"));
        Assert.Contains(journeys, item => item.Places.Contains("peak-to-peak")
            && item.Places.Contains("rocky-mountain-national-park")
            && item.Places.Contains("san-juan-mountains"));
        Assert.Contains(journeys, item => item.Places.Contains("blue-ridge-parkway"));
        Assert.Contains(journeys, item => item.Places.Contains("natchez-trace-parkway"));
        Assert.All(journeys, item =>
        {
            Assert.Null(item.DestinationDraft);
            Assert.Null(item.ActivityDraft);
            Assert.Contains(PlannerFootStepContextKind.Adventure, item.ContextKinds);
            Assert.Contains("journey-blueprint", item.Categories);
            Assert.True(item.DurationDays >= 5);
            Assert.Contains("recheck", item.Freshness, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(item.Sources);
        });
    }

    /// <summary>U.S. camping and hiking ideas cover stays, day activities, and Journey blueprints safely.</summary>
    [Fact]
    public async Task ListAsync_UnitedStatesCampingAndHikingCatalog_IsDiverseAndAdvisory()
    {
        var items = await CreateSource().ListAsync(new("creator_demo_01"), "en-US");
        var campingAndHiking = items.Where(item =>
            item.SourceClasses.Contains("real-world-curated")
            && item.Categories.Contains("usa-camping-hiking")).ToArray();

        Assert.Equal(10, campingAndHiking.Length);
        Assert.Equal(3, campingAndHiking.Count(item => item.Kind == "accommodation"));
        Assert.Equal(4, campingAndHiking.Count(item => item.Kind == "activity"));
        Assert.Equal(3, campingAndHiking.Count(item => item.Kind == "journey-pattern"));
        Assert.Contains(campingAndHiking, item => item.Places.Contains("california"));
        Assert.Contains(campingAndHiking, item => item.Places.Contains("washington"));
        Assert.Contains(campingAndHiking, item => item.Places.Contains("virginia"));
        Assert.Contains(campingAndHiking, item => item.Places.Contains("arizona"));
        Assert.Contains(campingAndHiking, item => item.Places.Contains("utah"));
        Assert.Contains(campingAndHiking, item => item.Accessibility.Contains("mobility-access-details-published"));
        Assert.Contains(campingAndHiking, item => item.Categories.Contains("strenuous-hike"));
        Assert.Contains(campingAndHiking, item => item.Paces.Contains("unhurried"));
        Assert.Contains(campingAndHiking, item => item.Paces.Contains("active"));
        Assert.All(campingAndHiking, item =>
        {
            Assert.Null(item.DestinationDraft);
            Assert.Null(item.ActivityDraft);
            Assert.NotEmpty(item.Sources);
            Assert.Contains("recheck", item.Freshness, StringComparison.OrdinalIgnoreCase);
        });

        Assert.All(campingAndHiking.Where(item => item.Kind == "accommodation"), item =>
        {
            Assert.Contains("before booking", item.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no availability or price claim", item.Freshness, StringComparison.OrdinalIgnoreCase);
        });
        Assert.All(campingAndHiking.Where(item => item.Kind == "journey-pattern"), item =>
        {
            Assert.Contains(PlannerFootStepContextKind.Adventure, item.ContextKinds);
            Assert.Contains("journey-blueprint", item.Categories);
            Assert.True(item.DurationDays >= 5);
        });
        Assert.All(campingAndHiking.Where(item => item.Kind == "activity"), item =>
        {
            Assert.Contains(PlannerFootStepContextKind.Adventure, item.ContextKinds);
            Assert.Contains(PlannerFootStepContextKind.Destination, item.ContextKinds);
            Assert.Contains(PlannerFootStepContextKind.Day, item.ContextKinds);
        });
    }

    /// <summary>Southwest coverage provides one Destination and one Journey for every developed travel category.</summary>
    [Fact]
    public async Task ListAsync_SouthwestCatalog_CoversEveryCategoryWithDestinationsAndJourneys()
    {
        var items = await CreateSource().ListAsync(new("creator_demo_01"), "en-US");
        var southwest = items.Where(item => item.Categories.Contains("us-southwest")).ToArray();

        Assert.Equal(8, southwest.Length);
        Assert.Equal(4, southwest.Count(item => item.Kind == "destination"));
        Assert.Equal(4, southwest.Count(item => item.Kind == "journey-pattern"));
        foreach (var category in new[] { "motorcycle-touring", "rv-travel", "camping", "hiking" })
        {
            Assert.Contains(southwest, item => item.Kind == "destination" && item.Categories.Contains(category));
            Assert.Contains(southwest, item => item.Kind == "journey-pattern" && item.Categories.Contains(category));
        }

        Assert.Contains(southwest, item => item.Places.Contains("arizona"));
        Assert.Contains(southwest, item => item.Places.Contains("new-mexico"));
        Assert.Contains(southwest, item => item.Places.Contains("texas"));
        Assert.All(southwest, item =>
        {
            Assert.Null(item.DestinationDraft);
            Assert.Null(item.ActivityDraft);
            Assert.Contains(PlannerFootStepContextKind.Adventure, item.ContextKinds);
            Assert.NotEmpty(item.Accessibility);
            Assert.NotEmpty(item.Sources);
            Assert.Contains("recheck", item.Freshness, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("us-southwest", item.Places);
        });
        Assert.All(southwest.Where(item => item.Kind == "journey-pattern"), item =>
        {
            Assert.Contains("journey-blueprint", item.Categories);
            Assert.True(item.DurationDays >= 5);
        });
    }

    /// <summary>West Coast wine ideas provide one Destination and Journey per state with responsible, reviewable claims.</summary>
    [Fact]
    public async Task ListAsync_WestCoastWineCatalog_CoversEachStateAndRemainsAdvisory()
    {
        var items = await CreateSource().ListAsync(new("creator_demo_01"), "en-US");
        var wine = items.Where(item => item.Categories.Contains("wine-travel")).ToArray();

        Assert.Equal(6, wine.Length);
        Assert.Equal(3, wine.Count(item => item.Kind == "destination"));
        Assert.Equal(3, wine.Count(item => item.Kind == "journey-pattern"));
        foreach (var state in new[] { "california", "oregon", "washington" })
        {
            Assert.Contains(wine, item => item.Kind == "destination" && item.Places.Contains(state));
            Assert.Contains(wine, item => item.Kind == "journey-pattern" && item.Places.Contains(state));
        }

        Assert.Contains(wine, item => item.TransportationModes.Contains("walking"));
        Assert.Contains(wine, item => item.TransportationModes.Contains("bicycle"));
        Assert.Contains(wine, item => item.TransportationModes.Contains("hired-driver"));
        Assert.Contains(wine, item => item.BudgetBands.Contains("budget"));
        Assert.Contains(wine, item => item.BudgetBands.Contains("premium"));
        Assert.Contains(wine, item => item.Paces.Contains("unhurried"));
        Assert.Contains(wine, item => item.Paces.Contains("active"));
        var washingtonMatch = Assert.Single(wine, item =>
            item.Kind == "destination"
            && item.Places.Contains("washington")
            && item.TransportationModes.Contains("walking")
            && item.BudgetBands.Contains("budget")
            && item.Categories.Contains("art"));
        Assert.Equal("footstep_destination_walla_walla_wine_districts", washingtonMatch.Id);
        Assert.All(wine, item =>
        {
            Assert.Equal(new CreatorId("creator_tsa_01"), item.OwnerCreatorId);
            Assert.Null(item.DestinationDraft);
            Assert.Null(item.ActivityDraft);
            Assert.Contains(PlannerFootStepContextKind.Adventure, item.ContextKinds);
            Assert.Contains("us-pacific", item.Places);
            Assert.Contains("recheck", item.Freshness, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("responsible-transport-review-required", item.Accessibility);
            Assert.NotEmpty(item.Sources);
            Assert.DoesNotContain("guaranteed", item.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("available now", item.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("book through", item.Summary, StringComparison.OrdinalIgnoreCase);
        });
        Assert.All(wine.Where(item => item.Kind == "journey-pattern"), item =>
        {
            Assert.Contains("journey-blueprint", item.Categories);
            Assert.True(item.DurationDays >= 5);
        });
    }

    private static DevelopmentPlannerFootStepCatalogSource CreateSource() =>
        new(TestContentServiceFactory.CreateHostEnvironment());
}
