using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Contains the allowlisted desired state for editing one planned activity.</summary>
/// <param name="Actor">The authenticated human actor.</param>
/// <param name="CreatorId">The explicit Creator ownership scope.</param>
/// <param name="AdventurePlanId">The target Adventure Plan.</param>
/// <param name="PlannedActivityId">The activity to edit.</param>
/// <param name="ExpectedVersion">The plan version rendered into the form.</param>
/// <param name="Title">The desired activity title.</param>
/// <param name="StartsAtLocal">The desired optional local start time.</param>
/// <param name="EndsAtLocal">The desired optional local end time.</param>
public sealed record EditPlannedActivityCommand(
    ActorIdentity Actor,
    CreatorId CreatorId,
    AdventurePlanId AdventurePlanId,
    PlannedActivityId PlannedActivityId,
    long ExpectedVersion,
    string Title,
    TimeOnly? StartsAtLocal,
    TimeOnly? EndsAtLocal);

/// <summary>Classifies non-disclosing planned-activity edit outcomes.</summary>
public enum EditPlannedActivityOutcome
{
    /// <summary>The activity and required audit event committed.</summary>
    Updated,
    /// <summary>The authoritative activity already had the requested values.</summary>
    Unchanged,
    /// <summary>Authorization or authoritative ownership could not be established.</summary>
    Denied,
    /// <summary>The submitted plan version was stale.</summary>
    Conflict,
    /// <summary>The submitted activity fields were invalid.</summary>
    ValidationFailed,
    /// <summary>The operation failed without committing authoritative state.</summary>
    Failed
}

/// <summary>Returns only safe planned-activity edit result data.</summary>
/// <param name="Outcome">The non-disclosing result category.</param>
/// <param name="Version">The authoritative version for successful or unchanged outcomes.</param>
public sealed record EditPlannedActivityResult(EditPlannedActivityOutcome Outcome, long? Version = null);

/// <summary>Edits activity details within private Adventure Plans.</summary>
public interface IPlannedActivityEditService
{
    /// <summary>Authorizes, validates, and atomically edits one planned activity.</summary>
    Task<EditPlannedActivityResult> EditAsync(
        EditPlannedActivityCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements instance-authorized optimistic planned-activity editing.</summary>
public sealed class PlannedActivityEditService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IPlanningTransactionFactory transactionFactory,
    IPlanningCreationIdentityGenerator identityGenerator,
    TimeProvider timeProvider) : IPlannedActivityEditService
{
    /// <inheritdoc />
    public async Task<EditPlannedActivityResult> EditAsync(
        EditPlannedActivityCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null || command.Actor is null || !command.Actor.IsHuman
            || !command.Actor.UserId.HasValue || command.CreatorId == default
            || command.AdventurePlanId == default || command.PlannedActivityId == default)
        {
            return Result(EditPlannedActivityOutcome.Denied);
        }

        if (command.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(command.Title)
            || command.Title != command.Title.Trim() || command.Title.Length > 200
            || (command.StartsAtLocal is { } start && command.EndsAtLocal is { } end && end < start))
        {
            return Result(EditPlannedActivityOutcome.ValidationFailed);
        }

        try
        {
            var membership = await membershipProvider.GetMembershipAsync(
                command.Actor.UserId.Value, command.CreatorId, cancellationToken);
            if (membership is null)
            {
                return Result(EditPlannedActivityOutcome.Denied);
            }

            var decision = await authorizationPolicyEvaluator.AuthorizeAsync(
                new AuthorizationRequest(
                    command.Actor, Permissions.AdventurePlanEdit,
                    AuthorizationResourceScope.ForInstance(
                        command.CreatorId, AuthorizationResourceTypes.AdventurePlan,
                        command.AdventurePlanId.Value),
                    membershipVersion: membership.Version), cancellationToken);
            if (!decision.IsAllowed
                || decision.AuditRequirement != AuthorizationAuditRequirement.RequiredMutation)
            {
                return Result(EditPlannedActivityOutcome.Denied);
            }

            await using var transaction = await transactionFactory.BeginAsync(
                command.CreatorId, cancellationToken);
            var current = await transaction.AdventurePlans.GetAsync(
                command.CreatorId, command.AdventurePlanId, cancellationToken);
            if (current is null || current.CreatorId != command.CreatorId
                || current.Id != command.AdventurePlanId || current.Status == PlanningStatus.Archived)
            {
                return Result(EditPlannedActivityOutcome.Denied);
            }

            var activity = current.Activities.SingleOrDefault(
                item => item.Id == command.PlannedActivityId);
            if (activity is null)
            {
                return Result(EditPlannedActivityOutcome.Denied);
            }

            if (activity.Title == command.Title
                && activity.StartsAtLocal == command.StartsAtLocal
                && activity.EndsAtLocal == command.EndsAtLocal)
            {
                return new(EditPlannedActivityOutcome.Unchanged, current.Audit.Version);
            }

            if (current.Audit.Version != command.ExpectedVersion)
            {
                return Result(EditPlannedActivityOutcome.Conflict);
            }

            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var updated = current.WithEditedPlannedActivity(
                command.PlannedActivityId, command.Title,
                command.StartsAtLocal, command.EndsAtLocal, now);
            var edited = updated.Activities.Single(item => item.Id == command.PlannedActivityId);
            await transaction.AdventurePlans.UpdatePlannedActivityAsync(
                command.CreatorId, updated, edited, command.ExpectedVersion, cancellationToken);
            transaction.RequiredAuditIntents.AddRequired(new AuditEventIntent(
                identityGenerator.NewAuditEventId(), command.Actor, command.CreatorId,
                Permissions.AdventurePlanEdit,
                AuthorizationResourceScope.ForInstance(
                    command.CreatorId, AuthorizationResourceTypes.AdventurePlan,
                    command.AdventurePlanId.Value),
                AuditOutcome.Succeeded, AuditReasonCategory.Completed, now,
                identityGenerator.NewCorrelationId(),
                previousVersion: command.ExpectedVersion,
                resultingVersion: updated.Audit.Version));
            await transaction.CommitAsync(cancellationToken);
            return new(EditPlannedActivityOutcome.Updated, updated.Audit.Version);
        }
        catch (PlanningConcurrencyException)
        {
            return Result(EditPlannedActivityOutcome.Conflict);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(EditPlannedActivityOutcome.Failed);
        }
    }

    private static EditPlannedActivityResult Result(EditPlannedActivityOutcome outcome) => new(outcome);
}
