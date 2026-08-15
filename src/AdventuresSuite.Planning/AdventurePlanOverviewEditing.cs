using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Contains the allowlisted fields for one Adventure Plan overview edit.</summary>
public sealed record EditAdventurePlanOverviewCommand
{
    /// <summary>Initializes one provider-neutral overview-edit request.</summary>
    public EditAdventurePlanOverviewCommand(
        ActorIdentity actor,
        CreatorId creatorId,
        AdventurePlanId adventurePlanId,
        long expectedVersion,
        string title,
        string? workingDescription,
        DateOnly startDate,
        DateOnly endDate)
    {
        Actor = actor;
        CreatorId = creatorId;
        AdventurePlanId = adventurePlanId;
        ExpectedVersion = expectedVersion;
        Title = title;
        WorkingDescription = workingDescription;
        StartDate = startDate;
        EndDate = endDate;
    }

    /// <summary>Gets the authenticated actor requesting the edit.</summary>
    public ActorIdentity Actor { get; init; }
    /// <summary>Gets the explicit Creator ownership scope.</summary>
    public CreatorId CreatorId { get; init; }
    /// <summary>Gets the target Adventure Plan identity.</summary>
    public AdventurePlanId AdventurePlanId { get; init; }
    /// <summary>Gets the version rendered into the submitted form.</summary>
    public long ExpectedVersion { get; init; }
    /// <summary>Gets the proposed private title.</summary>
    public string Title { get; init; }
    /// <summary>Gets the proposed optional private working description.</summary>
    public string? WorkingDescription { get; init; }
    /// <summary>Gets the proposed inclusive first local calendar date.</summary>
    public DateOnly StartDate { get; init; }
    /// <summary>Gets the proposed inclusive last local calendar date.</summary>
    public DateOnly EndDate { get; init; }
}

/// <summary>Classifies non-disclosing Adventure Plan overview-edit outcomes.</summary>
public enum EditAdventurePlanOverviewOutcome
{
    /// <summary>The overview and required audit event committed.</summary>
    Updated,
    /// <summary>The authorized request matched current state and made no mutation.</summary>
    Unchanged,
    /// <summary>Authorization or authoritative ownership could not be established.</summary>
    Denied,
    /// <summary>The submitted version was stale.</summary>
    Conflict,
    /// <summary>The submitted overview fields were invalid.</summary>
    ValidationFailed,
    /// <summary>A date change was rejected because dated itinerary records exist.</summary>
    DateChangeBlocked,
    /// <summary>The operation failed without committing authoritative state.</summary>
    Failed
}

/// <summary>Returns only safe overview-edit result data.</summary>
public sealed record EditAdventurePlanOverviewResult
{
    /// <summary>Initializes a safe typed result.</summary>
    public EditAdventurePlanOverviewResult(
        EditAdventurePlanOverviewOutcome outcome,
        long? version = null)
    {
        Outcome = outcome;
        Version = version;
    }

    /// <summary>Gets the non-disclosing result category.</summary>
    public EditAdventurePlanOverviewOutcome Outcome { get; }
    /// <summary>Gets the current resulting version only for successful or unchanged outcomes.</summary>
    public long? Version { get; }
}

/// <summary>Edits the allowlisted overview of one private Adventure Plan.</summary>
public interface IAdventurePlanOverviewEditService
{
    /// <summary>Authorizes, validates, and atomically applies one overview edit.</summary>
    Task<EditAdventurePlanOverviewResult> EditAsync(
        EditAdventurePlanOverviewCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements instance-authorized optimistic Adventure Plan overview editing.</summary>
public sealed class AdventurePlanOverviewEditService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IPlanningTransactionFactory transactionFactory,
    IPlanningCreationIdentityGenerator identityGenerator,
    TimeProvider timeProvider) : IAdventurePlanOverviewEditService
{
    /// <inheritdoc />
    public async Task<EditAdventurePlanOverviewResult> EditAsync(
        EditAdventurePlanOverviewCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null
            || command.Actor is null
            || !command.Actor.IsHuman
            || !command.Actor.UserId.HasValue
            || command.CreatorId == default
            || command.AdventurePlanId == default)
        {
            return Result(EditAdventurePlanOverviewOutcome.Denied);
        }

        try
        {
            var membership = await membershipProvider.GetMembershipAsync(
                command.Actor.UserId.Value,
                command.CreatorId,
                cancellationToken);
            if (membership is null)
            {
                return Result(EditAdventurePlanOverviewOutcome.Denied);
            }

            var decision = await authorizationPolicyEvaluator.AuthorizeAsync(
                new AuthorizationRequest(
                    command.Actor,
                    Permissions.AdventurePlanEdit,
                    AuthorizationResourceScope.ForInstance(
                        command.CreatorId,
                        AuthorizationResourceTypes.AdventurePlan,
                        command.AdventurePlanId.Value),
                    membershipVersion: membership.Version),
                cancellationToken);
            if (!decision.IsAllowed
                || decision.AuditRequirement != AuthorizationAuditRequirement.RequiredMutation)
            {
                return Result(EditAdventurePlanOverviewOutcome.Denied);
            }

            if (!TryValidate(command, out var description, out var dates))
            {
                return Result(EditAdventurePlanOverviewOutcome.ValidationFailed);
            }

            await using var transaction = await transactionFactory.BeginAsync(
                command.CreatorId,
                cancellationToken);
            var current = await transaction.AdventurePlans.GetAsync(
                command.CreatorId,
                command.AdventurePlanId,
                cancellationToken);
            if (current is null
                || current.CreatorId != command.CreatorId
                || current.Id != command.AdventurePlanId
                || current.Status == PlanningStatus.Archived)
            {
                return Result(EditAdventurePlanOverviewOutcome.Denied);
            }

            if (current.Audit.Version != command.ExpectedVersion)
            {
                return Result(EditAdventurePlanOverviewOutcome.Conflict);
            }

            var datesChanged = current.Dates != dates;
            if (datesChanged && HasDatedItinerary(current))
            {
                return Result(EditAdventurePlanOverviewOutcome.DateChangeBlocked);
            }

            if (current.Title == command.Title
                && current.WorkingDescription == description
                && !datesChanged)
            {
                return new(EditAdventurePlanOverviewOutcome.Unchanged, current.Audit.Version);
            }

            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var updated = current.WithOverview(command.Title, description, dates, now);
            await transaction.AdventurePlans.UpdateOverviewAsync(
                command.CreatorId,
                updated,
                command.ExpectedVersion,
                cancellationToken);
            transaction.RequiredAuditIntents.AddRequired(new AuditEventIntent(
                identityGenerator.NewAuditEventId(),
                command.Actor,
                command.CreatorId,
                Permissions.AdventurePlanEdit,
                AuthorizationResourceScope.ForInstance(
                    command.CreatorId,
                    AuthorizationResourceTypes.AdventurePlan,
                    command.AdventurePlanId.Value),
                AuditOutcome.Succeeded,
                AuditReasonCategory.Completed,
                now,
                identityGenerator.NewCorrelationId(),
                previousVersion: command.ExpectedVersion,
                resultingVersion: updated.Audit.Version));
            await transaction.CommitAsync(cancellationToken);
            return new(EditAdventurePlanOverviewOutcome.Updated, updated.Audit.Version);
        }
        catch (PlanningConcurrencyException)
        {
            return Result(EditAdventurePlanOverviewOutcome.Conflict);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(EditAdventurePlanOverviewOutcome.Failed);
        }
    }

    private static bool TryValidate(
        EditAdventurePlanOverviewCommand command,
        out string? description,
        out PlanningDateRange dates)
    {
        description = string.IsNullOrEmpty(command.WorkingDescription)
            ? null
            : command.WorkingDescription;
        dates = default;
        if (command.ExpectedVersion < 1
            || string.IsNullOrWhiteSpace(command.Title)
            || command.Title != command.Title.Trim()
            || command.Title.Length > 200
            || (description is not null
                && (description != description.Trim() || description.Length > 2000))
            || command.EndDate < command.StartDate)
        {
            return false;
        }

        dates = new PlanningDateRange(command.StartDate, command.EndDate);
        return true;
    }

    private static bool HasDatedItinerary(AdventurePlan plan) =>
        plan.DestinationVisits.Count > 0
        || plan.ItineraryDays.Count > 0
        || plan.Transportation.Count > 0
        || plan.Accommodations.Count > 0;

    private static EditAdventurePlanOverviewResult Result(
        EditAdventurePlanOverviewOutcome outcome) => new(outcome);
}
