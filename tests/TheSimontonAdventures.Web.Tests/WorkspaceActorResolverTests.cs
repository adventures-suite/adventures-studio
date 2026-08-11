using System.Security.Claims;
using AdventuresSuite.Identity.ExternalId;
using TheSimontonAdventures.Web.Authorization;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies web identity is minimized into the provider-neutral actor boundary.</summary>
public sealed class WorkspaceActorResolverTests
{
    private readonly WorkspaceActorResolver resolver = new();

    /// <summary>Anonymous principals never become Planning actors.</summary>
    [Fact]
    public void Resolve_AnonymousPrincipal_ReturnsNull() =>
        Assert.Null(resolver.Resolve(new ClaimsPrincipal(new ClaimsIdentity())));

    /// <summary>Exactly one valid application User identity becomes an ordinary human actor.</summary>
    [Fact]
    public void Resolve_ValidApplicationIdentity_ReturnsHumanActor()
    {
        var principal = Principal(new Claim(ApplicationUserClaims.UserId, "user_planner_01"));

        var actor = resolver.Resolve(principal);

        Assert.NotNull(actor);
        Assert.True(actor.IsHuman);
        Assert.Equal("user_planner_01", actor.UserId!.Value.Value);
    }

    /// <summary>Missing, duplicate, or malformed User claims fail closed.</summary>
    [Fact]
    public void Resolve_UntrustedClaims_ReturnNull()
    {
        Assert.Null(resolver.Resolve(Principal(new Claim("sub", "external-subject"))));
        Assert.Null(resolver.Resolve(Principal(
            new Claim(ApplicationUserClaims.UserId, "user_one"),
            new Claim(ApplicationUserClaims.UserId, "user_two"))));
        Assert.Null(resolver.Resolve(Principal(
            new Claim(ApplicationUserClaims.UserId, "INVALID"))));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "test"));
}
