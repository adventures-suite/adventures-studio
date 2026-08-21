using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Contains allowlisted fields for one provider-neutral transportation segment.</summary>
/// <param name="Actor">The authenticated human actor.</param>
/// <param name="CreatorId">The explicit Creator scope.</param>
/// <param name="AdventurePlanId">The target plan.</param>
/// <param name="ExpectedVersion">The rendered plan version.</param>
/// <param name="Mode">The provider-neutral transportation mode.</param>
/// <param name="From">The departure place.</param>
/// <param name="To">The arrival place.</param>
/// <param name="DepartureDate">The local departure date.</param>
/// <param name="DepartureTimeLocal">The optional local departure time.</param>
/// <param name="DepartureTimeZoneId">The departure IANA time zone.</param>
/// <param name="ArrivalDate">The local arrival date.</param>
/// <param name="ArrivalTimeLocal">The optional local arrival time.</param>
/// <param name="ArrivalTimeZoneId">The arrival IANA time zone.</param>
/// <param name="DepartureDestinationVisitId">The optional authoritative departure destination visit.</param>
/// <param name="ArrivalDestinationVisitId">The optional authoritative arrival destination visit.</param>
public sealed record AddTransportationSegmentCommand(
    ActorIdentity Actor,
    CreatorId CreatorId,
    AdventurePlanId AdventurePlanId,
    long ExpectedVersion,
    string Mode,
    string From,
    string To,
    DateOnly DepartureDate,
    TimeOnly? DepartureTimeLocal,
    string DepartureTimeZoneId,
    DateOnly ArrivalDate,
    TimeOnly? ArrivalTimeLocal,
    string ArrivalTimeZoneId,
    DestinationVisitId? DepartureDestinationVisitId = null,
    DestinationVisitId? ArrivalDestinationVisitId = null)
{
    /// <summary>Gets the authenticated human actor.</summary>
    public ActorIdentity Actor { get; init; } = Actor;
    /// <summary>Gets the explicit Creator scope.</summary>
    public CreatorId CreatorId { get; init; } = CreatorId;
    /// <summary>Gets the target Adventure Plan.</summary>
    public AdventurePlanId AdventurePlanId { get; init; } = AdventurePlanId;
    /// <summary>Gets the rendered plan version.</summary>
    public long ExpectedVersion { get; init; } = ExpectedVersion;
    /// <summary>Gets the provider-neutral mode.</summary>
    public string Mode { get; init; } = Mode;
    /// <summary>Gets the departure place.</summary>
    public string From { get; init; } = From;
    /// <summary>Gets the arrival place.</summary>
    public string To { get; init; } = To;
    /// <summary>Gets the local departure date.</summary>
    public DateOnly DepartureDate { get; init; } = DepartureDate;
    /// <summary>Gets the optional local departure time.</summary>
    public TimeOnly? DepartureTimeLocal { get; init; } = DepartureTimeLocal;
    /// <summary>Gets the departure IANA time zone.</summary>
    public string DepartureTimeZoneId { get; init; } = DepartureTimeZoneId;
    /// <summary>Gets the local arrival date.</summary>
    public DateOnly ArrivalDate { get; init; } = ArrivalDate;
    /// <summary>Gets the optional local arrival time.</summary>
    public TimeOnly? ArrivalTimeLocal { get; init; } = ArrivalTimeLocal;
    /// <summary>Gets the arrival IANA time zone.</summary>
    public string ArrivalTimeZoneId { get; init; } = ArrivalTimeZoneId;
    /// <summary>Gets the optional authoritative departure destination visit.</summary>
    public DestinationVisitId? DepartureDestinationVisitId { get; init; } = DepartureDestinationVisitId;
    /// <summary>Gets the optional authoritative arrival destination visit.</summary>
    public DestinationVisitId? ArrivalDestinationVisitId { get; init; } = ArrivalDestinationVisitId;
}

/// <summary>Classifies non-disclosing transportation-segment outcomes.</summary>
public enum AddTransportationSegmentOutcome
{
    /// <summary>The segment and required audit event committed.</summary>
    Added,
    /// <summary>Authorization or authoritative ownership could not be established.</summary>
    Denied,
    /// <summary>The submitted plan version was stale.</summary>
    Conflict,
    /// <summary>The submitted transportation fields were invalid.</summary>
    ValidationFailed,
    /// <summary>The operation failed without committing authoritative state.</summary>
    Failed
}

/// <summary>Returns only safe transportation-segment result data.</summary>
/// <param name="Outcome">The non-disclosing outcome.</param>
/// <param name="Version">The resulting version only after success.</param>
public sealed record AddTransportationSegmentResult(
    AddTransportationSegmentOutcome Outcome,
    long? Version = null)
{
    /// <summary>Gets the non-disclosing result category.</summary>
    public AddTransportationSegmentOutcome Outcome { get; } = Outcome;
    /// <summary>Gets the resulting version only after success.</summary>
    public long? Version { get; } = Version;
}

/// <summary>Adds provider-neutral transportation segments to private Adventure Plans.</summary>
public interface ITransportationSegmentAddService
{
    /// <summary>Authorizes, validates, and atomically adds one transportation segment.</summary>
    Task<AddTransportationSegmentResult> AddAsync(
        AddTransportationSegmentCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements instance-authorized optimistic transportation creation.</summary>
public sealed class TransportationSegmentAddService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IPlanningTransactionFactory transactionFactory,
    IPlanningCreationIdentityGenerator identityGenerator,
    TimeProvider timeProvider) : ITransportationSegmentAddService
{
    /// <inheritdoc />
    public async Task<AddTransportationSegmentResult> AddAsync(
        AddTransportationSegmentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null || command.Actor is null || !command.Actor.IsHuman
            || !command.Actor.UserId.HasValue || command.CreatorId == default
            || command.AdventurePlanId == default)
        {
            return Result(AddTransportationSegmentOutcome.Denied);
        }

        if (!TryValidate(command, out var departureZone, out var arrivalZone))
        {
            return Result(AddTransportationSegmentOutcome.ValidationFailed);
        }

        try
        {
            var membership = await membershipProvider.GetMembershipAsync(
                command.Actor.UserId.Value, command.CreatorId, cancellationToken);
            if (membership is null)
            {
                return Result(AddTransportationSegmentOutcome.Denied);
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
                return Result(AddTransportationSegmentOutcome.Denied);
            }

            await using var transaction = await transactionFactory.BeginAsync(
                command.CreatorId, cancellationToken);
            var current = await transaction.AdventurePlans.GetAsync(
                command.CreatorId, command.AdventurePlanId, cancellationToken);
            if (current is null || current.CreatorId != command.CreatorId
                || current.Id != command.AdventurePlanId
                || current.Status == PlanningStatus.Archived)
            {
                return Result(AddTransportationSegmentOutcome.Denied);
            }

            if (current.Audit.Version != command.ExpectedVersion)
            {
                return Result(AddTransportationSegmentOutcome.Conflict);
            }

            if (!current.Dates.Contains(command.DepartureDate)
                || !current.Dates.Contains(command.ArrivalDate))
            {
                return Result(AddTransportationSegmentOutcome.ValidationFailed);
            }

            if (!ReferencesVisit(current, command.DepartureDestinationVisitId)
                || !ReferencesVisit(current, command.ArrivalDestinationVisitId))
            {
                return Result(AddTransportationSegmentOutcome.Denied);
            }

            var segment = new TransportationSegment
            {
                Id = identityGenerator.NewTransportationSegmentId(),
                DepartureDestinationVisitId = command.DepartureDestinationVisitId,
                ArrivalDestinationVisitId = command.ArrivalDestinationVisitId,
                Mode = command.Mode,
                From = command.From,
                To = command.To,
                DepartureDate = command.DepartureDate,
                DepartureTimeLocal = command.DepartureTimeLocal,
                DepartureTimeZone = departureZone,
                ArrivalDate = command.ArrivalDate,
                ArrivalTimeLocal = command.ArrivalTimeLocal,
                ArrivalTimeZone = arrivalZone,
                Status = PlanItemStatus.Proposed
            };
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var updated = current.WithTransportationSegment(segment, now);
            await transaction.AdventurePlans.AddTransportationSegmentAsync(
                command.CreatorId, updated, segment, command.ExpectedVersion, cancellationToken);
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
            return new(AddTransportationSegmentOutcome.Added, updated.Audit.Version);
        }
        catch (PlanningConcurrencyException)
        {
            return Result(AddTransportationSegmentOutcome.Conflict);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(AddTransportationSegmentOutcome.Failed);
        }
    }

    private static bool ReferencesVisit(AdventurePlan plan, DestinationVisitId? visitId) =>
        visitId is null || plan.DestinationVisits.Any(visit => visit.Id == visitId.Value);

    private static bool TryValidate(
        AddTransportationSegmentCommand command,
        out IanaTimeZone departureZone,
        out IanaTimeZone arrivalZone)
    {
        departureZone = default;
        arrivalZone = default;
        if (command.ExpectedVersion < 1
            || !ValidText(command.Mode, 100) || !ValidText(command.From, 200)
            || !ValidText(command.To, 200) || command.ArrivalDate < command.DepartureDate
            || (command.ArrivalDate == command.DepartureDate
                && command.DepartureTimeZoneId == command.ArrivalTimeZoneId
                && command.DepartureTimeLocal is { } departure
                && command.ArrivalTimeLocal is { } arrival && arrival < departure))
        {
            return false;
        }

        try
        {
            departureZone = new(command.DepartureTimeZoneId);
            arrivalZone = new(command.ArrivalTimeZoneId);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool ValidText(string value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value == value.Trim() && value.Length <= maximumLength;

    private static AddTransportationSegmentResult Result(
        AddTransportationSegmentOutcome outcome) => new(outcome);
}
