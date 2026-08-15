using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Contains the allowlisted fields for adding one local itinerary day.</summary>
public sealed record AddItineraryDayCommand
{
    /// <summary>Initializes one provider-neutral itinerary-day request.</summary>
    public AddItineraryDayCommand(
        ActorIdentity actor,
        CreatorId creatorId,
        AdventurePlanId adventurePlanId,
        DestinationVisitId destinationVisitId,
        long expectedVersion,
        DateOnly date,
        string title)
    {
        Actor = actor;
        CreatorId = creatorId;
        AdventurePlanId = adventurePlanId;
        DestinationVisitId = destinationVisitId;
        ExpectedVersion = expectedVersion;
        Date = date;
        Title = title;
    }

    /// <summary>Gets the authenticated human actor.</summary>
    public ActorIdentity Actor { get; }
    /// <summary>Gets the explicit Creator ownership scope.</summary>
    public CreatorId CreatorId { get; }
    /// <summary>Gets the target Adventure Plan identity.</summary>
    public AdventurePlanId AdventurePlanId { get; }
    /// <summary>Gets the destination visit that supplies date and time-zone context.</summary>
    public DestinationVisitId DestinationVisitId { get; }
    /// <summary>Gets the plan version rendered into the form.</summary>
    public long ExpectedVersion { get; }
    /// <summary>Gets the local itinerary date.</summary>
    public DateOnly Date { get; }
    /// <summary>Gets the day's working title.</summary>
    public string Title { get; }
}

/// <summary>Classifies non-disclosing itinerary-day outcomes.</summary>
public enum AddItineraryDayOutcome
{
    /// <summary>The day and required audit event committed.</summary>
    Added,
    /// <summary>Authorization or authoritative ownership could not be established.</summary>
    Denied,
    /// <summary>The submitted plan version was stale.</summary>
    Conflict,
    /// <summary>The submitted day fields or visit relationship were invalid.</summary>
    ValidationFailed,
    /// <summary>The operation failed without committing authoritative state.</summary>
    Failed
}

/// <summary>Returns only safe itinerary-day result data.</summary>
public sealed record AddItineraryDayResult
{
    /// <summary>Initializes one safe typed result.</summary>
    public AddItineraryDayResult(AddItineraryDayOutcome outcome, long? version = null)
    {
        Outcome = outcome;
        Version = version;
    }

    /// <summary>Gets the non-disclosing result category.</summary>
    public AddItineraryDayOutcome Outcome { get; }
    /// <summary>Gets the resulting plan version only after success.</summary>
    public long? Version { get; }
}

/// <summary>Adds local itinerary days to private Adventure Plans.</summary>
public interface IItineraryDayAddService
{
    /// <summary>Authorizes, validates, and atomically adds one itinerary day.</summary>
    Task<AddItineraryDayResult> AddAsync(
        AddItineraryDayCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements instance-authorized optimistic itinerary-day creation.</summary>
public sealed class ItineraryDayAddService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IPlanningTransactionFactory transactionFactory,
    IPlanningCreationIdentityGenerator identityGenerator,
    TimeProvider timeProvider) : IItineraryDayAddService
{
    /// <inheritdoc />
    public async Task<AddItineraryDayResult> AddAsync(
        AddItineraryDayCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null || command.Actor is null || !command.Actor.IsHuman
            || !command.Actor.UserId.HasValue || command.CreatorId == default
            || command.AdventurePlanId == default || command.DestinationVisitId == default)
        {
            return Result(AddItineraryDayOutcome.Denied);
        }

        if (command.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(command.Title)
            || command.Title != command.Title.Trim() || command.Title.Length > 200)
        {
            return Result(AddItineraryDayOutcome.ValidationFailed);
        }

        try
        {
            var membership = await membershipProvider.GetMembershipAsync(
                command.Actor.UserId.Value, command.CreatorId, cancellationToken);
            if (membership is null)
            {
                return Result(AddItineraryDayOutcome.Denied);
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
                return Result(AddItineraryDayOutcome.Denied);
            }

            await using var transaction = await transactionFactory.BeginAsync(
                command.CreatorId, cancellationToken);
            var current = await transaction.AdventurePlans.GetAsync(
                command.CreatorId, command.AdventurePlanId, cancellationToken);
            if (current is null || current.CreatorId != command.CreatorId
                || current.Id != command.AdventurePlanId
                || current.Status == PlanningStatus.Archived)
            {
                return Result(AddItineraryDayOutcome.Denied);
            }

            if (current.Audit.Version != command.ExpectedVersion)
            {
                return Result(AddItineraryDayOutcome.Conflict);
            }

            var visit = current.DestinationVisits.SingleOrDefault(
                item => item.Id == command.DestinationVisitId);
            if (visit is null || !visit.Dates.Contains(command.Date)
                || current.ItineraryDays.Any(item => item.Date == command.Date))
            {
                return Result(AddItineraryDayOutcome.ValidationFailed);
            }

            var day = new ItineraryDay
            {
                Id = identityGenerator.NewItineraryDayId(),
                DestinationVisitId = visit.Id,
                Date = command.Date,
                TimeZone = visit.TimeZone,
                Title = command.Title
            };
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var updated = current.WithItineraryDay(day, now);
            await transaction.AdventurePlans.AddItineraryDayAsync(
                command.CreatorId, updated, day, command.ExpectedVersion, cancellationToken);
            transaction.RequiredAuditIntents.AddRequired(new AuditEventIntent(
                identityGenerator.NewAuditEventId(), command.Actor, command.CreatorId,
                Permissions.AdventurePlanEdit,
                AuthorizationResourceScope.ForInstance(
                    command.CreatorId,
                    AuthorizationResourceTypes.AdventurePlan,
                    command.AdventurePlanId.Value),
                AuditOutcome.Succeeded, AuditReasonCategory.Completed, now,
                identityGenerator.NewCorrelationId(),
                previousVersion: command.ExpectedVersion,
                resultingVersion: updated.Audit.Version));
            await transaction.CommitAsync(cancellationToken);
            return new(AddItineraryDayOutcome.Added, updated.Audit.Version);
        }
        catch (PlanningConcurrencyException)
        {
            return Result(AddItineraryDayOutcome.Conflict);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(AddItineraryDayOutcome.Failed);
        }
    }

    private static AddItineraryDayResult Result(AddItineraryDayOutcome outcome) => new(outcome);
}
