using AdventuresSuite.Companion.Application;
using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace AdventuresSuite.Api.Tests;

/// <summary>Verifies the closed Adventure-overview information-policy catalog and boundary.</summary>
public sealed class CompanionInformationPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Proves the v1 profile exposes only the approved overview fields and presentation modes.</summary>
    [Fact]
    public void AdventureOverviewV1_IsExactClosedFieldCatalog()
    {
        var catalog = new CompanionInformationProfileCatalog();

        Assert.True(catalog.TryGet(CompanionInformationProfileCatalog.AdventureOverviewV1, out var profile));
        var value = Assert.IsType<CompanionInformationProfile>(profile);
        Assert.Equal(1, value.DefinitionVersion);
        Assert.Equal(
            [
                CompanionInformationField.AdventureId,
                CompanionInformationField.AdventureTitle,
                CompanionInformationField.AdventureStatus,
                CompanionInformationField.AdventureDates,
                CompanionInformationField.PrimaryTimeZone,
                CompanionInformationField.CountdownInputs,
                CompanionInformationField.DestinationVisitId,
                CompanionInformationField.DestinationName,
                CompanionInformationField.DestinationDates,
                CompanionInformationField.DestinationTimeZone,
                CompanionInformationField.DestinationSequence
            ],
            value.AllowedFields.OrderBy(field => field));
        Assert.True(value.UsesGenericDescription);
        Assert.True(value.UsesGenericReadiness);
        Assert.False(value.IncludesCapabilityLinks);
        Assert.False(value.IncludesHeroResources);
        Assert.False(value.IncludesNextItem);
        Assert.False(catalog.TryGet("Companion_adventure_overview_v1", out _));
        Assert.False(catalog.TryGet("companion_adventure_overview_v2", out _));
    }

    /// <summary>Proves only one exact active assignment to the current participation can allow the profile.</summary>
    [Fact]
    public async Task AssignedPolicy_ExactActiveAssignment_AllowsOpaqueVersion()
    {
        var policy = Policy(Assignment());

        var decision = await policy.EvaluateAsync(Request());

        Assert.True(decision.IsAllowed);
        Assert.Equal("info_overview_v1_d1_a9", decision.Version);
    }

    /// <summary>Proves missing and unknown assignments cannot fall back to a profile.</summary>
    [Fact]
    public async Task AssignedPolicy_MissingOrUnknownProfile_FailsClosed()
    {
        AssertClosed(await Policy(null).EvaluateAsync(Request()));
        AssertClosed(await Policy(Assignment(profileKey: "unknown_profile")).EvaluateAsync(Request()));
        AssertClosed(await Policy(Assignment()).EvaluateAsync(Request(Permissions.AdventurePlanEdit)));

        var closedProvider = new ClosedCompanionInformationPolicyAssignmentProvider();
        Assert.Null(await closedProvider.GetAsync(
            new CreatorId("creator_alpha"), "plan_alpha", "traveler_alpha"));
    }

    /// <summary>Proves inactive, expired, revoked, and stale-participation assignments fail closed.</summary>
    [Theory]
    [InlineData("revoked")]
    [InlineData("future")]
    [InlineData("expired")]
    [InlineData("stale_participation")]
    public async Task AssignedPolicy_IneffectiveAssignment_FailsClosed(string scenario)
    {
        var assignment = scenario switch
        {
            "revoked" => Assignment(status: CompanionInformationPolicyAssignmentStatus.Revoked),
            "future" => Assignment(effectiveFromUtc: Now.AddMinutes(1)),
            "expired" => Assignment(expiresAtUtc: Now),
            "stale_participation" => Assignment(participationVersion: 4),
            _ => throw new InvalidOperationException()
        };

        AssertClosed(await Policy(assignment).EvaluateAsync(Request()));
    }

    /// <summary>Proves Creator, Adventure, traveler, and case substitutions fail closed.</summary>
    [Theory]
    [InlineData("creator")]
    [InlineData("adventure")]
    [InlineData("traveler")]
    [InlineData("adventure_case")]
    [InlineData("traveler_case")]
    public async Task AssignedPolicy_ScopeSubstitution_FailsClosed(string scenario)
    {
        var assignment = scenario switch
        {
            "creator" => Assignment(creatorId: "creator_beta"),
            "adventure" => Assignment(adventureId: "plan_beta"),
            "traveler" => Assignment(travelerId: "traveler_beta"),
            "adventure_case" => Assignment(adventureId: "Plan_alpha"),
            "traveler_case" => Assignment(travelerId: "Traveler_alpha"),
            _ => throw new InvalidOperationException()
        };

        AssertClosed(await Policy(assignment).EvaluateAsync(Request()));
    }

    /// <summary>Proves cancellation propagates and never becomes an allowed or denied decision.</summary>
    [Fact]
    public async Task AssignedPolicy_Cancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Policy(Assignment()).EvaluateAsync(Request(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ClosedCompanionInformationPolicyAssignmentProvider().GetAsync(
                new CreatorId("creator_alpha"), "plan_alpha", "traveler_alpha", cancellation.Token));
    }

    private static AssignedCompanionInformationPolicy Policy(
        CompanionInformationPolicyAssignment? assignment) => new(
            new CompanionInformationProfileCatalog(),
            new StubAssignmentProvider(assignment));

    private static CompanionInformationPolicyRequest Request(Permission? permission = null) => new(
        new UserId("user_alpha"),
        new CreatorId("creator_alpha"),
        "plan_alpha",
        "traveler_alpha",
        MembershipVersion: 3,
        ParticipationVersion: 5,
        permission ?? Permissions.AdventurePlanView,
        Now);

    private static CompanionInformationPolicyAssignment Assignment(
        string creatorId = "creator_alpha",
        string adventureId = "plan_alpha",
        string travelerId = "traveler_alpha",
        long participationVersion = 5,
        string profileKey = CompanionInformationProfileCatalog.AdventureOverviewV1,
        CompanionInformationPolicyAssignmentStatus status = CompanionInformationPolicyAssignmentStatus.Active,
        DateTimeOffset? effectiveFromUtc = null,
        DateTimeOffset? expiresAtUtc = null) => new(
            new CreatorId(creatorId),
            adventureId,
            travelerId,
            participationVersion,
            profileKey,
            version: 9,
            status,
            effectiveFromUtc ?? Now.AddDays(-1),
            expiresAtUtc);

    private static void AssertClosed(CompanionInformationPolicyDecision decision)
    {
        Assert.False(decision.IsAllowed);
        Assert.Null(decision.Version);
    }

    private sealed class StubAssignmentProvider(CompanionInformationPolicyAssignment? assignment)
        : ICompanionInformationPolicyAssignmentProvider
    {
        public Task<CompanionInformationPolicyAssignment?> GetAsync(
            CreatorId creatorId,
            string adventureId,
            string travelerId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(assignment);
        }
    }
}
