using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Contains the allowlisted desired state for editing one itinerary-day title.</summary>
/// <param name="Actor">The authenticated human actor.</param>
/// <param name="CreatorId">The explicit Creator ownership scope.</param>
/// <param name="AdventurePlanId">The target Adventure Plan.</param>
/// <param name="ItineraryDayId">The itinerary day to edit.</param>
/// <param name="ExpectedVersion">The plan version rendered into the form.</param>
/// <param name="Title">The desired day title.</param>
public sealed record EditItineraryDayCommand(
    ActorIdentity Actor,
    CreatorId CreatorId,
    AdventurePlanId AdventurePlanId,
    ItineraryDayId ItineraryDayId,
    long ExpectedVersion,
    string Title);

/// <summary>Classifies non-disclosing itinerary-day edit outcomes.</summary>
public enum EditItineraryDayOutcome
{
    /// <summary>The title and required audit event committed.</summary>
    Updated,
    /// <summary>The authoritative title already matched the requested value.</summary>
    Unchanged,
    /// <summary>Authorization or authoritative ownership could not be established.</summary>
    Denied,
    /// <summary>The submitted plan version was stale.</summary>
    Conflict,
    /// <summary>The submitted title was invalid.</summary>
    ValidationFailed,
    /// <summary>The operation failed without committing authoritative state.</summary>
    Failed
}

/// <summary>Returns only safe itinerary-day edit result data.</summary>
/// <param name="Outcome">The non-disclosing result category.</param>
/// <param name="Version">The authoritative version for successful or unchanged outcomes.</param>
public sealed record EditItineraryDayResult(EditItineraryDayOutcome Outcome, long? Version = null);

/// <summary>Edits itinerary-day titles within private Adventure Plans.</summary>
public interface IItineraryDayEditService
{
    /// <summary>Authorizes, validates, and atomically edits one itinerary-day title.</summary>
    Task<EditItineraryDayResult> EditAsync(
        EditItineraryDayCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements instance-authorized optimistic itinerary-day title editing.</summary>
public sealed class ItineraryDayEditService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IPlanningTransactionFactory transactionFactory,
    IPlanningCreationIdentityGenerator identityGenerator,
    TimeProvider timeProvider) : IItineraryDayEditService
{
    /// <inheritdoc />
    public async Task<EditItineraryDayResult> EditAsync(
        EditItineraryDayCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null || command.Actor is null || !command.Actor.IsHuman
            || !command.Actor.UserId.HasValue || command.CreatorId == default
            || command.AdventurePlanId == default || command.ItineraryDayId == default)
        {
            return Result(EditItineraryDayOutcome.Denied);
        }

        if (command.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(command.Title)
            || command.Title != command.Title.Trim() || command.Title.Length > 200)
        {
            return Result(EditItineraryDayOutcome.ValidationFailed);
        }

        try
        {
            var membership = await membershipProvider.GetMembershipAsync(
                command.Actor.UserId.Value, command.CreatorId, cancellationToken);
            if (membership is null)
            {
                return Result(EditItineraryDayOutcome.Denied);
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
                return Result(EditItineraryDayOutcome.Denied);
            }

            await using var transaction = await transactionFactory.BeginAsync(
                command.CreatorId, cancellationToken);
            var current = await transaction.AdventurePlans.GetAsync(
                command.CreatorId, command.AdventurePlanId, cancellationToken);
            if (current is null || current.CreatorId != command.CreatorId
                || current.Id != command.AdventurePlanId || current.Status == PlanningStatus.Archived)
            {
                return Result(EditItineraryDayOutcome.Denied);
            }

            var day = current.ItineraryDays.SingleOrDefault(item => item.Id == command.ItineraryDayId);
            if (day is null)
            {
                return Result(EditItineraryDayOutcome.Denied);
            }

            if (day.Title == command.Title)
            {
                return new(EditItineraryDayOutcome.Unchanged, current.Audit.Version);
            }

            if (current.Audit.Version != command.ExpectedVersion)
            {
                return Result(EditItineraryDayOutcome.Conflict);
            }

            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var updated = current.WithEditedItineraryDayTitle(
                command.ItineraryDayId, command.Title, now);
            var edited = updated.ItineraryDays.Single(item => item.Id == command.ItineraryDayId);
            await transaction.AdventurePlans.UpdateItineraryDayAsync(
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
            return new(EditItineraryDayOutcome.Updated, updated.Audit.Version);
        }
        catch (PlanningConcurrencyException)
        {
            return Result(EditItineraryDayOutcome.Conflict);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(EditItineraryDayOutcome.Failed);
        }
    }

    private static EditItineraryDayResult Result(EditItineraryDayOutcome outcome) => new(outcome);
}
