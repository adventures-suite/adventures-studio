using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Contains the allowlisted desired state for editing one transportation segment.</summary>
/// <param name="Actor">The authenticated human actor.</param>
/// <param name="CreatorId">The explicit Creator scope.</param>
/// <param name="AdventurePlanId">The target plan.</param>
/// <param name="TransportationSegmentId">The transportation segment to edit.</param>
/// <param name="ExpectedVersion">The rendered plan version.</param>
/// <param name="Mode">The desired provider-neutral transportation mode.</param>
/// <param name="From">The desired departure place.</param>
/// <param name="To">The desired arrival place.</param>
/// <param name="DepartureDate">The desired local departure date.</param>
/// <param name="DepartureTimeLocal">The desired optional local departure time.</param>
/// <param name="DepartureTimeZoneId">The desired departure IANA time zone.</param>
/// <param name="ArrivalDate">The desired local arrival date.</param>
/// <param name="ArrivalTimeLocal">The desired optional local arrival time.</param>
/// <param name="ArrivalTimeZoneId">The desired arrival IANA time zone.</param>
public sealed record EditTransportationSegmentCommand(
    ActorIdentity Actor,
    CreatorId CreatorId,
    AdventurePlanId AdventurePlanId,
    TransportationSegmentId TransportationSegmentId,
    long ExpectedVersion,
    string Mode,
    string From,
    string To,
    DateOnly DepartureDate,
    TimeOnly? DepartureTimeLocal,
    string DepartureTimeZoneId,
    DateOnly ArrivalDate,
    TimeOnly? ArrivalTimeLocal,
    string ArrivalTimeZoneId);

/// <summary>Classifies non-disclosing transportation edit outcomes.</summary>
public enum EditTransportationSegmentOutcome
{
    /// <summary>The transportation segment and required audit event committed.</summary>
    Updated,
    /// <summary>The authoritative segment already had the requested values.</summary>
    Unchanged,
    /// <summary>Authorization or authoritative ownership could not be established.</summary>
    Denied,
    /// <summary>The submitted plan version was stale.</summary>
    Conflict,
    /// <summary>The submitted transportation fields were invalid.</summary>
    ValidationFailed,
    /// <summary>The operation failed without committing authoritative state.</summary>
    Failed
}

/// <summary>Returns only safe transportation edit result data.</summary>
/// <param name="Outcome">The non-disclosing result category.</param>
/// <param name="Version">The authoritative version after success or an unchanged replay.</param>
public sealed record EditTransportationSegmentResult(
    EditTransportationSegmentOutcome Outcome,
    long? Version = null);

/// <summary>Edits transportation details within private Adventure Plans.</summary>
public interface ITransportationSegmentEditService
{
    /// <summary>Authorizes, validates, and atomically edits one transportation segment.</summary>
    Task<EditTransportationSegmentResult> EditAsync(
        EditTransportationSegmentCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements instance-authorized optimistic transportation editing.</summary>
public sealed class TransportationSegmentEditService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IPlanningTransactionFactory transactionFactory,
    IPlanningCreationIdentityGenerator identityGenerator,
    TimeProvider timeProvider) : ITransportationSegmentEditService
{
    /// <inheritdoc />
    public async Task<EditTransportationSegmentResult> EditAsync(
        EditTransportationSegmentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null || command.Actor is null || !command.Actor.IsHuman
            || !command.Actor.UserId.HasValue || command.CreatorId == default
            || command.AdventurePlanId == default || command.TransportationSegmentId == default)
        {
            return Result(EditTransportationSegmentOutcome.Denied);
        }

        if (!TryValidate(command, out var departureZone, out var arrivalZone))
        {
            return Result(EditTransportationSegmentOutcome.ValidationFailed);
        }

        try
        {
            var membership = await membershipProvider.GetMembershipAsync(
                command.Actor.UserId.Value, command.CreatorId, cancellationToken);
            if (membership is null)
            {
                return Result(EditTransportationSegmentOutcome.Denied);
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
                return Result(EditTransportationSegmentOutcome.Denied);
            }

            await using var transaction = await transactionFactory.BeginAsync(
                command.CreatorId, cancellationToken);
            var current = await transaction.AdventurePlans.GetAsync(
                command.CreatorId, command.AdventurePlanId, cancellationToken);
            if (current is null || current.CreatorId != command.CreatorId
                || current.Id != command.AdventurePlanId || current.Status == PlanningStatus.Archived)
            {
                return Result(EditTransportationSegmentOutcome.Denied);
            }

            var segment = current.Transportation.SingleOrDefault(
                item => item.Id == command.TransportationSegmentId);
            if (segment is null)
            {
                return Result(EditTransportationSegmentOutcome.Denied);
            }

            if (!current.Dates.Contains(command.DepartureDate)
                || !current.Dates.Contains(command.ArrivalDate))
            {
                return Result(EditTransportationSegmentOutcome.ValidationFailed);
            }

            if (Matches(segment, command, departureZone, arrivalZone))
            {
                return new(EditTransportationSegmentOutcome.Unchanged, current.Audit.Version);
            }

            if (current.Audit.Version != command.ExpectedVersion)
            {
                return Result(EditTransportationSegmentOutcome.Conflict);
            }

            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var updated = current.WithEditedTransportationSegment(
                command.TransportationSegmentId, command.Mode, command.From, command.To,
                command.DepartureDate, command.DepartureTimeLocal, departureZone,
                command.ArrivalDate, command.ArrivalTimeLocal, arrivalZone, now);
            var edited = updated.Transportation.Single(
                item => item.Id == command.TransportationSegmentId);
            await transaction.AdventurePlans.UpdateTransportationSegmentAsync(
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
            return new(EditTransportationSegmentOutcome.Updated, updated.Audit.Version);
        }
        catch (PlanningConcurrencyException)
        {
            return Result(EditTransportationSegmentOutcome.Conflict);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(EditTransportationSegmentOutcome.Failed);
        }
    }

    private static bool Matches(
        TransportationSegment segment,
        EditTransportationSegmentCommand command,
        IanaTimeZone departureZone,
        IanaTimeZone arrivalZone) =>
        segment.Mode == command.Mode
        && segment.From == command.From
        && segment.To == command.To
        && segment.DepartureDate == command.DepartureDate
        && segment.DepartureTimeLocal == command.DepartureTimeLocal
        && segment.DepartureTimeZone == departureZone
        && segment.ArrivalDate == command.ArrivalDate
        && segment.ArrivalTimeLocal == command.ArrivalTimeLocal
        && segment.ArrivalTimeZone == arrivalZone;

    private static bool TryValidate(
        EditTransportationSegmentCommand command,
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

    private static EditTransportationSegmentResult Result(
        EditTransportationSegmentOutcome outcome) => new(outcome);
}
