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

        Assert.Equal(8, migrations.Count);
        Assert.EndsWith("0001_create_planning_schema.sql", migrations[0], StringComparison.Ordinal);
        Assert.EndsWith("0002_create_adventure_plans.sql", migrations[1], StringComparison.Ordinal);
        Assert.EndsWith("0003_create_planning_children.sql", migrations[2], StringComparison.Ordinal);
        Assert.EndsWith("0004_create_authentication_persistence.sql", migrations[3], StringComparison.Ordinal);
        Assert.EndsWith("0005_bind_sessions_to_external_identities.sql", migrations[4], StringComparison.Ordinal);
        Assert.EndsWith("0006_create_creator_memberships.sql", migrations[5], StringComparison.Ordinal);
        Assert.EndsWith("0007_create_traveler_participations.sql", migrations[6], StringComparison.Ordinal);
        Assert.EndsWith("0008_create_companion_read_role.sql", migrations[7], StringComparison.Ordinal);
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

    /// <summary>Ensures authentication persistence preserves exact identity and least privilege.</summary>
    [Fact]
    public void AuthenticationSchema_DeclaresExactIdentityAndRuntimeBoundaries()
    {
        var migration = ReadMigration("0004_create_authentication_persistence.sql");

        Assert.Contains("CREATE TABLE auth.Users", migration, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE auth.ExternalIdentities", migration, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE auth.UserSessions", migration, StringComparison.Ordinal);
        Assert.Contains("Issuer nvarchar(2048) COLLATE Latin1_General_100_BIN2", migration, StringComparison.Ordinal);
        Assert.Contains("Subject nvarchar(255) COLLATE Latin1_General_100_BIN2", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("Email", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GRANT SELECT, INSERT, UPDATE ON SCHEMA::auth", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("GRANT SELECT, INSERT, UPDATE, DELETE", migration, StringComparison.Ordinal);
        Assert.Contains("DENY ALTER ON SCHEMA::auth", migration, StringComparison.Ordinal);
    }

    /// <summary>Ensures membership state is Creator-scoped and audit evidence is append-only.</summary>
    [Fact]
    public void MembershipSchema_DeclaresIsolationConcurrencyAndAuditBoundaries()
    {
        var migration = ReadMigration("0006_create_creator_memberships.sql");

        Assert.Contains("PRIMARY KEY (CreatorId, CreatorMembershipId)", migration, StringComparison.Ordinal);
        Assert.Contains("UNIQUE (CreatorId, UserId)", migration, StringComparison.Ordinal);
        Assert.Contains("Version bigint NOT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE auth.CreatorMembershipRoles", migration, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE auth.CreatorMembershipPermissionGrants", migration, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE audit.AuditEvents", migration, StringComparison.Ordinal);
        Assert.Contains("GRANT SELECT, INSERT ON OBJECT::audit.AuditEvents", migration, StringComparison.Ordinal);
        Assert.Contains("DENY UPDATE, DELETE ON OBJECT::audit.AuditEvents", migration, StringComparison.Ordinal);
        Assert.Contains("DENY DELETE ON OBJECT::auth.CreatorMemberships", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("GRANT DELETE ON OBJECT::auth.CreatorMemberships", migration, StringComparison.Ordinal);
    }

    /// <summary>Ensures mobile access requires an explicit plan-scoped user binding.</summary>
    [Fact]
    public void TravelerParticipationSchema_DeclaresExplicitRevocableAccessWithoutRuntimeGrant()
    {
        var migration = ReadMigration("0007_create_traveler_participations.sql");

        Assert.Contains("CREATE TABLE planning.TravelerParticipations", migration, StringComparison.Ordinal);
        Assert.Contains("PRIMARY KEY (CreatorId, AdventurePlanId, UserId)", migration, StringComparison.Ordinal);
        Assert.Contains("Status IN ('Invited', 'Accepted', 'Revoked')", migration, StringComparison.Ordinal);
        Assert.Contains("REFERENCES planning.Travelers", migration, StringComparison.Ordinal);
        Assert.Contains("REFERENCES auth.Users", migration, StringComparison.Ordinal);
        Assert.Contains("IX_TravelerParticipations_AuthorizedList", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("GRANT SELECT", migration, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ensures the Companion role is object-scoped and read-only.</summary>
    [Fact]
    public void CompanionReadRole_UsesExplicitObjectsAndDeniesMutationAndDdl()
    {
        var migration = ReadMigration("0008_create_companion_read_role.sql");

        Assert.Contains("CREATE ROLE AdventuresSuiteCompanionReadRuntime", migration, StringComparison.Ordinal);
        Assert.Contains("GRANT SELECT ON OBJECT::planning.AdventurePlans", migration, StringComparison.Ordinal);
        Assert.Contains("GRANT SELECT ON OBJECT::auth.CreatorMemberships", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("GRANT SELECT ON SCHEMA", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DENY INSERT, UPDATE, DELETE", migration, StringComparison.Ordinal);
        Assert.Contains("DENY ALTER, CONTROL ON SCHEMA::planning", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("db_datareader", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("db_datawriter", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("db_ddladmin", migration, StringComparison.OrdinalIgnoreCase);
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
