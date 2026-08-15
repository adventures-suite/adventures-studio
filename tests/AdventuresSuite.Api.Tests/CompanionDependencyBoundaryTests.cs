using AdventuresSuite.Companion.Application;

namespace AdventuresSuite.Api.Tests;

/// <summary>Verifies the independent API host dependency boundary.</summary>
public sealed class CompanionDependencyBoundaryTests
{
    /// <summary>Ensures the API and Application projects do not reference prohibited hosts or infrastructure.</summary>
    [Fact]
    public void ProjectsHaveNoProhibitedReferences()
    {
        var apiReferences = typeof(CompanionApiConstants).Assembly.GetReferencedAssemblies().Select(value => value.Name).ToArray();
        var applicationReferences = typeof(ICompanionProjectionService).Assembly.GetReferencedAssemblies().Select(value => value.Name).ToArray();
        Assert.DoesNotContain("TheSimontonAdventures.Web", apiReferences);
        Assert.DoesNotContain("AdventuresSuite.Companion.Poc", apiReferences);
        Assert.DoesNotContain(applicationReferences, value => value?.Contains("SqlServer", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(applicationReferences, value => value?.StartsWith("Azure.", StringComparison.Ordinal) == true);
        Assert.DoesNotContain("Dapper", applicationReferences);
        Assert.DoesNotContain("Microsoft.Data.SqlClient", applicationReferences);
        Assert.DoesNotContain(applicationReferences,
            value => value?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) == true);
    }
}
