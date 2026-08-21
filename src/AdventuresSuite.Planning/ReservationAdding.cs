using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Contains allowlisted fields for one proposed reservation summary.</summary>
public sealed record AddReservationCommand
{
    /// <summary>Initializes one provider-neutral reservation request.</summary>
    public AddReservationCommand(
        ActorIdentity actor,
        CreatorId creatorId,
        AdventurePlanId adventurePlanId,
        long expectedVersion,
        string subject,
        DestinationVisitId? destinationVisitId = null)
    {
        Actor = actor;
        CreatorId = creatorId;
        AdventurePlanId = adventurePlanId;
        ExpectedVersion = expectedVersion;
        Subject = subject;
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
    /// <summary>Gets the reservation subject without confirmation credentials.</summary>
    public string Subject { get; init; }
    /// <summary>Gets the optional destination visit this summary supports.</summary>
    public DestinationVisitId? DestinationVisitId { get; init; }
}

/// <summary>Classifies non-disclosing reservation outcomes.</summary>
public enum AddReservationOutcome
{
    /// <summary>The reservation summary and audit event committed.</summary>
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

/// <summary>Returns only safe reservation result data.</summary>
public sealed record AddReservationResult
{
    /// <summary>Initializes one safe result.</summary>
    public AddReservationResult(AddReservationOutcome outcome, long? version = null)
    {
        Outcome = outcome;
        Version = version;
    }

    /// <summary>Gets the non-disclosing outcome.</summary>
    public AddReservationOutcome Outcome { get; }
    /// <summary>Gets the resulting version only after success.</summary>
    public long? Version { get; }
}

/// <summary>Adds credential-free proposed reservation summaries to private plans.</summary>
public interface IReservationAddService
{
    /// <summary>Authorizes, validates, and atomically adds one reservation summary.</summary>
    Task<AddReservationResult> AddAsync(
        AddReservationCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements instance-authorized optimistic reservation creation.</summary>
public sealed class ReservationAddService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IPlanningTransactionFactory transactionFactory,
    IPlanningCreationIdentityGenerator identityGenerator,
    TimeProvider timeProvider) : IReservationAddService
{
    /// <inheritdoc />
    public async Task<AddReservationResult> AddAsync(
        AddReservationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null || command.Actor is null || !command.Actor.IsHuman
            || !command.Actor.UserId.HasValue || command.CreatorId == default
            || command.AdventurePlanId == default)
        {
            return Result(AddReservationOutcome.Denied);
        }

        if (command.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(command.Subject)
            || command.Subject != command.Subject.Trim() || command.Subject.Length > 200)
        {
            return Result(AddReservationOutcome.ValidationFailed);
        }

        try
        {
            var membership = await membershipProvider.GetMembershipAsync(
                command.Actor.UserId.Value, command.CreatorId, cancellationToken);
            if (membership is null)
            {
                return Result(AddReservationOutcome.Denied);
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
                return Result(AddReservationOutcome.Denied);
            }

            await using var transaction = await transactionFactory.BeginAsync(
                command.CreatorId, cancellationToken);
            var current = await transaction.AdventurePlans.GetAsync(
                command.CreatorId, command.AdventurePlanId, cancellationToken);
            if (current is null || current.CreatorId != command.CreatorId
                || current.Id != command.AdventurePlanId
                || current.Status == PlanningStatus.Archived)
            {
                return Result(AddReservationOutcome.Denied);
            }
            if (current.Audit.Version != command.ExpectedVersion)
            {
                return Result(AddReservationOutcome.Conflict);
            }
            if (command.DestinationVisitId is { } visitId
                && current.DestinationVisits.All(visit => visit.Id != visitId))
            {
                return Result(AddReservationOutcome.Denied);
            }

            var reservation = new Reservation
            {
                Id = identityGenerator.NewReservationId(),
                DestinationVisitId = command.DestinationVisitId,
                Subject = command.Subject,
                ConfirmationReference = null,
                Status = PlanItemStatus.Proposed
            };
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var updated = current.WithReservation(reservation, now);
            await transaction.AdventurePlans.AddReservationAsync(
                command.CreatorId,
                updated,
                reservation,
                command.ExpectedVersion,
                cancellationToken);
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
            return new(AddReservationOutcome.Added, updated.Audit.Version);
        }
        catch (PlanningConcurrencyException)
        {
            return Result(AddReservationOutcome.Conflict);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(AddReservationOutcome.Failed);
        }
    }

    private static AddReservationResult Result(AddReservationOutcome outcome) => new(outcome);
}
