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

        var connectionFactory = MigrationSqlConnectionFactory.Create(
            connectionString, selection.Mode, token);
        using var migrationLock = DatabaseMigratorRunner.AcquireMigrationLock(
            connectionFactory.CreateConnection);
        var before = await MigrationOperationalState.CaptureAsync(
            connectionFactory.CreateConnection);
        var beforeOutcome = MigrationOperationalState.Classify(before.Journal);
        WriteState(operationId, "pre-migration-state", before, beforeOutcome);
        ValidatePreMigrationState(before, beforeOutcome);

        await VerifyPermissionsBeforeMigrationAsync(
            connectionFactory.CreateConnection, operationId);

        Exception? migrationFailure = null;
        IReadOnlyList<string> selectedScripts = [];
        try
        {
            // This operation is bounded to the reviewed transition through 0014,
            // including exact repair-forward baselines at 0012 or 0013.
            selectedScripts = DatabaseMigratorRunner.MigrateWithLockHeld(
                connectionFactory.CreateConnection,
                maximumMigrationNumber: "0014");
        }
        catch (Exception exception)
        {
            migrationFailure = exception;
        }

        var after = await MigrationOperationalState.CaptureAsync(
            connectionFactory.CreateConnection);
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

    /// <summary>Fails before DbUp selection when the exact temporary catalog is unavailable.</summary>
    internal static async Task VerifyPermissionsBeforeMigrationAsync(
        Func<Microsoft.Data.SqlClient.SqlConnection> connectionFactory,
        string operationId)
    {
        try
        {
            await AzureDevelopmentBootstrapper.VerifyMigrationPermissionsAsync(connectionFactory);
            WriteEvidence(new
            {
                eventName = "migration-permissions-verified",
                operationId,
                exactCatalogVerified = true
            });
        }
        catch
        {
            WriteEvidence(new
            {
                eventName = "migration-permissions-rejected",
                operationId,
                exactCatalogVerified = false
            });
            throw new InvalidOperationException(
                "The exact temporary migration permission catalog is unavailable.");
        }
    }

    internal static MigrationOperationClassification ClassifyResult(
        MigrationStateEvidence before,
        MigrationStateEvidence after,
        MigrationJournalOutcome afterOutcome,
        Exception? migrationFailure)
    {
        if (!string.Equals(before.ApplicationFingerprint, after.ApplicationFingerprint, StringComparison.Ordinal))
            return MigrationOperationClassification.Unexpected;
        if (migrationFailure is null && afterOutcome == MigrationJournalOutcome.At0014
            && VerifyExpectedPostState(after))
            return MigrationOperationClassification.Complete;
        if (migrationFailure is not null && afterOutcome == MigrationJournalOutcome.At0013
            && VerifyExpected0013State(after))
            return MigrationOperationClassification.Migration0013Committed;
        if (migrationFailure is not null && afterOutcome == MigrationJournalOutcome.At0012
            && VerifyExpected0012State(after))
            return MigrationOperationClassification.Migration0012Committed;
        if (migrationFailure is not null && afterOutcome == MigrationJournalOutcome.At0011
            && VerifyExpected0011State(after))
            return MigrationOperationClassification.Migration0011Committed;
        if (migrationFailure is not null && afterOutcome == MigrationJournalOutcome.At0010
            && VerifyExpected0010State(after))
            return MigrationOperationClassification.Migration0010Committed;
        if (migrationFailure is not null && afterOutcome == MigrationJournalOutcome.At0009
            && VerifyExpected0009PrerequisiteState(after))
            return MigrationOperationClassification.NoScriptCommitted;
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

    /// <summary>Requires an exact reviewed 0009, 0012, or 0013 state.</summary>
    internal static void ValidatePreMigrationState(
        MigrationStateEvidence state,
        MigrationJournalOutcome outcome)
    {
        var approved = outcome switch
        {
            MigrationJournalOutcome.At0009 => VerifyExpected0009PrerequisiteState(state),
            MigrationJournalOutcome.At0012 => VerifyExpected0012State(state),
            MigrationJournalOutcome.At0013 => VerifyExpected0013State(state),
            _ => false
        };
        if (!approved)
            throw new InvalidOperationException(
                "The pre-migration database state is not an approved migration baseline.");
    }

    private static bool VerifyExpected0009PrerequisiteState(MigrationStateEvidence state) =>
        !state.CompanionPolicyAssignmentsExists
        && !state.CompanionPolicyAssignmentEventsExists
        && state.CompanionPolicyRoleExists
        && state.CompanionPolicyRoleMemberCount == 0
        && state.CompanionPolicyParentRoleCount == 0
        && string.Equals(state.CompanionPolicyRoleOwner, "dbo", StringComparison.Ordinal)
        && state.PolicyPermissions.Count == 0
        && VerifyExpected0009State(state);

    internal static bool VerifyExpectedBootstrapped0006State(MigrationStateEvidence state) =>
        !state.TravelerParticipationsExists
        && state.TravelerConstraintCount == 0
        && !state.TravelerAuthorizedListIndexExists
        && !state.AdventurePlanCreateResultsExists
        && state.AdventurePlanCreateResultConstraintCount == 0
        && !state.AdventurePlanCreateResultExpiryIndexExists
        && state.CompanionRoleExists
        && state.CompanionRoleMemberCount == 0
        && state.CompanionParentRoleCount == 0
        && string.Equals(state.CompanionRoleOwner, "dbo", StringComparison.Ordinal)
        && state.PlanningRoleExists
        && state.PlanningRoleMemberCount == 0
        && state.PlanningParentRoleCount == 0
        && string.Equals(state.PlanningRoleOwner, "dbo", StringComparison.Ordinal)
        && state.RelevantObjects.Count == 0
        && state.CompanionPermissions.Count == 0
        && state.PlanningPermissions.Count == 0;

    internal static bool VerifyExpected0010State(MigrationStateEvidence state)
    {
        if (!VerifyExpected0009State(state)
            || !state.CompanionPolicyAssignmentsExists
            || !state.CompanionPolicyAssignmentEventsExists
            || !state.CompanionPolicyRoleExists
            || state.CompanionPolicyRoleMemberCount != 0
            || state.CompanionPolicyParentRoleCount != 0
            || !string.Equals(state.CompanionPolicyRoleOwner, "dbo", StringComparison.Ordinal)
            || !state.RelevantObjects.SequenceEqual(
                ["audit.CompanionInformationPolicyAssignmentEvents|USER_TABLE",
                 "planning.AdventurePlanCreateResults|USER_TABLE",
                 "planning.CompanionInformationPolicyAssignments|USER_TABLE",
                 "planning.TravelerParticipations|USER_TABLE"], StringComparer.Ordinal))
            return false;

        return new HashSet<string>(StringComparer.Ordinal)
        {
            "GRANT|INSERT|audit|AuditEvents",
            "DENY|UPDATE|audit|AuditEvents",
            "DENY|DELETE|audit|AuditEvents",
            "GRANT|INSERT|audit|CompanionInformationPolicyAssignmentEvents",
            "DENY|UPDATE|audit|CompanionInformationPolicyAssignmentEvents",
            "DENY|DELETE|audit|CompanionInformationPolicyAssignmentEvents",
            "GRANT|SELECT|planning|AdventurePlans",
            "GRANT|SELECT|planning|TravelerParticipations",
            "GRANT|SELECT|planning|CompanionInformationPolicyAssignments",
            "GRANT|INSERT|planning|CompanionInformationPolicyAssignments",
            "GRANT|UPDATE|planning|CompanionInformationPolicyAssignments",
            "DENY|DELETE|planning|CompanionInformationPolicyAssignments",
            "DENY|ALTER|audit|",
            "DENY|ALTER|planning|"
        }.SetEquals(state.PolicyPermissions)
            && ExpectedPlanningPermissions().SetEquals(state.PlanningPermissions);
    }

    internal static bool VerifyExpected0011State(MigrationStateEvidence state) =>
        VerifyExpected0010Authority(state)
        && state.AdventurePlanTemplateOriginsExists
        && state.AdventurePlanTemplateOriginConstraintCount == 9
        && state.AdventurePlanTemplateOriginIndexExists
        && !state.PlannerFootStepApplicationsExists
        && state.PlannerFootStepApplicationConstraintCount == 0
        && state.PlannerFootStepApplicationIndexCount == 0
        && state.RelevantObjects.SequenceEqual(
            ["audit.CompanionInformationPolicyAssignmentEvents|USER_TABLE",
             "planning.AdventurePlanCreateResults|USER_TABLE",
             "planning.AdventurePlanTemplateOrigins|USER_TABLE",
             "planning.CompanionInformationPolicyAssignments|USER_TABLE",
             "planning.TravelerParticipations|USER_TABLE"], StringComparer.Ordinal)
        && ExpectedPlanningPermissions(includeTemplateOrigins: true)
            .SetEquals(state.PlanningPermissions);

    internal static bool VerifyExpected0012State(MigrationStateEvidence state) =>
        VerifyExpected0012Structure(state)
        && NoDestinationPlanItemLinks(state)
        && ExpectedPlanningPermissions(includeTemplateOrigins: true, includeFootStepApplications: true)
            .SetEquals(state.PlanningPermissions);

    private static bool VerifyExpected0012Structure(MigrationStateEvidence state) =>
        VerifyExpected0010Authority(state)
        && state.AdventurePlanTemplateOriginsExists
        && state.AdventurePlanTemplateOriginConstraintCount == 9
        && state.AdventurePlanTemplateOriginIndexExists
        && state.PlannerFootStepApplicationsExists
        && state.PlannerFootStepApplicationConstraintCount == 11
        && state.PlannerFootStepApplicationIndexCount == 2
        && state.RelevantObjects.SequenceEqual(
            ["audit.CompanionInformationPolicyAssignmentEvents|USER_TABLE",
             "planning.AdventurePlanCreateResults|USER_TABLE",
             "planning.AdventurePlanTemplateOrigins|USER_TABLE",
             "planning.CompanionInformationPolicyAssignments|USER_TABLE",
             "planning.PlannerFootStepApplications|USER_TABLE",
             "planning.TravelerParticipations|USER_TABLE"], StringComparer.Ordinal);

    /// <summary>Requires the exact schema and least-privilege state through migration 0013.</summary>
    internal static bool VerifyExpected0013State(MigrationStateEvidence state) =>
        VerifyExpected0012Structure(state)
        && ExpectedPlanningPermissions(includeTemplateOrigins: true, includeFootStepApplications: true,
            includeSchemaRuntimePermissions: true).SetEquals(state.PlanningPermissions)
        && NoDestinationPlanItemLinks(state);

    /// <summary>Requires the exact schema and least-privilege state through migration 0014.</summary>
    internal static bool VerifyExpectedPostState(MigrationStateEvidence state) =>
        VerifyExpected0012Structure(state)
        && ExpectedPlanningPermissions(includeTemplateOrigins: true, includeFootStepApplications: true,
            includeSchemaRuntimePermissions: true).SetEquals(state.PlanningPermissions)
        && state.DestinationPlanItemLinkColumnCount == 4
        && state.DestinationPlanItemLinkForeignKeyCount == 4
        && state.DestinationPlanItemLinkIndexCount == 4;

    private static bool NoDestinationPlanItemLinks(MigrationStateEvidence state) =>
        state.DestinationPlanItemLinkColumnCount == 0
        && state.DestinationPlanItemLinkForeignKeyCount == 0
        && state.DestinationPlanItemLinkIndexCount == 0;

    private static bool VerifyExpected0010Authority(MigrationStateEvidence state) =>
        state.TravelerParticipationsExists
        && state.TravelerConstraintCount == 7
        && state.TravelerAuthorizedListIndexExists
        && state.CompanionRoleExists
        && state.CompanionRoleMemberCount == 0
        && state.CompanionParentRoleCount == 0
        && string.Equals(state.CompanionRoleOwner, "dbo", StringComparison.Ordinal)
        && state.AdventurePlanCreateResultsExists
        && state.AdventurePlanCreateResultConstraintCount == 7
        && state.AdventurePlanCreateResultExpiryIndexExists
        && state.PlanningRoleExists
        && state.PlanningRoleMemberCount == 0
        && state.PlanningParentRoleCount == 0
        && string.Equals(state.PlanningRoleOwner, "dbo", StringComparison.Ordinal)
        && state.CompanionPolicyAssignmentsExists
        && state.CompanionPolicyAssignmentEventsExists
        && state.CompanionPolicyRoleExists
        && state.CompanionPolicyRoleMemberCount == 0
        && state.CompanionPolicyParentRoleCount == 0
        && string.Equals(state.CompanionPolicyRoleOwner, "dbo", StringComparison.Ordinal)
        && new HashSet<string>(StringComparer.Ordinal)
        {
            "GRANT|INSERT|audit|AuditEvents",
            "DENY|UPDATE|audit|AuditEvents",
            "DENY|DELETE|audit|AuditEvents",
            "GRANT|INSERT|audit|CompanionInformationPolicyAssignmentEvents",
            "DENY|UPDATE|audit|CompanionInformationPolicyAssignmentEvents",
            "DENY|DELETE|audit|CompanionInformationPolicyAssignmentEvents",
            "GRANT|SELECT|planning|AdventurePlans",
            "GRANT|SELECT|planning|TravelerParticipations",
            "GRANT|SELECT|planning|CompanionInformationPolicyAssignments",
            "GRANT|INSERT|planning|CompanionInformationPolicyAssignments",
            "GRANT|UPDATE|planning|CompanionInformationPolicyAssignments",
            "DENY|DELETE|planning|CompanionInformationPolicyAssignments",
            "DENY|ALTER|audit|",
            "DENY|ALTER|planning|"
        }.SetEquals(state.PolicyPermissions)
        && VerifyCompanionPermissions(state.CompanionPermissions, includePolicyAssignment: true);

    private static HashSet<string> ExpectedPlanningPermissions(
        bool includeTemplateOrigins = false,
        bool includeFootStepApplications = false,
        bool includeSchemaRuntimePermissions = false)
    {
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "GRANT|INSERT|planning|AdventurePlanCreateResults",
            "GRANT|SELECT|planning|AdventurePlanCreateResults",
            "DENY|UPDATE|planning|AdventurePlanCreateResults",
            "DENY|DELETE|planning|AdventurePlanCreateResults",
            "DENY|ALTER|planning|"
        };
        if (includeTemplateOrigins)
        {
            expected.Add("GRANT|INSERT|planning|AdventurePlanTemplateOrigins");
            expected.Add("GRANT|SELECT|planning|AdventurePlanTemplateOrigins");
            expected.Add("DENY|UPDATE|planning|AdventurePlanTemplateOrigins");
            expected.Add("DENY|DELETE|planning|AdventurePlanTemplateOrigins");
        }
        if (includeFootStepApplications)
        {
            expected.Add("GRANT|INSERT|planning|PlannerFootStepApplications");
            expected.Add("GRANT|SELECT|planning|PlannerFootStepApplications");
            expected.Add("DENY|UPDATE|planning|PlannerFootStepApplications");
            expected.Add("DENY|DELETE|planning|PlannerFootStepApplications");
        }
        if (includeSchemaRuntimePermissions)
        {
            expected.Add("GRANT|SELECT|planning|");
            expected.Add("GRANT|INSERT|planning|");
            expected.Add("GRANT|UPDATE|planning|");
            expected.Add("DENY|DELETE|planning|");
        }
        return expected;
    }

    internal static bool VerifyExpected0009State(MigrationStateEvidence state)
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
            || state.AdventurePlanTemplateOriginsExists
            || state.AdventurePlanTemplateOriginConstraintCount != 0
            || state.AdventurePlanTemplateOriginIndexExists
            || state.PlannerFootStepApplicationsExists
            || state.PlannerFootStepApplicationConstraintCount != 0
            || state.PlannerFootStepApplicationIndexCount != 0
            || state.DestinationPlanItemLinkColumnCount != 0
            || state.DestinationPlanItemLinkForeignKeyCount != 0
            || state.DestinationPlanItemLinkIndexCount != 0
            || !state.RelevantObjects.Where(value =>
                    !value.Contains("CompanionInformationPolicy", StringComparison.Ordinal))
                .SequenceEqual(
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
        expectedPermissions.Add("GRANT|SELECT|planning|CompanionInformationPolicyAssignments");
        expectedPermissions.Add("DENY|INSERT|planning|CompanionInformationPolicyAssignments");
        expectedPermissions.Add("DENY|UPDATE|planning|CompanionInformationPolicyAssignments");
        expectedPermissions.Add("DENY|DELETE|planning|CompanionInformationPolicyAssignments");
        if (!state.CompanionPolicyAssignmentsExists)
        {
            expectedPermissions.Remove("GRANT|SELECT|planning|CompanionInformationPolicyAssignments");
            expectedPermissions.Remove("DENY|INSERT|planning|CompanionInformationPolicyAssignments");
            expectedPermissions.Remove("DENY|UPDATE|planning|CompanionInformationPolicyAssignments");
            expectedPermissions.Remove("DENY|DELETE|planning|CompanionInformationPolicyAssignments");
        }
        return expectedPermissions.SetEquals(state.CompanionPermissions)
            && ExpectedPlanningPermissions().SetEquals(state.PlanningPermissions);
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

    private static bool VerifyCompanionPermissions(
        IReadOnlyList<string> actual,
        bool includePolicyAssignment = false)
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
        if (includePolicyAssignment)
        {
            expected.Add("GRANT|SELECT|planning|CompanionInformationPolicyAssignments");
            expected.Add("DENY|INSERT|planning|CompanionInformationPolicyAssignments");
            expected.Add("DENY|UPDATE|planning|CompanionInformationPolicyAssignments");
            expected.Add("DENY|DELETE|planning|CompanionInformationPolicyAssignments");
        }
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
            state.PolicyPermissions,
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
            ,
            state.CompanionPolicyAssignmentsExists,
            state.CompanionPolicyAssignmentEventsExists,
            state.CompanionPolicyRoleExists,
            state.CompanionPolicyRoleMemberCount,
            state.CompanionPolicyParentRoleCount,
            state.CompanionPolicyRoleOwner,
            state.AdventurePlanTemplateOriginsExists,
            state.AdventurePlanTemplateOriginConstraintCount,
            state.AdventurePlanTemplateOriginIndexExists,
            state.PlannerFootStepApplicationsExists,
            state.PlannerFootStepApplicationConstraintCount,
            state.PlannerFootStepApplicationIndexCount,
            state.DestinationPlanItemLinkColumnCount,
            state.DestinationPlanItemLinkForeignKeyCount,
            state.DestinationPlanItemLinkIndexCount
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
    Migration0012Committed,
    Migration0013Committed,
    Migration0011Committed,
    Migration0010Committed,
    Migration0008Committed,
    Migration0007Committed,
    NoScriptCommitted,
    Unexpected
}
