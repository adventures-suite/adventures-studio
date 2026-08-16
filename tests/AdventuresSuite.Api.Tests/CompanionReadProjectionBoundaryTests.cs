using System.Reflection;
using AdventuresSuite.Companion.Application;

namespace AdventuresSuite.Api.Tests;

/// <summary>Protects the provider-neutral Companion read-projection boundary.</summary>
public sealed class CompanionReadProjectionBoundaryTests
{
    /// <summary>Ensures application contracts do not expose adapter or transport dependencies.</summary>
    [Fact]
    public void ApplicationContracts_AreProviderNeutral()
    {
        var assembly = typeof(ICompanionAdventureSummaryQuery).Assembly;
        var referenced = assembly.GetReferencedAssemblies().Select(value => value.Name).ToHashSet();

        Assert.DoesNotContain("Dapper", referenced);
        Assert.DoesNotContain("Microsoft.Data.SqlClient", referenced);
        Assert.DoesNotContain("Microsoft.AspNetCore", referenced);
        Assert.DoesNotContain("AdventuresSuite.Api", referenced);
        Assert.All(
            typeof(ICompanionAdventureSummaryQuery).GetMethods()
                .Concat(typeof(ICompanionAdventureDetailQuery).GetMethods())
                .Concat(typeof(ICompanionTodayQuery).GetMethods())
                .Concat(typeof(ICompanionItineraryQuery).GetMethods())
                .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)
                    .Append(method.ReturnType)),
            type => Assert.DoesNotContain("Contracts", type.FullName ?? string.Empty, StringComparison.Ordinal));
    }

    /// <summary>Ensures SQL persistence rows remain private to their adapter assembly.</summary>
    [Fact]
    public void SqlAdapter_DoesNotExportPersistenceRows()
    {
        var assemblyPath = Path.Combine(
            AppContext.BaseDirectory,
            "AdventuresSuite.Companion.SqlServer.dll");
        Assert.True(File.Exists(assemblyPath));
        var assembly = Assembly.LoadFrom(assemblyPath);

        Assert.DoesNotContain(assembly.ExportedTypes,
            type => type.Name.EndsWith("Row", StringComparison.Ordinal));
    }
}
