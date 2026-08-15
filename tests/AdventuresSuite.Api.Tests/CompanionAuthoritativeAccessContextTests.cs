using AdventuresSuite.Companion.Application;
using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace AdventuresSuite.Api.Tests;

/// <summary>Verifies the provider-neutral authoritative access-context contracts.</summary>
public sealed class CompanionAuthoritativeAccessContextTests
{
    /// <summary>Proves exact external identity values are preserved without normalization.</summary>
    [Fact]
    public void ExternalIdentity_PreservesExactCaseSensitiveValues()
    {
        var identity = Identity("https://Login.Example.test/Tenant", "Subject-A");

        Assert.Equal("entra_external_id", identity.ProviderId.Value);
        Assert.Equal("https://Login.Example.test/Tenant", identity.Issuer.Value);
        Assert.Equal("Subject-A", identity.Subject.Value);
        Assert.NotEqual(identity, Identity("https://login.example.test/Tenant", "Subject-A"));
        Assert.NotEqual(identity, Identity("https://Login.Example.test/Tenant", "subject-a"));
    }

    /// <summary>Proves the default information policy remains closed and propagates cancellation.</summary>
    [Fact]
    public async Task ClosedPolicy_NeverApprovesProjection()
    {
        var policy = new ClosedCompanionInformationPolicy();
        var request = new CompanionInformationPolicyRequest(
            new UserId("user_alpha"), new CreatorId("creator_alpha"), "plan_alpha",
            "traveler_alpha", 3, 5, Permissions.AdventurePlanView,
            new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero));

        var decision = await policy.EvaluateAsync(request);
        Assert.False(decision.IsAllowed);
        Assert.Null(decision.Version);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            policy.EvaluateAsync(request, cancellation.Token));
    }

    /// <summary>Proves closed results never retain server-owned authorization facts.</summary>
    [Theory]
    [InlineData(CompanionAccessContextOutcome.Unmapped)]
    [InlineData(CompanionAccessContextOutcome.Disabled)]
    [InlineData(CompanionAccessContextOutcome.Revoked)]
    [InlineData(CompanionAccessContextOutcome.Inactive)]
    [InlineData(CompanionAccessContextOutcome.Unauthorized)]
    [InlineData(CompanionAccessContextOutcome.Ambiguous)]
    [InlineData(CompanionAccessContextOutcome.Malformed)]
    [InlineData(CompanionAccessContextOutcome.OperationallyUnavailable)]
    [InlineData(CompanionAccessContextOutcome.InformationPolicyClosed)]
    public void ClosedResolution_CarriesNoContext(CompanionAccessContextOutcome outcome)
    {
        var result = CompanionAccessContextResolution.Closed(outcome);

        Assert.Equal(outcome, result.Outcome);
        Assert.Null(result.Context);
    }

    /// <summary>Proves a successful context contains only server-owned, versioned facts.</summary>
    [Fact]
    public void ResolvedContext_RequiresExplicitServerFacts()
    {
        var evaluatedAt = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var context = new CompanionAuthoritativeAccessContext(
            new UserId("user_alpha"), 7, new CreatorId("creator_alpha"), "plan_alpha",
            "traveler_alpha", 3, 5, Permissions.AdventurePlanView, "adventure_read_v1", evaluatedAt);

        var result = CompanionAccessContextResolution.Resolved(context);

        Assert.Equal(CompanionAccessContextOutcome.Resolved, result.Outcome);
        Assert.Same(context, result.Context);
        var resolved = Assert.IsType<CompanionAuthoritativeAccessContext>(result.Context);
        Assert.Equal(Permissions.AdventurePlanView, resolved.RequiredPermission);
        Assert.Equal(evaluatedAt, resolved.EvaluatedAtUtc);
    }

    private static CompanionExternalIdentity Identity(string issuer, string subject) => new(
        new ExternalIdentityProviderId("entra_external_id"),
        new ExternalIdentityIssuer(issuer),
        new ExternalIdentitySubject(subject));
}
