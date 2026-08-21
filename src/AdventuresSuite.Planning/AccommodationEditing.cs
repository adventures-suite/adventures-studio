using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Contains the allowlisted desired state for editing one accommodation.</summary>
/// <param name="Actor">The authenticated human actor.</param>
/// <param name="CreatorId">The explicit Creator scope.</param>
/// <param name="AdventurePlanId">The target plan.</param>
/// <param name="AccommodationId">The accommodation to edit.</param>
/// <param name="ExpectedVersion">The rendered plan version.</param>
/// <param name="Name">The desired accommodation name.</param>
/// <param name="StartDate">The desired inclusive local start date.</param>
/// <param name="EndDate">The desired inclusive local end date.</param>
/// <param name="TimeZoneId">The desired property IANA time zone.</param>
public sealed record EditAccommodationCommand(
    ActorIdentity Actor,
    CreatorId CreatorId,
    AdventurePlanId AdventurePlanId,
    AccommodationId AccommodationId,
    long ExpectedVersion,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    DestinationVisitId? DestinationVisitId = null);

/// <summary>Classifies non-disclosing accommodation edit outcomes.</summary>
public enum EditAccommodationOutcome
{
    /// <summary>The accommodation and required audit event committed.</summary>
    Updated,
    /// <summary>The authoritative accommodation already had the requested values.</summary>
    Unchanged,
    /// <summary>Authorization or authoritative ownership could not be established.</summary>
    Denied,
    /// <summary>The submitted plan version was stale.</summary>
    Conflict,
    /// <summary>The submitted accommodation fields were invalid.</summary>
    ValidationFailed,
    /// <summary>The operation failed without committing authoritative state.</summary>
    Failed
}

/// <summary>Returns only safe accommodation edit result data.</summary>
/// <param name="Outcome">The non-disclosing result category.</param>
/// <param name="Version">The authoritative version after success or an unchanged replay.</param>
public sealed record EditAccommodationResult(EditAccommodationOutcome Outcome, long? Version = null);

/// <summary>Edits accommodation details within private Adventure Plans.</summary>
public interface IAccommodationEditService
{
    /// <summary>Authorizes, validates, and atomically edits one accommodation.</summary>
    Task<EditAccommodationResult> EditAsync(
        EditAccommodationCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements instance-authorized optimistic accommodation editing.</summary>
public sealed class AccommodationEditService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IPlanningTransactionFactory transactionFactory,
    IPlanningCreationIdentityGenerator identityGenerator,
    TimeProvider timeProvider) : IAccommodationEditService
{
    /// <inheritdoc />
    public async Task<EditAccommodationResult> EditAsync(
        EditAccommodationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null || command.Actor is null || !command.Actor.IsHuman
            || !command.Actor.UserId.HasValue || command.CreatorId == default
            || command.AdventurePlanId == default || command.AccommodationId == default)
        {
            return Result(EditAccommodationOutcome.Denied);
        }

        if (!TryValidate(command, out var dates, out var timeZone))
        {
            return Result(EditAccommodationOutcome.ValidationFailed);
        }

        try
        {
            var membership = await membershipProvider.GetMembershipAsync(
                command.Actor.UserId.Value, command.CreatorId, cancellationToken);
            if (membership is null)
            {
                return Result(EditAccommodationOutcome.Denied);
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
                return Result(EditAccommodationOutcome.Denied);
            }

            await using var transaction = await transactionFactory.BeginAsync(
                command.CreatorId, cancellationToken);
            var current = await transaction.AdventurePlans.GetAsync(
                command.CreatorId, command.AdventurePlanId, cancellationToken);
            if (current is null || current.CreatorId != command.CreatorId
                || current.Id != command.AdventurePlanId || current.Status == PlanningStatus.Archived)
            {
                return Result(EditAccommodationOutcome.Denied);
            }

            var accommodation = current.Accommodations.SingleOrDefault(
                item => item.Id == command.AccommodationId);
            if (accommodation is null)
            {
                return Result(EditAccommodationOutcome.Denied);
            }

            if (!current.Dates.Contains(dates))
            {
                return Result(EditAccommodationOutcome.ValidationFailed);
            }
            if (command.DestinationVisitId is { } destinationVisitId
                && current.DestinationVisits.All(visit => visit.Id != destinationVisitId))
            {
                return Result(EditAccommodationOutcome.Denied);
            }

            if (Matches(accommodation, command, dates, timeZone))
            {
                return new(EditAccommodationOutcome.Unchanged, current.Audit.Version);
            }

            if (current.Audit.Version != command.ExpectedVersion)
            {
                return Result(EditAccommodationOutcome.Conflict);
            }

            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var updated = current.WithEditedAccommodation(
                command.AccommodationId, command.Name, dates, timeZone, now,
                replaceDestinationAssociation: true,
                destinationVisitId: command.DestinationVisitId);
            var edited = updated.Accommodations.Single(item => item.Id == command.AccommodationId);
            await transaction.AdventurePlans.UpdateAccommodationAsync(
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
            return new(EditAccommodationOutcome.Updated, updated.Audit.Version);
        }
        catch (PlanningConcurrencyException)
        {
            return Result(EditAccommodationOutcome.Conflict);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(EditAccommodationOutcome.Failed);
        }
    }

    private static bool Matches(
        Accommodation accommodation,
        EditAccommodationCommand command,
        PlanningDateRange dates,
        IanaTimeZone timeZone) =>
        accommodation.Name == command.Name
        && accommodation.Dates == dates
        && accommodation.TimeZone == timeZone
        && accommodation.DestinationVisitId == command.DestinationVisitId;

    private static bool TryValidate(
        EditAccommodationCommand command,
        out PlanningDateRange dates,
        out IanaTimeZone timeZone)
    {
        dates = default;
        timeZone = default;
        if (command.ExpectedVersion < 1 || !ValidText(command.Name, 200)
            || command.EndDate < command.StartDate)
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

    private static bool ValidText(string value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value == value.Trim() && value.Length <= maximumLength;

    private static EditAccommodationResult Result(EditAccommodationOutcome outcome) => new(outcome);
}
