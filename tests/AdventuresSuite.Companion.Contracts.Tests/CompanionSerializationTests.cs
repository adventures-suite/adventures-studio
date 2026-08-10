using System.Text.Json;
using AdventuresSuite.Companion.Contracts;

namespace AdventuresSuite.Companion.Contracts.Tests;

/// <summary>Verifies the standalone wire-contract assembly.</summary>
public sealed class CompanionSerializationTests
{
    /// <summary>Ensures closed enums serialize using the approved camel-case wire values.</summary>
    [Fact]
    public void ClosedEnumsUseApprovedWireValues()
    {
        Assert.Equal("\"inProgress\"", JsonSerializer.Serialize(
            CompanionAdventureStatus.InProgress,
            CompanionJsonSerializerContext.Default.CompanionAdventureStatus));
        Assert.Equal("\"attentionRequired\"", JsonSerializer.Serialize(
            CompanionReadinessState.AttentionRequired,
            CompanionJsonSerializerContext.Default.CompanionReadinessState));
    }

    /// <summary>Ensures source-generated metadata covers every public response root.</summary>
    [Fact]
    public void SourceGeneratedContextCoversEveryRoot()
    {
        Assert.NotNull(CompanionJsonSerializerContext.Default.CompanionAdventureCollectionDto);
        Assert.NotNull(CompanionJsonSerializerContext.Default.CompanionAdventureDto);
        Assert.NotNull(CompanionJsonSerializerContext.Default.CompanionTodayDto);
        Assert.NotNull(CompanionJsonSerializerContext.Default.CompanionItineraryDto);
        Assert.NotNull(CompanionJsonSerializerContext.Default.CompanionReadinessDto);
        Assert.NotNull(CompanionJsonSerializerContext.Default.CompanionPlaybookDto);
        Assert.NotNull(CompanionJsonSerializerContext.Default.CompanionProblemDto);
    }

    /// <summary>Ensures contracts cannot acquire server, SQL, ASP.NET, Azure, or provider dependencies.</summary>
    [Fact]
    public void ContractsAssemblyHasNoProhibitedDependencies()
    {
        var references = typeof(CompanionProjectionDto).Assembly.GetReferencedAssemblies()
            .Select(value => value.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(references, value => value.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, value => value.StartsWith("Azure.", StringComparison.Ordinal));
        Assert.DoesNotContain(references, value => value.Contains("Sql", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, value => value.Contains("Identity", StringComparison.OrdinalIgnoreCase));
    }
}
