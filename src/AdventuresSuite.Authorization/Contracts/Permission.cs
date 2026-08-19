using System.Collections.Frozen;

namespace TheSimontonAdventures.Web.Authorization;

/// <summary>Names one provider-independent operation that may be authorized.</summary>
public readonly record struct Permission
{
    private static readonly FrozenSet<string> ApprovedValues = new[]
    {
        "Creator.View",
        "Creator.ManageMembers",
        "AdventurePlan.View",
        "AdventurePlan.Create",
        "AdventurePlan.Edit",
        "AdventurePlan.ViewArchived",
        "AdventurePlan.Archive",
        "AdventurePlan.Restore",
        "AdventurePlan.ViewSensitiveReservations",
        "AdventurePlan.ManageCompanionPolicy",
        "PlanningProposal.Submit",
        "PlanningProposal.Review",
        "PlanningProposal.ApplyApproved",
        "PlanningEngagement.Invite",
        "PlanningEngagement.Manage",
        "PlanningEngagement.DirectEdit",
        "Audit.View",
        "Support.Impersonate"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Initializes a canonical permission name.</summary>
    public Permission(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value != value.Trim()
            || value.Length > 100
            || value.Count(character => character == '.') != 1
            || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '.')
            || !ApprovedValues.Contains(value))
        {
            throw new ArgumentException(
                "Permission must be a member of the approved operation vocabulary.",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>Gets the canonical permission name.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Determines whether a value belongs to the approved permission vocabulary.</summary>
    public static bool IsApproved(string? value) => value is not null && ApprovedValues.Contains(value);
}

/// <summary>Defines the approved initial authorization permission vocabulary.</summary>
public static class Permissions
{
    /// <summary>View a Creator workspace.</summary>
    public static readonly Permission CreatorView = new("Creator.View");
    /// <summary>Manage Creator memberships and permission assignments.</summary>
    public static readonly Permission CreatorManageMembers = new("Creator.ManageMembers");
    /// <summary>View a private Adventure Plan.</summary>
    public static readonly Permission AdventurePlanView = new("AdventurePlan.View");
    /// <summary>Create an Adventure Plan in a Creator collection.</summary>
    public static readonly Permission AdventurePlanCreate = new("AdventurePlan.Create");
    /// <summary>Edit an existing Adventure Plan.</summary>
    public static readonly Permission AdventurePlanEdit = new("AdventurePlan.Edit");
    /// <summary>List archived Adventure Plans.</summary>
    public static readonly Permission AdventurePlanViewArchived = new("AdventurePlan.ViewArchived");
    /// <summary>Archive an existing Adventure Plan.</summary>
    public static readonly Permission AdventurePlanArchive = new("AdventurePlan.Archive");
    /// <summary>Restore an archived Adventure Plan.</summary>
    public static readonly Permission AdventurePlanRestore = new("AdventurePlan.Restore");
    /// <summary>View protected reservation summaries.</summary>
    public static readonly Permission AdventurePlanViewSensitiveReservations = new("AdventurePlan.ViewSensitiveReservations");
    /// <summary>Manage a traveler's Companion information-policy assignment.</summary>
    public static readonly Permission AdventurePlanManageCompanionPolicy = new("AdventurePlan.ManageCompanionPolicy");
    /// <summary>Submit a non-authoritative Planning proposal.</summary>
    public static readonly Permission PlanningProposalSubmit = new("PlanningProposal.Submit");
    /// <summary>Review Planning proposals.</summary>
    public static readonly Permission PlanningProposalReview = new("PlanningProposal.Review");
    /// <summary>Apply already approved Planning proposal operations.</summary>
    public static readonly Permission PlanningProposalApplyApproved = new("PlanningProposal.ApplyApproved");
    /// <summary>Invite a future professional Planning collaborator.</summary>
    public static readonly Permission PlanningEngagementInvite = new("PlanningEngagement.Invite");
    /// <summary>Manage a future Planning Engagement.</summary>
    public static readonly Permission PlanningEngagementManage = new("PlanningEngagement.Manage");
    /// <summary>Directly edit under a future stronger engagement grant.</summary>
    public static readonly Permission PlanningEngagementDirectEdit = new("PlanningEngagement.DirectEdit");
    /// <summary>View authorized audit history.</summary>
    public static readonly Permission AuditView = new("Audit.View");
    /// <summary>Use explicitly controlled support impersonation.</summary>
    public static readonly Permission SupportImpersonate = new("Support.Impersonate");
}
