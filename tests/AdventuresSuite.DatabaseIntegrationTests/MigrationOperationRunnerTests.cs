using AdventuresSuite.DatabaseMigrator;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Serializes tests that temporarily replace process-global console writers.</summary>
[CollectionDefinition(ConsoleCaptureCollection.Name, DisableParallelization = true)]
public sealed class ConsoleCaptureCollection
{
    internal const string Name = "Console capture";
}

/// <summary>Verifies approval-gated migration outcome classification.</summary>
[Collection(ConsoleCaptureCollection.Name)]
public sealed class MigrationOperationRunnerTests
{
    [Fact]
    public async Task PermissionRejectionEmitsOnlyBoundedEvidence()
    {
        const string sensitiveFailure = "sensitive-database-error-must-not-be-emitted";
        using var writer = new StringWriter();
        var original = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                MigrationOperationRunner.VerifyPermissionsBeforeMigrationAsync(
                    () => throw new InvalidOperationException(sensitiveFailure),
                    "permission-gate-test"));
            Assert.Equal(
                "The exact temporary migration permission catalog is unavailable.",
                exception.Message);
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Equal(
            "{\"eventName\":\"migration-permissions-rejected\",\"operationId\":\"permission-gate-test\",\"exactCatalogVerified\":false}",
            writer.ToString().Trim());
        Assert.DoesNotContain(sensitiveFailure, writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CompleteRequires0010SchemaPermissionsAndUnchangedFingerprint()
    {
        var before = State(MigrationJournalOutcome.At0009, fingerprint: "SAME", complete: true,
            policyPrerequisite: true);
        var after = State(MigrationJournalOutcome.At0010, fingerprint: "SAME", complete: true,
            policyComplete: true);

        Assert.Equal(
            MigrationOperationClassification.Complete,
            MigrationOperationRunner.ClassifyResult(before, after, MigrationJournalOutcome.At0010, null));
    }

    [Fact]
    public void FailureAfter0008IsReportedAsCommittedPartialProgress()
    {
        var before = State(MigrationJournalOutcome.At0006, fingerprint: "SAME");
        var after = State(MigrationJournalOutcome.At0008, fingerprint: "SAME", migration0008: true);

        Assert.Equal(
            MigrationOperationClassification.Migration0008Committed,
            MigrationOperationRunner.ClassifyResult(
                before, after, MigrationJournalOutcome.At0008, new InvalidOperationException()));
    }

    [Fact]
    public void FailureAfter0007IsReportedAsCommittedPartialProgress()
    {
        var before = State(MigrationJournalOutcome.At0006, fingerprint: "SAME");
        var after = State(MigrationJournalOutcome.At0007, fingerprint: "SAME", migration0007: true);

        Assert.Equal(
            MigrationOperationClassification.Migration0007Committed,
            MigrationOperationRunner.ClassifyResult(
                before, after, MigrationJournalOutcome.At0007, new InvalidOperationException()));
    }

    [Fact]
    public void FailureAt0006ReportsNoCommittedScript()
    {
        var before = State(MigrationJournalOutcome.At0006, fingerprint: "SAME");
        var after = State(MigrationJournalOutcome.At0006, fingerprint: "SAME");

        Assert.Equal(
            MigrationOperationClassification.NoScriptCommitted,
            MigrationOperationRunner.ClassifyResult(
                before, after, MigrationJournalOutcome.At0006, new InvalidOperationException()));
    }

    [Fact]
    public void SuccessfulDbUpAtAnyStateOtherThan0009FailsClosed()
    {
        var before = State(MigrationJournalOutcome.At0006, fingerprint: "SAME");
        foreach (var outcome in new[]
                 {
                     MigrationJournalOutcome.Unexpected,
                     MigrationJournalOutcome.At0008,
                     MigrationJournalOutcome.At0007,
                     MigrationJournalOutcome.At0006
                 })
        {
            var after = State(outcome, fingerprint: "SAME");
            Assert.Equal(
                MigrationOperationClassification.Unexpected,
                MigrationOperationRunner.ClassifyResult(before, after, outcome, null));
        }
    }

    [Fact]
    public void FingerprintChangeAlwaysFailsClosed()
    {
        var before = State(MigrationJournalOutcome.At0006, fingerprint: "BEFORE");
        var after = State(MigrationJournalOutcome.At0009, fingerprint: "AFTER", complete: true);

        Assert.Equal(
            MigrationOperationClassification.Unexpected,
            MigrationOperationRunner.ClassifyResult(before, after, MigrationJournalOutcome.At0009, null));
    }

    [Fact]
    public void Partial0008ResidueAt0007FailsClosed()
    {
        var before = State(MigrationJournalOutcome.At0006, fingerprint: "SAME");
        var residue = State(MigrationJournalOutcome.At0007, fingerprint: "SAME", complete: true);

        Assert.Equal(
            MigrationOperationClassification.Unexpected,
            MigrationOperationRunner.ClassifyResult(
                before, residue, MigrationJournalOutcome.At0007, new InvalidOperationException()));
    }

    [Fact]
    public void Partial0009ResidueAt0008FailsClosed()
    {
        var before = State(MigrationJournalOutcome.At0006, fingerprint: "SAME");
        var residue = State(MigrationJournalOutcome.At0009, fingerprint: "SAME", complete: true) with
        {
            Journal = Journal(8)
        };

        Assert.Equal(
            MigrationOperationClassification.Unexpected,
            MigrationOperationRunner.ClassifyResult(
                before, residue, MigrationJournalOutcome.At0008, new InvalidOperationException()));
    }

    [Fact]
    public void CompleteStateRejectsMissingOrUnexpectedPlanningPermissions()
    {
        var complete = State(MigrationJournalOutcome.At0009, fingerprint: "SAME", complete: true);
        Assert.False(MigrationOperationRunner.VerifyExpectedPostState(complete with
        {
            PlanningPermissions = complete.PlanningPermissions.Skip(1).ToArray()
        }));
        Assert.False(MigrationOperationRunner.VerifyExpectedPostState(complete with
        {
            PlanningPermissions = complete.PlanningPermissions.Append(
                "GRANT|UPDATE|planning|AdventurePlanCreateResults").ToArray()
        }));
    }

    [Fact]
    public void JournalClassifierAcceptsOnlyExactOrderedStates()
    {
        Assert.Equal(MigrationJournalOutcome.At0006,
            MigrationOperationalState.Classify(Journal(6)));
        Assert.Equal(MigrationJournalOutcome.At0007,
            MigrationOperationalState.Classify(Journal(7)));
        Assert.Equal(MigrationJournalOutcome.At0008,
            MigrationOperationalState.Classify(Journal(8)));
        Assert.Equal(MigrationJournalOutcome.At0009,
            MigrationOperationalState.Classify(Journal(9)));
        Assert.Equal(MigrationJournalOutcome.Unexpected,
            MigrationOperationalState.Classify(Journal(8).Reverse().ToArray()));
        Assert.Equal(MigrationJournalOutcome.Unexpected,
            MigrationOperationalState.Classify(Journal(9).Append("malformed").ToArray()));
        Assert.Equal(MigrationJournalOutcome.Unexpected,
            MigrationOperationalState.Classify(["x"]));
    }

    [Fact]
    public void Exact0009RequiresAuthorityFreePolicyRole()
    {
        var state = State(MigrationJournalOutcome.At0009, fingerprint: "SAME", complete: true,
            policyPrerequisite: true);

        MigrationOperationRunner.ValidatePreMigrationState(
            state, MigrationJournalOutcome.At0009);

        foreach (var malformed in new[]
                 {
                     state with { CompanionPolicyRoleExists = false, CompanionPolicyRoleOwner = string.Empty },
                     state with { CompanionPolicyRoleOwner = "substituted_owner" },
                     state with { CompanionPolicyRoleMemberCount = 1 },
                     state with { CompanionPolicyParentRoleCount = 1 },
                     state with { PolicyPermissions = ["GRANT|SELECT|planning|AdventurePlans"] }
                 })
        {
            Assert.Throws<InvalidOperationException>(() =>
                MigrationOperationRunner.ValidatePreMigrationState(
                    malformed, MigrationJournalOutcome.At0009));
        }

        Assert.Throws<InvalidOperationException>(() =>
            MigrationOperationRunner.ValidatePreMigrationState(
                state, MigrationJournalOutcome.At0008));
    }

    private static MigrationStateEvidence State(
        MigrationJournalOutcome outcome,
        string fingerprint,
        bool complete = false,
        bool migration0008 = false,
        bool migration0007 = false,
        bool policyPrerequisite = false,
        bool policyComplete = false) =>
        new(
            Journal(outcome switch
            {
                MigrationJournalOutcome.At0006 => 6,
                MigrationJournalOutcome.At0007 => 7,
                MigrationJournalOutcome.At0008 => 8,
                MigrationJournalOutcome.At0009 => 9,
                MigrationJournalOutcome.At0010 => 10,
                _ => 5
            }),
            policyComplete
                ? ["audit.CompanionInformationPolicyAssignmentEvents|USER_TABLE",
                    "planning.AdventurePlanCreateResults|USER_TABLE",
                    "planning.CompanionInformationPolicyAssignments|USER_TABLE",
                    "planning.TravelerParticipations|USER_TABLE"]
                : complete
                ? ["planning.AdventurePlanCreateResults|USER_TABLE", "planning.TravelerParticipations|USER_TABLE"]
                : migration0008 || migration0007 ? ["planning.TravelerParticipations|USER_TABLE"] : [],
            complete || migration0008 ? ExpectedPermissions(policyComplete) : [],
            complete ? ExpectedPlanningPermissions() : [],
            policyComplete ? ExpectedPolicyPermissions() : [],
            ["planning.AdventurePlans|0|0"],
            fingerprint,
            complete || migration0008 || migration0007,
            complete || migration0008,
            0,
            0,
            complete || migration0008 ? "dbo" : string.Empty,
            complete || migration0008 || migration0007 ? 7 : 0,
            complete || migration0008 || migration0007,
            complete,
            complete,
            0,
            0,
            complete ? "dbo" : string.Empty,
            complete ? 7 : 0,
            complete,
            policyComplete,
            policyComplete,
            policyPrerequisite || policyComplete,
            0,
            0,
            policyPrerequisite || policyComplete ? "dbo" : string.Empty);

    private static IReadOnlyList<string> Journal(int count) =>
        MigrationCatalog.GetOrderedResourceNames(typeof(MigrationCatalog).Assembly)
            .Take(count)
            .ToArray();

    private static IReadOnlyList<string> ExpectedPermissions(bool includePolicyAssignment = false)
    {
        var permissions = new List<string>();
        foreach (var target in new[]
        {
            "planning|AdventurePlans", "planning|TravelerParticipations", "planning|DestinationVisits",
            "auth|CreatorMemberships", "auth|CreatorMembershipRoles",
            "auth|CreatorMembershipPermissionGrants"
        })
        {
            var parts = target.Split('|');
            permissions.Add($"GRANT|SELECT|{parts[0]}|{parts[1]}");
            permissions.Add($"DENY|INSERT|{parts[0]}|{parts[1]}");
            permissions.Add($"DENY|UPDATE|{parts[0]}|{parts[1]}");
            permissions.Add($"DENY|DELETE|{parts[0]}|{parts[1]}");
        }
        permissions.Add("DENY|ALTER|auth|");
        permissions.Add("DENY|ALTER|planning|");
        if (includePolicyAssignment)
        {
            permissions.Add("GRANT|SELECT|planning|CompanionInformationPolicyAssignments");
            permissions.Add("DENY|INSERT|planning|CompanionInformationPolicyAssignments");
            permissions.Add("DENY|UPDATE|planning|CompanionInformationPolicyAssignments");
            permissions.Add("DENY|DELETE|planning|CompanionInformationPolicyAssignments");
        }
        return permissions;
    }

    private static IReadOnlyList<string> ExpectedPlanningPermissions() =>
    [
        "GRANT|INSERT|planning|AdventurePlanCreateResults",
        "GRANT|SELECT|planning|AdventurePlanCreateResults",
        "DENY|UPDATE|planning|AdventurePlanCreateResults",
        "DENY|DELETE|planning|AdventurePlanCreateResults",
        "DENY|ALTER|planning|"
    ];

    private static IReadOnlyList<string> ExpectedPolicyPermissions() =>
    [
        "GRANT|INSERT|audit|AuditEvents", "DENY|UPDATE|audit|AuditEvents",
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
        "DENY|ALTER|audit|", "DENY|ALTER|planning|"
    ];
}
