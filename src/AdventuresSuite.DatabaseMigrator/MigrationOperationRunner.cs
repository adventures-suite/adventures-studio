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

        var credential = new ManagedIdentityCredential(
            ManagedIdentityId.FromUserAssignedClientId(clientId.ToString()));
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(["https://database.windows.net/.default"]));
        var identity = await MigrationIdentityValidator.ValidateAsync(
            token, connectionString, tenantId, objectId, clientId, principalName, server, database);
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
            // This operational command remains pinned to its separately reviewed 0006 -> 0008 scope.
            selectedScripts = DatabaseMigratorRunner.MigrateWithLockHeld(
                connectionString,
                maximumMigrationNumber: "0008");
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
        if (migrationFailure is null && afterOutcome == MigrationJournalOutcome.At0008
            && VerifyExpectedPostState(after))
            return MigrationOperationClassification.Complete;
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
            || !state.RelevantObjects.SequenceEqual(
                ["planning.TravelerParticipations|USER_TABLE"], StringComparer.Ordinal))
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
        return expectedPermissions.SetEquals(state.CompanionPermissions);
    }

    private static bool VerifyExpected0006State(MigrationStateEvidence state) =>
        !state.TravelerParticipationsExists
        && !state.CompanionRoleExists
        && state.RelevantObjects.Count == 0
        && state.CompanionPermissions.Count == 0;

    private static bool VerifyExpected0007State(MigrationStateEvidence state) =>
        state.TravelerParticipationsExists
        && state.TravelerConstraintCount == 7
        && state.TravelerAuthorizedListIndexExists
        && !state.CompanionRoleExists
        && state.CompanionPermissions.Count == 0
        && state.RelevantObjects.SequenceEqual(
            ["planning.TravelerParticipations|USER_TABLE"], StringComparer.Ordinal);

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
            state.ApplicationDataSignatures,
            state.ApplicationFingerprint,
            state.TravelerParticipationsExists,
            state.CompanionRoleExists,
            state.CompanionRoleMemberCount,
            state.CompanionParentRoleCount,
            state.CompanionRoleOwner,
            state.TravelerConstraintCount,
            state.TravelerAuthorizedListIndexExists
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
    Migration0007Committed,
    NoScriptCommitted,
    Unexpected
}
