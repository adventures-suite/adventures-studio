using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.DatabaseMigrator;

/// <summary>Captures and classifies bounded migration evidence without changing database state.</summary>
internal static class MigrationOperationalState
{
    internal static async Task<MigrationStateEvidence> CaptureAsync(string connectionString)
        => await CaptureAsync(() => new SqlConnection(connectionString));

    /// <summary>Captures state through the exact reviewed connection factory.</summary>
    internal static async Task<MigrationStateEvidence> CaptureAsync(
        Func<SqlConnection> connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        await using var connection = connectionFactory();
        await connection.OpenAsync();

        var journal = await ReadStringsAsync(connection, """
            SELECT ScriptName
            FROM dbo.AdventuresSuiteSchemaVersions
            ORDER BY ScriptName;
            """);
        var objects = await ReadStringsAsync(connection, """
            SELECT CONCAT(schemas.name COLLATE DATABASE_DEFAULT, N'.',
                          objects.name COLLATE DATABASE_DEFAULT, N'|',
                          objects.type_desc COLLATE DATABASE_DEFAULT)
            FROM sys.objects AS objects
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = objects.schema_id
            WHERE objects.type = N'U'
              AND ((schemas.name = N'planning' AND objects.name IN
                  (N'TravelerParticipations', N'AdventurePlanCreateResults'))
               OR objects.name LIKE N'%Companion%')
            ORDER BY schemas.name, objects.name, objects.type_desc;
            """);
        var permissions = await ReadStringsAsync(connection, """
            SELECT CONCAT(permissions.state_desc COLLATE DATABASE_DEFAULT, N'|',
                          permissions.permission_name COLLATE DATABASE_DEFAULT, N'|',
                          COALESCE(schemas.name COLLATE DATABASE_DEFAULT, N''), N'|',
                          COALESCE(objects.name COLLATE DATABASE_DEFAULT, N''))
            FROM sys.database_permissions AS permissions
            INNER JOIN sys.database_principals AS principals
                ON principals.principal_id = permissions.grantee_principal_id
            LEFT JOIN sys.objects AS objects
                ON permissions.class = 1 AND objects.object_id = permissions.major_id
            LEFT JOIN sys.schemas AS schemas
                ON (permissions.class = 1 AND schemas.schema_id = objects.schema_id)
                OR (permissions.class = 3 AND schemas.schema_id = permissions.major_id)
            WHERE principals.name = N'AdventuresSuiteCompanionReadRuntime'
            ORDER BY permissions.class, schemas.name, objects.name, permissions.permission_name;
            """);
        var planningPermissions = await ReadStringsAsync(connection, """
            SELECT CONCAT(permissions.state_desc COLLATE DATABASE_DEFAULT, N'|',
                          permissions.permission_name COLLATE DATABASE_DEFAULT, N'|',
                          COALESCE(schemas.name COLLATE DATABASE_DEFAULT, N''), N'|',
                          COALESCE(objects.name COLLATE DATABASE_DEFAULT, N''))
            FROM sys.database_permissions AS permissions
            INNER JOIN sys.database_principals AS principals
                ON principals.principal_id = permissions.grantee_principal_id
            LEFT JOIN sys.objects AS objects
                ON permissions.class = 1 AND objects.object_id = permissions.major_id
            LEFT JOIN sys.schemas AS schemas
                ON (permissions.class = 1 AND schemas.schema_id = objects.schema_id)
                OR (permissions.class = 3 AND schemas.schema_id = permissions.major_id)
            WHERE principals.name = N'AdventuresSuitePlanningRuntime'
            ORDER BY permissions.class, schemas.name, objects.name, permissions.permission_name;
            """);
        var policyPermissions = await ReadStringsAsync(connection, """
            SELECT CONCAT(permissions.state_desc COLLATE DATABASE_DEFAULT, N'|',
                          permissions.permission_name COLLATE DATABASE_DEFAULT, N'|',
                          COALESCE(schemas.name COLLATE DATABASE_DEFAULT, N''), N'|',
                          COALESCE(objects.name COLLATE DATABASE_DEFAULT, N''))
            FROM sys.database_permissions AS permissions
            INNER JOIN sys.database_principals AS principals
                ON principals.principal_id = permissions.grantee_principal_id
            LEFT JOIN sys.objects AS objects
                ON permissions.class = 1 AND objects.object_id = permissions.major_id
            LEFT JOIN sys.schemas AS schemas
                ON (permissions.class = 1 AND schemas.schema_id = objects.schema_id)
                OR (permissions.class = 3 AND schemas.schema_id = permissions.major_id)
            WHERE principals.name = N'AdventuresSuiteCompanionPolicyRuntime'
            ORDER BY permissions.class, schemas.name, objects.name, permissions.permission_name;
            """);
        var fingerprints = await ReadStringsAsync(connection, """
            SELECT CONCAT(N'planning.AdventurePlans|', COUNT_BIG(*), N'|',
                          COALESCE(CHECKSUM_AGG(BINARY_CHECKSUM(*)), 0))
            FROM planning.AdventurePlans
            UNION ALL
            SELECT CONCAT(N'planning.DestinationVisits|', COUNT_BIG(*), N'|',
                          COALESCE(CHECKSUM_AGG(BINARY_CHECKSUM(*)), 0))
            FROM planning.DestinationVisits
            UNION ALL
            SELECT CONCAT(N'auth.CreatorMemberships|', COUNT_BIG(*), N'|',
                          COALESCE(CHECKSUM_AGG(BINARY_CHECKSUM(*)), 0))
            FROM auth.CreatorMemberships
            UNION ALL
            SELECT CONCAT(N'auth.CreatorMembershipRoles|', COUNT_BIG(*), N'|',
                          COALESCE(CHECKSUM_AGG(BINARY_CHECKSUM(*)), 0))
            FROM auth.CreatorMembershipRoles
            UNION ALL
            SELECT CONCAT(N'auth.CreatorMembershipPermissionGrants|', COUNT_BIG(*), N'|',
                          COALESCE(CHECKSUM_AGG(BINARY_CHECKSUM(*)), 0))
            FROM auth.CreatorMembershipPermissionGrants
            ORDER BY 1;
            """);

        return new(
            journal,
            objects,
            permissions,
            planningPermissions,
            policyPermissions,
            fingerprints,
            Hash(fingerprints),
            await ObjectExistsAsync(connection, "planning.TravelerParticipations"),
            await PrincipalExistsAsync(connection, "AdventuresSuiteCompanionReadRuntime"),
            await ScalarAsync(connection, """
                SELECT COUNT(*)
                FROM sys.database_role_members AS memberships
                INNER JOIN sys.database_principals AS roles
                    ON roles.principal_id = memberships.role_principal_id
                WHERE roles.name = N'AdventuresSuiteCompanionReadRuntime';
                """),
            await ScalarAsync(connection, """
                SELECT COUNT(*)
                FROM sys.database_role_members AS memberships
                INNER JOIN sys.database_principals AS members
                    ON members.principal_id = memberships.member_principal_id
                WHERE members.name = N'AdventuresSuiteCompanionReadRuntime';
                """),
            await ScalarStringAsync(connection, """
                SELECT COALESCE(owners.name, N'')
                FROM sys.database_principals AS roles
                LEFT JOIN sys.database_principals AS owners
                    ON owners.principal_id = roles.owning_principal_id
                WHERE roles.name = N'AdventuresSuiteCompanionReadRuntime';
                """),
            await ScalarAsync(connection, """
                SELECT COUNT(*) FROM sys.objects
                WHERE parent_object_id = OBJECT_ID(N'planning.TravelerParticipations')
                  AND type IN (N'PK', N'UQ', N'F', N'C');
                """),
            await ScalarAsync(connection, """
                SELECT COUNT(*) FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'planning.TravelerParticipations')
                  AND name = N'IX_TravelerParticipations_AuthorizedList';
                """) == 1,
            await ObjectExistsAsync(connection, "planning.AdventurePlanCreateResults"),
            await PrincipalExistsAsync(connection, "AdventuresSuitePlanningRuntime"),
            await ScalarAsync(connection, """
                SELECT COUNT(*) FROM sys.database_role_members AS memberships
                INNER JOIN sys.database_principals AS roles
                    ON roles.principal_id = memberships.role_principal_id
                WHERE roles.name = N'AdventuresSuitePlanningRuntime';
                """),
            await ScalarAsync(connection, """
                SELECT COUNT(*) FROM sys.database_role_members AS memberships
                INNER JOIN sys.database_principals AS members
                    ON members.principal_id = memberships.member_principal_id
                WHERE members.name = N'AdventuresSuitePlanningRuntime';
                """),
            await ScalarStringAsync(connection, """
                SELECT COALESCE(owners.name, N'') FROM sys.database_principals AS roles
                LEFT JOIN sys.database_principals AS owners
                    ON owners.principal_id = roles.owning_principal_id
                WHERE roles.name = N'AdventuresSuitePlanningRuntime';
                """),
            await ScalarAsync(connection, """
                SELECT COUNT(*) FROM sys.objects
                WHERE parent_object_id = OBJECT_ID(N'planning.AdventurePlanCreateResults')
                  AND type IN (N'PK', N'UQ', N'F', N'C');
                """),
            await ScalarAsync(connection, """
                SELECT COUNT(*) FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'planning.AdventurePlanCreateResults')
                  AND name = N'IX_AdventurePlanCreateResults_Expiry';
                """) == 1,
            await ObjectExistsAsync(connection, "planning.CompanionInformationPolicyAssignments"),
            await ObjectExistsAsync(connection, "audit.CompanionInformationPolicyAssignmentEvents"),
            await PrincipalExistsAsync(connection, "AdventuresSuiteCompanionPolicyRuntime"),
            await ScalarAsync(connection, """
                SELECT COUNT(*) FROM sys.database_role_members AS memberships
                INNER JOIN sys.database_principals AS roles
                    ON roles.principal_id = memberships.role_principal_id
                WHERE roles.name = N'AdventuresSuiteCompanionPolicyRuntime';
                """),
            await ScalarAsync(connection, """
                SELECT COUNT(*) FROM sys.database_role_members AS memberships
                INNER JOIN sys.database_principals AS members
                    ON members.principal_id = memberships.member_principal_id
                WHERE members.name = N'AdventuresSuiteCompanionPolicyRuntime';
                """),
            await ScalarStringAsync(connection, """
                SELECT COALESCE(owners.name, N'') FROM sys.database_principals AS roles
                LEFT JOIN sys.database_principals AS owners
                    ON owners.principal_id = roles.owning_principal_id
                WHERE roles.name = N'AdventuresSuiteCompanionPolicyRuntime';
                """));
    }

    internal static MigrationJournalOutcome Classify(IReadOnlyList<string> journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        var catalog = MigrationCatalog.GetOrderedResourceNames(typeof(MigrationCatalog).Assembly);
        if (journal.SequenceEqual(catalog.Take(6), StringComparer.Ordinal)) return MigrationJournalOutcome.At0006;
        if (journal.SequenceEqual(catalog.Take(7), StringComparer.Ordinal)) return MigrationJournalOutcome.At0007;
        if (journal.SequenceEqual(catalog.Take(8), StringComparer.Ordinal)) return MigrationJournalOutcome.At0008;
        if (journal.SequenceEqual(catalog.Take(9), StringComparer.Ordinal)) return MigrationJournalOutcome.At0009;
        if (journal.SequenceEqual(catalog.Take(10), StringComparer.Ordinal)) return MigrationJournalOutcome.At0010;
        if (journal.SequenceEqual(catalog.Take(11), StringComparer.Ordinal)) return MigrationJournalOutcome.At0011;
        return MigrationJournalOutcome.Unexpected;
    }

    private static async Task<IReadOnlyList<string>> ReadStringsAsync(
        SqlConnection connection, string sql)
    {
        var values = new List<string>();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) values.Add(reader.GetString(0));
        return values.AsReadOnly();
    }

    private static async Task<bool> ObjectExistsAsync(SqlConnection connection, string name)
    {
        await using var command = new SqlCommand("SELECT CASE WHEN OBJECT_ID(@Name) IS NULL THEN 0 ELSE 1 END;", connection);
        command.Parameters.AddWithValue("@Name", name);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task<bool> PrincipalExistsAsync(SqlConnection connection, string name)
    {
        await using var command = new SqlCommand(
            "SELECT CASE WHEN DATABASE_PRINCIPAL_ID(@Name) IS NULL THEN 0 ELSE 1 END;", connection);
        command.Parameters.AddWithValue("@Name", name);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task<int> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<string> ScalarStringAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private static string Hash(IReadOnlyList<string> values) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values))));
}

internal sealed record MigrationStateEvidence(
    IReadOnlyList<string> Journal,
    IReadOnlyList<string> RelevantObjects,
    IReadOnlyList<string> CompanionPermissions,
    IReadOnlyList<string> PlanningPermissions,
    IReadOnlyList<string> PolicyPermissions,
    IReadOnlyList<string> ApplicationDataSignatures,
    string ApplicationFingerprint,
    bool TravelerParticipationsExists,
    bool CompanionRoleExists,
    int CompanionRoleMemberCount,
    int CompanionParentRoleCount,
    string CompanionRoleOwner,
    int TravelerConstraintCount,
    bool TravelerAuthorizedListIndexExists,
    bool AdventurePlanCreateResultsExists,
    bool PlanningRoleExists,
    int PlanningRoleMemberCount,
    int PlanningParentRoleCount,
    string PlanningRoleOwner,
    int AdventurePlanCreateResultConstraintCount,
    bool AdventurePlanCreateResultExpiryIndexExists,
    bool CompanionPolicyAssignmentsExists,
    bool CompanionPolicyAssignmentEventsExists,
    bool CompanionPolicyRoleExists,
    int CompanionPolicyRoleMemberCount,
    int CompanionPolicyParentRoleCount,
    string CompanionPolicyRoleOwner);

internal enum MigrationJournalOutcome
{
    At0006,
    At0007,
    At0008,
    At0009,
    At0010,
    At0011,
    Unexpected
}
