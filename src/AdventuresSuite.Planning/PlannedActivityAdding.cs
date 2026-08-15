using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Contains the allowlisted fields for adding one proposed activity.</summary>
public sealed record AddPlannedActivityCommand
{
    /// <summary>Initializes one provider-neutral proposed-activity request.</summary>
    public AddPlannedActivityCommand(
        ActorIdentity actor,
        CreatorId creatorId,
        AdventurePlanId adventurePlanId,
        ItineraryDayId itineraryDayId,
        long expectedVersion,
        string title,
        TimeOnly? startsAtLocal,
        TimeOnly? endsAtLocal)
    {
        Actor = actor;
        CreatorId = creatorId;
        AdventurePlanId = adventurePlanId;
        ItineraryDayId = itineraryDayId;
        ExpectedVersion = expectedVersion;
        Title = title;
        StartsAtLocal = startsAtLocal;
        EndsAtLocal = endsAtLocal;
    }

    /// <summary>Gets the authenticated human actor.</summary>
    public ActorIdentity Actor { get; }
    /// <summary>Gets the explicit Creator ownership scope.</summary>
    public CreatorId CreatorId { get; }
    /// <summary>Gets the target Adventure Plan identity.</summary>
    public AdventurePlanId AdventurePlanId { get; }
    /// <summary>Gets the owning local itinerary day.</summary>
    public ItineraryDayId ItineraryDayId { get; }
    /// <summary>Gets the plan version rendered into the form.</summary>
    public long ExpectedVersion { get; }
    /// <summary>Gets the proposed activity title.</summary>
    public string Title { get; }
    /// <summary>Gets the optional local start time.</summary>
    public TimeOnly? StartsAtLocal { get; }
    /// <summary>Gets the optional local end time.</summary>
    public TimeOnly? EndsAtLocal { get; }
}

/// <summary>Classifies non-disclosing proposed-activity outcomes.</summary>
public enum AddPlannedActivityOutcome
{
    /// <summary>The activity and required audit event committed.</summary>
    Added,
    /// <summary>Authorization or authoritative ownership could not be established.</summary>
    Denied,
    /// <summary>The submitted plan version was stale.</summary>
    Conflict,
    /// <summary>The submitted activity fields or day relationship were invalid.</summary>
    ValidationFailed,
    /// <summary>The operation failed without committing authoritative state.</summary>
    Failed
}

/// <summary>Returns only safe proposed-activity result data.</summary>
public sealed record AddPlannedActivityResult
{
    /// <summary>Initializes one safe typed result.</summary>
    public AddPlannedActivityResult(AddPlannedActivityOutcome outcome, long? version = null)
    {
        Outcome = outcome;
        Version = version;
    }

    /// <summary>Gets the non-disclosing result category.</summary>
    public AddPlannedActivityOutcome Outcome { get; }
    /// <summary>Gets the resulting plan version only after success.</summary>
    public long? Version { get; }
}

/// <summary>Adds proposed activities to private Adventure Plans.</summary>
public interface IPlannedActivityAddService
{
    /// <summary>Authorizes, validates, and atomically adds one proposed activity.</summary>
    Task<AddPlannedActivityResult> AddAsync(
        AddPlannedActivityCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements instance-authorized optimistic proposed-activity creation.</summary>
public sealed class PlannedActivityAddService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IPlanningTransactionFactory transactionFactory,
    IPlanningCreationIdentityGenerator identityGenerator,
    TimeProvider timeProvider) : IPlannedActivityAddService
{
    /// <inheritdoc />
    public async Task<AddPlannedActivityResult> AddAsync(
        AddPlannedActivityCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null || command.Actor is null || !command.Actor.IsHuman
            || !command.Actor.UserId.HasValue || command.CreatorId == default
            || command.AdventurePlanId == default || command.ItineraryDayId == default)
        {
            return Result(AddPlannedActivityOutcome.Denied);
        }

        if (command.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(command.Title)
            || command.Title != command.Title.Trim() || command.Title.Length > 200
            || (command.StartsAtLocal is { } start && command.EndsAtLocal is { } end && end < start))
        {
            return Result(AddPlannedActivityOutcome.ValidationFailed);
        }

        try
        {
            var membership = await membershipProvider.GetMembershipAsync(
                command.Actor.UserId.Value, command.CreatorId, cancellationToken);
            if (membership is null)
            {
                return Result(AddPlannedActivityOutcome.Denied);
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
                return Result(AddPlannedActivityOutcome.Denied);
            }

            await using var transaction = await transactionFactory.BeginAsync(
                command.CreatorId, cancellationToken);
            var current = await transaction.AdventurePlans.GetAsync(
                command.CreatorId, command.AdventurePlanId, cancellationToken);
            if (current is null || current.CreatorId != command.CreatorId
                || current.Id != command.AdventurePlanId
                || current.Status == PlanningStatus.Archived)
            {
                return Result(AddPlannedActivityOutcome.Denied);
            }

            if (current.Audit.Version != command.ExpectedVersion)
            {
                return Result(AddPlannedActivityOutcome.Conflict);
            }

            if (!current.ItineraryDays.Any(item => item.Id == command.ItineraryDayId))
            {
                return Result(AddPlannedActivityOutcome.ValidationFailed);
            }

            var activity = new PlannedActivity
            {
                Id = identityGenerator.NewPlannedActivityId(),
                ItineraryDayId = command.ItineraryDayId,
                Title = command.Title,
                StartsAtLocal = command.StartsAtLocal,
                EndsAtLocal = command.EndsAtLocal,
                Status = PlanItemStatus.Proposed
            };
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var updated = current.WithPlannedActivity(activity, now);
            await transaction.AdventurePlans.AddPlannedActivityAsync(
                command.CreatorId, updated, activity, command.ExpectedVersion, cancellationToken);
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
            return new(AddPlannedActivityOutcome.Added, updated.Audit.Version);
        }
        catch (PlanningConcurrencyException)
        {
            return Result(AddPlannedActivityOutcome.Conflict);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(AddPlannedActivityOutcome.Failed);
        }
    }

    private static AddPlannedActivityResult Result(AddPlannedActivityOutcome outcome) => new(outcome);
}
