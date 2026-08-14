using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;

namespace AdventuresSuite.DatabaseMigrator;

/// <summary>Runs the approval-gated migration operation with bounded, sanitized evidence.</summary>
internal static partial class MigrationOperationRunner
{
    internal static async Task<int> RunAsync(string connectionString)
    {
        var operationId = Require("ADVENTURESSUITE_MIGRATION_OPERATION_ID");
        if (!OperationIdPattern().IsMatch(operationId))
            throw new InvalidOperationException("The migration operation identifier is invalid.");
        var releaseSha = RequireHex("ADVENTURESSUITE_RELEASE_SHA", 40);
        var packageSha256 = RequireHex("ADVENTURESSUITE_MIGRATION_PACKAGE_SHA256", 64);
        var catalogSha256 = RequireHex("ADVENTURESSUITE_MIGRATION_CATALOG_SHA256", 64);
        if (!string.Equals(catalogSha256,
            MigrationCatalog.CalculateOrderedCatalogSha256(typeof(MigrationCatalog).Assembly),
            StringComparison.Ordinal))
            throw new InvalidOperationException("The migration catalog checksum does not match the embedded catalog.");
        var tenantId = RequireGuid("ADVENTURESSUITE_MIGRATION_TENANT_ID");
        var objectId = RequireGuid("ADVENTURESSUITE_MIGRATION_PRINCIPAL_ID");
        var clientId = RequireGuid("ADVENTURESSUITE_MIGRATION_PRINCIPAL_CLIENT_ID");
        var principalName = Require("ADVENTURESSUITE_MIGRATION_PRINCIPAL_NAME");
        var server = Require("ADVENTURESSUITE_SQL_SERVER");
        var database = Require("ADVENTURESSUITE_SQL_DATABASE");

        var startedAt = DateTimeOffset.UtcNow;
        WriteEvidence(new
        {
            eventName = "migration-operation-started",
            operationId,
            startedAt,
            releaseSha,
            packageSha256,
            orderedMigrationCatalogSha256 = catalogSha256,
            orderedCatalog = MigrationCatalog.GetOrderedResourceNames(typeof(MigrationCatalog).Assembly)
        });

        var selection = MigrationCredentialFactory.Create(tenantId, clientId);
        var token = await selection.Credential.GetTokenAsync(
            new TokenRequestContext(["https://database.windows.net/.default"]),
            CancellationToken.None);
        var identity = await MigrationIdentityValidator.ValidateAsync(
            token, connectionString, tenantId, objectId, clientId, principalName, server, database,
            selection.Mode);
        WriteEvidence(new
        {
            eventName = "migration-identity-verified",
            operationId,
            identity.TenantId,
            identity.ObjectId,
            identity.ClientId,
            identity.SqlPrincipalAlias,
            identity.Server,
            identity.Database,
            tokenExpiresAt = token.ExpiresOn
        });

        using var migrationLock = DatabaseMigratorRunner.AcquireMigrationLock(connectionString);
        var before = await MigrationOperationalState.CaptureAsync(connectionString);
        var beforeOutcome = MigrationOperationalState.Classify(before.Journal);
        WriteState(operationId, "pre-migration-state", before, beforeOutcome);
        if (beforeOutcome != MigrationJournalOutcome.At0006
            || before.TravelerParticipationsExists
            || before.CompanionRoleExists
            || before.RelevantObjects.Count != 0
            || before.CompanionPermissions.Count != 0)
            throw new InvalidOperationException("The pre-migration database state is not the approved 0006 baseline.");

        Exception? migrationFailure = null;
        IReadOnlyList<string> selectedScripts = [];
        try
        {
            // This operation is explicitly bounded to the reviewed 0006 -> 0009 transition.
            selectedScripts = DatabaseMigratorRunner.MigrateWithLockHeld(
                connectionString,
                maximumMigrationNumber: "0009");
        }
        catch (Exception exception)
        {
            migrationFailure = exception;
        }

        var after = await MigrationOperationalState.CaptureAsync(connectionString);
        var afterOutcome = MigrationOperationalState.Classify(after.Journal);
        WriteState(operationId, "post-migration-state", after, afterOutcome);
        var classification = ClassifyResult(before, after, afterOutcome, migrationFailure);
        var exitCode = classification == MigrationOperationClassification.Complete ? 0 : 1;
        WriteEvidence(new
        {
            eventName = "migration-operation-completed",
            operationId,
            completedAt = DateTimeOffset.UtcNow,
            releaseSha,
            packageSha256,
            orderedMigrationCatalogSha256 = catalogSha256,
            selectedScripts,
            classification = classification.ToString(),
            exitCode
        });
        MigrationExecutionModes.WriteCompletionEnvelope(
            operationId, releaseSha, packageSha256, catalogSha256, startedAt,
            classification.ToString(), exitCode, new
            {
                journalClassification = afterOutcome.ToString(),
                schemaAndPermissionsVerified = VerifyExpectedPostState(after),
                fingerprintMatched = string.Equals(
                    before.ApplicationFingerprint, after.ApplicationFingerprint, StringComparison.Ordinal)
            });
        return exitCode;
    }

    internal static MigrationOperationClassification ClassifyResult(
        MigrationStateEvidence before,
        MigrationStateEvidence after,
        MigrationJournalOutcome afterOutcome,
        Exception? migrationFailure)
    {
        if (!string.Equals(before.ApplicationFingerprint, after.ApplicationFingerprint, StringComparison.Ordinal))
            return MigrationOperationClassification.Unexpected;
        if (migrationFailure is null && afterOutcome == MigrationJournalOutcome.At0009
            && VerifyExpectedPostState(after))
            return MigrationOperationClassification.Complete;
        if (migrationFailure is not null && afterOutcome == MigrationJournalOutcome.At0008
            && VerifyExpected0008State(after))
            return MigrationOperationClassification.Migration0008Committed;
        if (migrationFailure is not null && afterOutcome == MigrationJournalOutcome.At0007
            && VerifyExpected0007State(after))
            return MigrationOperationClassification.Migration0007Committed;
        if (migrationFailure is not null && afterOutcome == MigrationJournalOutcome.At0006
            && VerifyExpected0006State(after))
            return MigrationOperationClassification.NoScriptCommitted;
        return MigrationOperationClassification.Unexpected;
    }

    internal static bool VerifyExpectedPostState(MigrationStateEvidence state)
    {
        if (!state.TravelerParticipationsExists
            || state.TravelerConstraintCount != 7
            || !state.TravelerAuthorizedListIndexExists
            || !state.CompanionRoleExists
            || state.CompanionRoleMemberCount != 0
            || state.CompanionParentRoleCount != 0
            || !string.Equals(state.CompanionRoleOwner, "dbo", StringComparison.Ordinal)
            || !state.AdventurePlanCreateResultsExists
            || !state.PlanningRoleExists
            || state.PlanningRoleMemberCount != 0
            || state.PlanningParentRoleCount != 0
            || !string.Equals(state.PlanningRoleOwner, "dbo", StringComparison.Ordinal)
            || state.AdventurePlanCreateResultConstraintCount != 7
            || !state.AdventurePlanCreateResultExpiryIndexExists
            || !state.RelevantObjects.SequenceEqual(
                ["planning.AdventurePlanCreateResults|USER_TABLE",
                 "planning.TravelerParticipations|USER_TABLE"], StringComparer.Ordinal))
            return false;

        var expectedPermissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in new[]
        {
            "planning|AdventurePlans", "planning|TravelerParticipations", "planning|DestinationVisits",
            "auth|CreatorMemberships", "auth|CreatorMembershipRoles",
            "auth|CreatorMembershipPermissionGrants"
        })
        {
            var parts = target.Split('|');
            expectedPermissions.Add($"GRANT|SELECT|{parts[0]}|{parts[1]}");
            foreach (var permission in new[] { "INSERT", "UPDATE", "DELETE" })
                expectedPermissions.Add($"DENY|{permission}|{parts[0]}|{parts[1]}");
        }
        expectedPermissions.Add("DENY|ALTER|auth|");
        expectedPermissions.Add("DENY|ALTER|planning|");
        return expectedPermissions.SetEquals(state.CompanionPermissions)
            && new HashSet<string>(StringComparer.Ordinal)
            {
                "GRANT|INSERT|planning|AdventurePlanCreateResults",
                "GRANT|SELECT|planning|AdventurePlanCreateResults",
                "DENY|UPDATE|planning|AdventurePlanCreateResults",
                "DENY|DELETE|planning|AdventurePlanCreateResults",
                "DENY|ALTER|planning|"
            }.SetEquals(state.PlanningPermissions);
    }

    private static bool VerifyExpected0006State(MigrationStateEvidence state) =>
        !state.TravelerParticipationsExists
        && !state.CompanionRoleExists
        && !state.AdventurePlanCreateResultsExists
        && !state.PlanningRoleExists
        && state.RelevantObjects.Count == 0
        && state.CompanionPermissions.Count == 0
        && state.PlanningPermissions.Count == 0;

    private static bool VerifyExpected0007State(MigrationStateEvidence state) =>
        state.TravelerParticipationsExists
        && state.TravelerConstraintCount == 7
        && state.TravelerAuthorizedListIndexExists
        && !state.CompanionRoleExists
        && !state.AdventurePlanCreateResultsExists
        && !state.PlanningRoleExists
        && state.CompanionPermissions.Count == 0
        && state.PlanningPermissions.Count == 0
        && state.RelevantObjects.SequenceEqual(
            ["planning.TravelerParticipations|USER_TABLE"], StringComparer.Ordinal);

    private static bool VerifyExpected0008State(MigrationStateEvidence state) =>
        state.TravelerParticipationsExists
        && state.TravelerConstraintCount == 7
        && state.TravelerAuthorizedListIndexExists
        && state.CompanionRoleExists
        && state.CompanionRoleMemberCount == 0
        && state.CompanionParentRoleCount == 0
        && string.Equals(state.CompanionRoleOwner, "dbo", StringComparison.Ordinal)
        && !state.AdventurePlanCreateResultsExists
        && !state.PlanningRoleExists
        && state.PlanningPermissions.Count == 0
        && state.RelevantObjects.SequenceEqual(
            ["planning.TravelerParticipations|USER_TABLE"], StringComparer.Ordinal)
        && VerifyCompanionPermissions(state.CompanionPermissions);

    private static bool VerifyCompanionPermissions(IReadOnlyList<string> actual)
    {
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in new[]
        {
            "planning|AdventurePlans", "planning|TravelerParticipations", "planning|DestinationVisits",
            "auth|CreatorMemberships", "auth|CreatorMembershipRoles",
            "auth|CreatorMembershipPermissionGrants"
        })
        {
            var parts = target.Split('|');
            expected.Add($"GRANT|SELECT|{parts[0]}|{parts[1]}");
            foreach (var permission in new[] { "INSERT", "UPDATE", "DELETE" })
                expected.Add($"DENY|{permission}|{parts[0]}|{parts[1]}");
        }
        expected.Add("DENY|ALTER|auth|");
        expected.Add("DENY|ALTER|planning|");
        return expected.SetEquals(actual);
    }

    private static void WriteState(
        string operationId, string eventName, MigrationStateEvidence state, MigrationJournalOutcome outcome) =>
        WriteEvidence(new
        {
            eventName,
            operationId,
            outcome = outcome.ToString(),
            state.Journal,
            state.RelevantObjects,
            state.CompanionPermissions,
            state.PlanningPermissions,
            state.ApplicationDataSignatures,
            state.ApplicationFingerprint,
            state.TravelerParticipationsExists,
            state.CompanionRoleExists,
            state.CompanionRoleMemberCount,
            state.CompanionParentRoleCount,
            state.CompanionRoleOwner,
            state.TravelerConstraintCount,
            state.TravelerAuthorizedListIndexExists,
            state.AdventurePlanCreateResultsExists,
            state.PlanningRoleExists,
            state.PlanningRoleMemberCount,
            state.PlanningParentRoleCount,
            state.PlanningRoleOwner,
            state.AdventurePlanCreateResultConstraintCount,
            state.AdventurePlanCreateResultExpiryIndexExists
        });

    private static void WriteEvidence(object value) =>
        Console.WriteLine(JsonSerializer.Serialize(value));

    private static string Require(string name) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))
            ? Environment.GetEnvironmentVariable(name)!.Trim()
            : throw new InvalidOperationException($"Set {name} for the reviewed migration operation.");

    private static Guid RequireGuid(string name) =>
        Guid.TryParse(Require(name), out var value)
            ? value : throw new InvalidOperationException($"Set a valid {name} value.");

    private static string RequireHex(string name, int length)
    {
        var value = Require(name);
        return value.Length == length && value.All(Uri.IsHexDigit)
            ? value.ToLowerInvariant()
            : throw new InvalidOperationException($"Set a valid {name} value.");
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{7,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex OperationIdPattern();

}

internal enum MigrationOperationClassification
{
    Complete,
    Migration0008Committed,
    Migration0007Committed,
    NoScriptCommitted,
    Unexpected
}
