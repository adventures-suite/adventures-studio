using System.Reflection;
using AdventuresSuite.DatabaseMigrator;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies deterministic discovery of immutable database migrations.</summary>
public sealed class DatabaseMigrationTests
{
    private static readonly Assembly MigratorAssembly = typeof(MigrationCatalog).Assembly;

    /// <summary>Ensures migrations are embedded once and sorted by ordinal name.</summary>
    [Fact]
    public void Catalog_ReturnsUniqueOrderedMigrations()
    {
        var migrations = MigrationCatalog.GetOrderedResourceNames(MigratorAssembly);

        Assert.Equal(3, migrations.Count);
        Assert.EndsWith("0001_create_planning_schema.sql", migrations[0], StringComparison.Ordinal);
        Assert.EndsWith("0002_create_adventure_plans.sql", migrations[1], StringComparison.Ordinal);
        Assert.EndsWith("0003_create_planning_children.sql", migrations[2], StringComparison.Ordinal);
        Assert.Equal(migrations.Count, migrations.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Ensures DbUp's embedded-resource filter selects only migrations.</summary>
    [Fact]
    public void Catalog_FilterRejectsNonMigrationResources()
    {
        Assert.False(MigrationCatalog.IsMigrationResource(
            MigratorAssembly,
            "AdventuresSuite.DatabaseMigrator.not-a-migration.txt"));
    }

    /// <summary>Ensures every independently stored Planning child preserves Creator scope.</summary>
    [Fact]
    public void PlanningChildren_EachTableDeclaresCreatorIdentity()
    {
        var migration = ReadMigration("0003_create_planning_children.sql");
        var tableSections = migration.Split(
                "CREATE TABLE ",
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        Assert.Equal(12, tableSections.Length);
        Assert.All(tableSections, section =>
            Assert.Contains("CreatorId nvarchar(64) NOT NULL", section, StringComparison.Ordinal));
    }

    /// <summary>Ensures the aggregate schema enforces Creator identity and versioning.</summary>
    [Fact]
    public void AdventurePlanSchema_DeclaresTenantAndConcurrencyConstraints()
    {
        var migration = ReadMigration("0002_create_adventure_plans.sql");

        Assert.Contains(
            "PRIMARY KEY (CreatorId, AdventurePlanId)",
            migration,
            StringComparison.Ordinal);
        Assert.Contains("Version bigint NOT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("CHECK (Version > 0)", migration, StringComparison.Ordinal);
        Assert.Contains("CreatedAtUtc datetimeoffset(0) NOT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("UpdatedAtUtc datetimeoffset(0) NOT NULL", migration, StringComparison.Ordinal);
    }

    private static string ReadMigration(string fileName)
    {
        var resourceName = MigrationCatalog.GetOrderedResourceNames(MigratorAssembly)
            .Single(name => name.EndsWith(fileName, StringComparison.Ordinal));
        using var stream = MigratorAssembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
