using TheSimontonAdventures.Web.Authorization;

namespace AdventuresSuite.Authorization.Tests;

/// <summary>Verifies the extracted provider-neutral authorization boundary.</summary>
public sealed class AuthorizationBoundaryTests
{
    /// <summary>Ensures the shared contracts cannot depend on hosts or infrastructure adapters.</summary>
    [Fact]
    public void AuthorizationAssemblyHasNoHostOrInfrastructureDependencies()
    {
        var references = typeof(Permission).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain("TheSimontonAdventures.Web", references);
        Assert.DoesNotContain(references, value => value.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, value => value.StartsWith("Azure.", StringComparison.Ordinal));
        Assert.DoesNotContain(references, value => value.Contains("Sql", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, value => value.Contains("Dapper", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Ensures permission and membership-audit vocabularies remain closed and distinct.</summary>
    [Fact]
    public void PermissionAndMembershipResourceVocabulariesAreClosed()
    {
        Assert.True(Permission.IsApproved(Permissions.CreatorManageMembers.Value));
        Assert.Throws<ArgumentException>(() => new Permission("Creator.Delete"));
        Assert.Equal("CreatorMembership", AuthorizationResourceTypes.CreatorMembership.Value);
        Assert.NotEqual(AuthorizationResourceTypes.Creator, AuthorizationResourceTypes.CreatorMembership);
    }
}
