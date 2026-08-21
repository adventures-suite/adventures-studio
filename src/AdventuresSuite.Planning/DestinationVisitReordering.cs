using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Contains one explicit destination-route reorder request.</summary>
public sealed record ReorderDestinationVisitCommand
{
    /// <summary>Initializes one provider-neutral destination reorder request.</summary>
    public ReorderDestinationVisitCommand(ActorIdentity actor, CreatorId creatorId,
        AdventurePlanId adventurePlanId, DestinationVisitId destinationVisitId,
        int targetSequence, long expectedVersion)
    {
        Actor = actor;
        CreatorId = creatorId;
        AdventurePlanId = adventurePlanId;
        DestinationVisitId = destinationVisitId;
        TargetSequence = targetSequence;
        ExpectedVersion = expectedVersion;
    }

    /// <summary>Gets the authenticated human actor.</summary>
    public ActorIdentity Actor { get; }
    /// <summary>Gets the explicit Creator ownership boundary.</summary>
    public CreatorId CreatorId { get; }
    /// <summary>Gets the target plan identity.</summary>
    public AdventurePlanId AdventurePlanId { get; }
    /// <summary>Gets the destination visit to move.</summary>
    public DestinationVisitId DestinationVisitId { get; }
    /// <summary>Gets the requested one-based route position.</summary>
    public int TargetSequence { get; }
    /// <summary>Gets the version rendered before the reorder review.</summary>
    public long ExpectedVersion { get; }
}

/// <summary>Classifies safe destination reorder outcomes.</summary>
public enum ReorderDestinationVisitOutcome
{
    /// <summary>The route and dependent local dates committed.</summary>
    Updated,
    /// <summary>The requested destination already occupied the requested position.</summary>
    Unchanged,
    /// <summary>Authorization or ownership could not be proven.</summary>
    Denied,
    /// <summary>The submitted plan version was stale.</summary>
    Conflict,
    /// <summary>The proposed route position was invalid.</summary>
    ValidationFailed,
    /// <summary>A committed or confirmed item prevents automatic date movement.</summary>
    BookingLocked,
    /// <summary>The new route would make an existing transportation schedule impossible.</summary>
    ScheduleConflict,
    /// <summary>The operation failed without committing state.</summary>
    Failed
}

/// <summary>Returns only a non-disclosing destination reorder result.</summary>
public sealed record ReorderDestinationVisitResult
{
    /// <summary>Initializes one non-disclosing reorder result.</summary>
    public ReorderDestinationVisitResult(ReorderDestinationVisitOutcome outcome, long? version = null)
    {
        Outcome = outcome;
        Version = version;
    }

    /// <summary>Gets the safe result classification.</summary>
    public ReorderDestinationVisitOutcome Outcome { get; }
    /// <summary>Gets the resulting version only for successful or unchanged outcomes.</summary>
    public long? Version { get; }
}

/// <summary>Reorders one destination and its date-linked planning records.</summary>
public interface IDestinationVisitReorderService
{
    /// <summary>Authorizes and atomically applies one reviewed destination reorder.</summary>
    Task<ReorderDestinationVisitResult> ReorderAsync(
        ReorderDestinationVisitCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements Creator-scoped, optimistic destination reordering.</summary>
public sealed class DestinationVisitReorderService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IPlanningTransactionFactory transactionFactory,
    IPlanningCreationIdentityGenerator identityGenerator,
    TimeProvider timeProvider) : IDestinationVisitReorderService
{
    /// <inheritdoc />
    public async Task<ReorderDestinationVisitResult> ReorderAsync(
        ReorderDestinationVisitCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null || command.Actor is null || !command.Actor.IsHuman
            || !command.Actor.UserId.HasValue || command.CreatorId == default
            || command.AdventurePlanId == default || command.DestinationVisitId == default)
        {
            return Result(ReorderDestinationVisitOutcome.Denied);
        }

        try
        {
            var membership = await membershipProvider.GetMembershipAsync(
                command.Actor.UserId.Value, command.CreatorId, cancellationToken);
            if (membership is null)
            {
                return Result(ReorderDestinationVisitOutcome.Denied);
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
                return Result(ReorderDestinationVisitOutcome.Denied);
            }

            await using var transaction = await transactionFactory.BeginAsync(command.CreatorId, cancellationToken);
            var current = await transaction.AdventurePlans.GetAsync(
                command.CreatorId, command.AdventurePlanId, cancellationToken);
            if (current is null || current.CreatorId != command.CreatorId
                || current.Id != command.AdventurePlanId || current.Status == PlanningStatus.Archived)
            {
                return Result(ReorderDestinationVisitOutcome.Denied);
            }

            if (current.Audit.Version != command.ExpectedVersion)
            {
                return Result(ReorderDestinationVisitOutcome.Conflict);
            }

            var ordered = current.DestinationVisits.OrderBy(item => item.Sequence).ToArray();
            var existing = ordered.SingleOrDefault(item => item.Id == command.DestinationVisitId);
            if (existing is null || command.TargetSequence < 1 || command.TargetSequence > ordered.Length)
            {
                return Result(ReorderDestinationVisitOutcome.ValidationFailed);
            }

            if (existing.Sequence == command.TargetSequence)
            {
                return new(ReorderDestinationVisitOutcome.Unchanged, current.Audit.Version);
            }

            if (HasBookingLock(current))
            {
                return Result(ReorderDestinationVisitOutcome.BookingLocked);
            }

            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var updated = current.WithReorderedDestinationVisit(
                command.DestinationVisitId, command.TargetSequence, now);
            await transaction.AdventurePlans.UpdateAsync(
                command.CreatorId, updated, command.ExpectedVersion, cancellationToken);
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
            return new(ReorderDestinationVisitOutcome.Updated, updated.Audit.Version);
        }
        catch (PlanningConcurrencyException)
        {
            return Result(ReorderDestinationVisitOutcome.Conflict);
        }
        catch (DestinationReorderScheduleException)
        {
            return Result(ReorderDestinationVisitOutcome.ScheduleConflict);
        }
        catch (ArgumentException)
        {
            return Result(ReorderDestinationVisitOutcome.ValidationFailed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(ReorderDestinationVisitOutcome.Failed);
        }
    }

    private static bool HasBookingLock(AdventurePlan plan)
    {
        static bool IsCommitted(PlanItemStatus status) => status is
            PlanItemStatus.Reserved or PlanItemStatus.Confirmed or
            PlanItemStatus.Changed or PlanItemStatus.Completed;

        var linkedDays = plan.ItineraryDays
            .Where(day => day.DestinationVisitId.HasValue)
            .Select(day => day.Id)
            .ToHashSet();
        return plan.Activities.Any(activity => linkedDays.Contains(activity.ItineraryDayId)
                && IsCommitted(activity.Status))
            || plan.Transportation.Any(segment =>
                (segment.DepartureDestinationVisitId.HasValue || segment.ArrivalDestinationVisitId.HasValue)
                && IsCommitted(segment.Status))
            || plan.Accommodations.Any(stay => stay.DestinationVisitId.HasValue && IsCommitted(stay.Status))
            || plan.Reservations.Any(reservation => reservation.DestinationVisitId.HasValue
                && (IsCommitted(reservation.Status) || !string.IsNullOrWhiteSpace(reservation.ConfirmationReference)));
    }

    private static ReorderDestinationVisitResult Result(ReorderDestinationVisitOutcome outcome) => new(outcome);
}
