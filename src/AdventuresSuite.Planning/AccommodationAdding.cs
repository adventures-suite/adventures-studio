using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Contains allowlisted fields for one proposed accommodation.</summary>
public sealed record AddAccommodationCommand
{
    /// <summary>Initializes one provider-neutral accommodation request.</summary>
    public AddAccommodationCommand(ActorIdentity actor, CreatorId creatorId,
        AdventurePlanId adventurePlanId, long expectedVersion, string name,
        DateOnly startDate, DateOnly endDate, string timeZoneId,
        DestinationVisitId? destinationVisitId = null)
    {
        Actor = actor; CreatorId = creatorId; AdventurePlanId = adventurePlanId;
        ExpectedVersion = expectedVersion; Name = name; StartDate = startDate;
        EndDate = endDate; TimeZoneId = timeZoneId;
        DestinationVisitId = destinationVisitId;
    }

    /// <summary>Gets the authenticated human actor.</summary>
    public ActorIdentity Actor { get; init; }
    /// <summary>Gets the explicit Creator scope.</summary>
    public CreatorId CreatorId { get; init; }
    /// <summary>Gets the target plan.</summary>
    public AdventurePlanId AdventurePlanId { get; init; }
    /// <summary>Gets the rendered plan version.</summary>
    public long ExpectedVersion { get; init; }
    /// <summary>Gets the working accommodation name.</summary>
    public string Name { get; init; }
    /// <summary>Gets the inclusive local start date.</summary>
    public DateOnly StartDate { get; init; }
    /// <summary>Gets the inclusive local end date.</summary>
    public DateOnly EndDate { get; init; }
    /// <summary>Gets the property's IANA time zone.</summary>
    public string TimeZoneId { get; init; }
    /// <summary>Gets the optional authoritative destination visit containing the stay.</summary>
    public DestinationVisitId? DestinationVisitId { get; init; }
}

/// <summary>Classifies non-disclosing accommodation outcomes.</summary>
public enum AddAccommodationOutcome
{
    /// <summary>The accommodation and audit event committed.</summary>
    Added,
    /// <summary>Authorization or ownership could not be established.</summary>
    Denied,
    /// <summary>The submitted plan version was stale.</summary>
    Conflict,
    /// <summary>The submitted fields were invalid.</summary>
    ValidationFailed,
    /// <summary>The operation failed without committing state.</summary>
    Failed
}

/// <summary>Returns only safe accommodation result data.</summary>
public sealed record AddAccommodationResult
{
    /// <summary>Initializes one safe result.</summary>
    public AddAccommodationResult(AddAccommodationOutcome outcome, long? version = null)
    {
        Outcome = outcome; Version = version;
    }
    /// <summary>Gets the non-disclosing outcome.</summary>
    public AddAccommodationOutcome Outcome { get; }
    /// <summary>Gets the resulting version only after success.</summary>
    public long? Version { get; }
}

/// <summary>Adds proposed accommodations to private Adventure Plans.</summary>
public interface IAccommodationAddService
{
    /// <summary>Authorizes, validates, and atomically adds one accommodation.</summary>
    Task<AddAccommodationResult> AddAsync(AddAccommodationCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements instance-authorized optimistic accommodation creation.</summary>
public sealed class AccommodationAddService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IPlanningTransactionFactory transactionFactory,
    IPlanningCreationIdentityGenerator identityGenerator,
    TimeProvider timeProvider) : IAccommodationAddService
{
    /// <inheritdoc />
    public async Task<AddAccommodationResult> AddAsync(AddAccommodationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null || command.Actor is null || !command.Actor.IsHuman
            || !command.Actor.UserId.HasValue || command.CreatorId == default
            || command.AdventurePlanId == default)
            return Result(AddAccommodationOutcome.Denied);

        if (!TryValidate(command, out var dates, out var zone))
            return Result(AddAccommodationOutcome.ValidationFailed);

        try
        {
            var membership = await membershipProvider.GetMembershipAsync(
                command.Actor.UserId.Value, command.CreatorId, cancellationToken);
            if (membership is null) return Result(AddAccommodationOutcome.Denied);
            var decision = await authorizationPolicyEvaluator.AuthorizeAsync(
                new AuthorizationRequest(command.Actor, Permissions.AdventurePlanEdit,
                    AuthorizationResourceScope.ForInstance(command.CreatorId,
                        AuthorizationResourceTypes.AdventurePlan, command.AdventurePlanId.Value),
                    membershipVersion: membership.Version), cancellationToken);
            if (!decision.IsAllowed
                || decision.AuditRequirement != AuthorizationAuditRequirement.RequiredMutation)
                return Result(AddAccommodationOutcome.Denied);

            await using var transaction = await transactionFactory.BeginAsync(
                command.CreatorId, cancellationToken);
            var current = await transaction.AdventurePlans.GetAsync(
                command.CreatorId, command.AdventurePlanId, cancellationToken);
            if (current is null || current.CreatorId != command.CreatorId
                || current.Id != command.AdventurePlanId || current.Status == PlanningStatus.Archived)
                return Result(AddAccommodationOutcome.Denied);
            if (current.Audit.Version != command.ExpectedVersion)
                return Result(AddAccommodationOutcome.Conflict);
            if (!current.Dates.Contains(dates))
                return Result(AddAccommodationOutcome.ValidationFailed);
            if (command.DestinationVisitId is { } destinationVisitId
                && current.DestinationVisits.All(visit => visit.Id != destinationVisitId))
                return Result(AddAccommodationOutcome.Denied);

            var accommodation = new Accommodation
            {
                Id = identityGenerator.NewAccommodationId(),
                DestinationVisitId = command.DestinationVisitId,
                Name = command.Name,
                Dates = dates,
                TimeZone = zone,
                Status = PlanItemStatus.Proposed
            };
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var updated = current.WithAccommodation(accommodation, now);
            await transaction.AdventurePlans.AddAccommodationAsync(
                command.CreatorId, updated, accommodation, command.ExpectedVersion, cancellationToken);
            transaction.RequiredAuditIntents.AddRequired(new AuditEventIntent(
                identityGenerator.NewAuditEventId(), command.Actor, command.CreatorId,
                Permissions.AdventurePlanEdit,
                AuthorizationResourceScope.ForInstance(command.CreatorId,
                    AuthorizationResourceTypes.AdventurePlan, command.AdventurePlanId.Value),
                AuditOutcome.Succeeded, AuditReasonCategory.Completed, now,
                identityGenerator.NewCorrelationId(),
                previousVersion: command.ExpectedVersion,
                resultingVersion: updated.Audit.Version));
            await transaction.CommitAsync(cancellationToken);
            return new(AddAccommodationOutcome.Added, updated.Audit.Version);
        }
        catch (PlanningConcurrencyException) { return Result(AddAccommodationOutcome.Conflict); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Result(AddAccommodationOutcome.Failed); }
    }

    private static bool TryValidate(AddAccommodationCommand command,
        out PlanningDateRange dates, out IanaTimeZone zone)
    {
        dates = default; zone = default;
        if (command.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(command.Name)
            || command.Name != command.Name.Trim() || command.Name.Length > 200
            || command.EndDate < command.StartDate) return false;
        try { dates = new(command.StartDate, command.EndDate); zone = new(command.TimeZoneId); return true; }
        catch (ArgumentException) { return false; }
    }

    private static AddAccommodationResult Result(AddAccommodationOutcome outcome) => new(outcome);
}
