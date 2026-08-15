using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Contains the allowlisted fields for adding one destination visit.</summary>
public sealed record AddDestinationVisitCommand
{
    /// <summary>Initializes one provider-neutral destination-visit request.</summary>
    public AddDestinationVisitCommand(
        ActorIdentity actor,
        CreatorId creatorId,
        AdventurePlanId adventurePlanId,
        long expectedVersion,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string timeZoneId)
    {
        Actor = actor;
        CreatorId = creatorId;
        AdventurePlanId = adventurePlanId;
        ExpectedVersion = expectedVersion;
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        TimeZoneId = timeZoneId;
    }

    /// <summary>Gets the authenticated human actor.</summary>
    public ActorIdentity Actor { get; }
    /// <summary>Gets the explicit Creator ownership scope.</summary>
    public CreatorId CreatorId { get; }
    /// <summary>Gets the target Adventure Plan identity.</summary>
    public AdventurePlanId AdventurePlanId { get; }
    /// <summary>Gets the plan version rendered into the form.</summary>
    public long ExpectedVersion { get; }
    /// <summary>Gets the destination's working name.</summary>
    public string Name { get; }
    /// <summary>Gets the inclusive first local visit date.</summary>
    public DateOnly StartDate { get; }
    /// <summary>Gets the inclusive last local visit date.</summary>
    public DateOnly EndDate { get; }
    /// <summary>Gets the proposed IANA time-zone identifier.</summary>
    public string TimeZoneId { get; }
}

/// <summary>Classifies non-disclosing destination-visit outcomes.</summary>
public enum AddDestinationVisitOutcome
{
    /// <summary>The visit and required audit event committed.</summary>
    Added,
    /// <summary>Authorization or authoritative ownership could not be established.</summary>
    Denied,
    /// <summary>The submitted plan version was stale.</summary>
    Conflict,
    /// <summary>The submitted visit fields were invalid.</summary>
    ValidationFailed,
    /// <summary>The operation failed without committing authoritative state.</summary>
    Failed
}

/// <summary>Returns only safe destination-visit result data.</summary>
public sealed record AddDestinationVisitResult
{
    /// <summary>Initializes one safe typed result.</summary>
    public AddDestinationVisitResult(AddDestinationVisitOutcome outcome, long? version = null)
    {
        Outcome = outcome;
        Version = version;
    }

    /// <summary>Gets the non-disclosing result category.</summary>
    public AddDestinationVisitOutcome Outcome { get; }
    /// <summary>Gets the resulting plan version only after success.</summary>
    public long? Version { get; }
}

/// <summary>Adds destination visits to private Adventure Plans.</summary>
public interface IDestinationVisitAddService
{
    /// <summary>Authorizes, validates, and atomically adds one destination visit.</summary>
    Task<AddDestinationVisitResult> AddAsync(
        AddDestinationVisitCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements instance-authorized optimistic destination-visit creation.</summary>
public sealed class DestinationVisitAddService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IPlanningTransactionFactory transactionFactory,
    IPlanningCreationIdentityGenerator identityGenerator,
    TimeProvider timeProvider) : IDestinationVisitAddService
{
    /// <inheritdoc />
    public async Task<AddDestinationVisitResult> AddAsync(
        AddDestinationVisitCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null || command.Actor is null || !command.Actor.IsHuman
            || !command.Actor.UserId.HasValue || command.CreatorId == default
            || command.AdventurePlanId == default)
        {
            return Result(AddDestinationVisitOutcome.Denied);
        }

        if (!TryValidate(command, out var dates, out var timeZone))
        {
            return Result(AddDestinationVisitOutcome.ValidationFailed);
        }

        try
        {
            var membership = await membershipProvider.GetMembershipAsync(
                command.Actor.UserId.Value, command.CreatorId, cancellationToken);
            if (membership is null)
            {
                return Result(AddDestinationVisitOutcome.Denied);
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
                return Result(AddDestinationVisitOutcome.Denied);
            }

            await using var transaction = await transactionFactory.BeginAsync(
                command.CreatorId, cancellationToken);
            var current = await transaction.AdventurePlans.GetAsync(
                command.CreatorId, command.AdventurePlanId, cancellationToken);
            if (current is null || current.CreatorId != command.CreatorId
                || current.Id != command.AdventurePlanId
                || current.Status == PlanningStatus.Archived)
            {
                return Result(AddDestinationVisitOutcome.Denied);
            }

            if (current.Audit.Version != command.ExpectedVersion)
            {
                return Result(AddDestinationVisitOutcome.Conflict);
            }

            if (!current.Dates.Contains(dates))
            {
                return Result(AddDestinationVisitOutcome.ValidationFailed);
            }

            var visit = new DestinationVisit
            {
                Id = identityGenerator.NewDestinationVisitId(),
                Name = command.Name,
                Dates = dates,
                TimeZone = timeZone,
                Sequence = current.DestinationVisits.Count == 0
                    ? 1
                    : checked(current.DestinationVisits.Max(item => item.Sequence) + 1)
            };
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var updated = current.WithDestinationVisit(visit, now);
            await transaction.AdventurePlans.AddDestinationVisitAsync(
                command.CreatorId, updated, visit, command.ExpectedVersion, cancellationToken);
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
            return new(AddDestinationVisitOutcome.Added, updated.Audit.Version);
        }
        catch (PlanningConcurrencyException)
        {
            return Result(AddDestinationVisitOutcome.Conflict);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(AddDestinationVisitOutcome.Failed);
        }
    }

    private static bool TryValidate(
        AddDestinationVisitCommand command,
        out PlanningDateRange dates,
        out IanaTimeZone timeZone)
    {
        dates = default;
        timeZone = default;
        if (command.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(command.Name)
            || command.Name != command.Name.Trim() || command.Name.Length > 200
            || command.EndDate < command.StartDate || string.IsNullOrWhiteSpace(command.TimeZoneId)
            || command.TimeZoneId != command.TimeZoneId.Trim())
        {
            return false;
        }

        try
        {
            dates = new(command.StartDate, command.EndDate);
            timeZone = new(command.TimeZoneId);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static AddDestinationVisitResult Result(AddDestinationVisitOutcome outcome) => new(outcome);
}
