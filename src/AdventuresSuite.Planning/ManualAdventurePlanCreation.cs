using System.Security.Cryptography;
using System.Text;
using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Contains the minimum private fields accepted for manual Adventure Plan creation.</summary>
public sealed record ManualAdventurePlanCreateCommand(
    ActorIdentity Actor,
    CreatorId CreatorId,
    PlanningIdempotencyKey IdempotencyKey,
    string Title,
    string? WorkingDescription,
    DateOnly StartDate,
    DateOnly EndDate);

/// <summary>Classifies safe outcomes from manual Adventure Plan creation.</summary>
public enum ManualAdventurePlanCreateOutcome
{
    /// <summary>A new private plan was committed.</summary>
    Created,
    /// <summary>A retry returned the previously committed plan.</summary>
    Replayed,
    /// <summary>The actor was not authorized for the Creator collection.</summary>
    Denied,
    /// <summary>The idempotency key belongs to a different validated request.</summary>
    Conflict,
    /// <summary>The submitted fields were invalid.</summary>
    ValidationFailed,
    /// <summary>The operation failed without committing authoritative state.</summary>
    Failed
}

/// <summary>Returns only the safe result needed by the application boundary.</summary>
public sealed record ManualAdventurePlanCreateResult
{
    private ManualAdventurePlanCreateResult(
        ManualAdventurePlanCreateOutcome outcome,
        AdventurePlanId? adventurePlanId)
    {
        Outcome = outcome;
        AdventurePlanId = adventurePlanId;
    }

    /// <summary>Gets the safe operation outcome.</summary>
    public ManualAdventurePlanCreateOutcome Outcome { get; }

    /// <summary>Gets the committed plan identity only for created or replayed results.</summary>
    public AdventurePlanId? AdventurePlanId { get; }

    /// <summary>Creates a result that identifies the committed plan.</summary>
    public static ManualAdventurePlanCreateResult Success(
        ManualAdventurePlanCreateOutcome outcome,
        AdventurePlanId adventurePlanId)
    {
        if (outcome is not (ManualAdventurePlanCreateOutcome.Created
            or ManualAdventurePlanCreateOutcome.Replayed)
            || adventurePlanId == default)
        {
            throw new ArgumentException("A successful creation result requires a committed plan identity.");
        }

        return new(outcome, adventurePlanId);
    }

    /// <summary>Creates a safe result without protected state.</summary>
    public static ManualAdventurePlanCreateResult Safe(ManualAdventurePlanCreateOutcome outcome)
    {
        if (outcome is ManualAdventurePlanCreateOutcome.Created
            or ManualAdventurePlanCreateOutcome.Replayed
            || !Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        return new(outcome, null);
    }
}

/// <summary>Generates server-owned identities for one Planning mutation.</summary>
public interface IPlanningCreationIdentityGenerator
{
    /// <summary>Generates a new private Adventure Plan identity.</summary>
    AdventurePlanId NewAdventurePlanId();

    /// <summary>Generates a new private destination-visit identity.</summary>
    DestinationVisitId NewDestinationVisitId();

    /// <summary>Generates a new private itinerary-day identity.</summary>
    ItineraryDayId NewItineraryDayId();

    /// <summary>Generates a new private planned-activity identity.</summary>
    PlannedActivityId NewPlannedActivityId();

    /// <summary>Generates a new private transportation-segment identity.</summary>
    TransportationSegmentId NewTransportationSegmentId();

    /// <summary>Generates a new private accommodation identity.</summary>
    AccommodationId NewAccommodationId();

    /// <summary>Generates a new required audit-event identity.</summary>
    AuditEventId NewAuditEventId();

    /// <summary>Generates a new operation correlation identity.</summary>
    CorrelationId NewCorrelationId();
}

/// <summary>Generates cryptographically unpredictable server-owned Planning identities.</summary>
public sealed class GuidPlanningCreationIdentityGenerator : IPlanningCreationIdentityGenerator
{
    /// <inheritdoc />
    public AdventurePlanId NewAdventurePlanId() => new($"plan_{Guid.NewGuid():N}");

    /// <inheritdoc />
    public DestinationVisitId NewDestinationVisitId() => new($"visit_{Guid.NewGuid():N}");

    /// <inheritdoc />
    public ItineraryDayId NewItineraryDayId() => new($"day_{Guid.NewGuid():N}");

    /// <inheritdoc />
    public PlannedActivityId NewPlannedActivityId() => new($"activity_{Guid.NewGuid():N}");

    /// <inheritdoc />
    public TransportationSegmentId NewTransportationSegmentId() => new($"transport_{Guid.NewGuid():N}");

    /// <inheritdoc />
    public AccommodationId NewAccommodationId() => new($"accommodation_{Guid.NewGuid():N}");

    /// <inheritdoc />
    public AuditEventId NewAuditEventId() => new($"audit_{Guid.NewGuid():N}");

    /// <inheritdoc />
    public CorrelationId NewCorrelationId() => new($"correlation_{Guid.NewGuid():N}");
}

/// <summary>Creates private Adventure Plans through authorization and atomic persistence.</summary>
public interface IManualAdventurePlanCreateService
{
    /// <summary>Creates or safely replays one manual Adventure Plan request.</summary>
    Task<ManualAdventurePlanCreateResult> CreateAsync(
        ManualAdventurePlanCreateCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements the smallest authorized, audited, retry-safe Planning mutation.</summary>
public sealed class ManualAdventurePlanCreateService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IPlanningTransactionFactory transactionFactory,
    IPlanningCreationIdentityGenerator identityGenerator,
    TimeProvider timeProvider) : IManualAdventurePlanCreateService
{
    private const int FingerprintVersion = 1;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(30);

    /// <inheritdoc />
    public async Task<ManualAdventurePlanCreateResult> CreateAsync(
        ManualAdventurePlanCreateCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null
            || command.Actor is null
            || !command.Actor.IsHuman
            || !command.Actor.UserId.HasValue
            || command.CreatorId == default
            || command.IdempotencyKey == default)
        {
            return ManualAdventurePlanCreateResult.Safe(ManualAdventurePlanCreateOutcome.Denied);
        }

        try
        {
            var membership = await membershipProvider.GetMembershipAsync(
                command.Actor.UserId.Value,
                command.CreatorId,
                cancellationToken);
            if (membership is null)
            {
                return ManualAdventurePlanCreateResult.Safe(ManualAdventurePlanCreateOutcome.Denied);
            }

            var authorization = await authorizationPolicyEvaluator.AuthorizeAsync(
                new AuthorizationRequest(
                    command.Actor,
                    Permissions.AdventurePlanCreate,
                    AuthorizationResourceScope.ForCollection(
                        command.CreatorId,
                        AuthorizationResourceTypes.AdventurePlan),
                    membershipVersion: membership.Version),
                cancellationToken);
            if (!authorization.IsAllowed
                || authorization.AuditRequirement != AuthorizationAuditRequirement.RequiredMutation)
            {
                return ManualAdventurePlanCreateResult.Safe(ManualAdventurePlanCreateOutcome.Denied);
            }

            if (!TryValidate(command, out var description))
            {
                return ManualAdventurePlanCreateResult.Safe(
                    ManualAdventurePlanCreateOutcome.ValidationFailed);
            }

            var now = timeProvider.GetUtcNow();
            var planId = identityGenerator.NewAdventurePlanId();
            var fingerprint = CreateFingerprint(command, description);
            await using var transaction = await transactionFactory.BeginAsync(
                command.CreatorId,
                cancellationToken);
            var idempotency = await transaction.AdventurePlanCreateIdempotency.ReserveAsync(
                command.CreatorId,
                new AdventurePlanCreateReservation(
                    PlanningIdempotencyOperations.AdventurePlanCreateV1,
                    command.IdempotencyKey,
                    fingerprint,
                    planId,
                    resultingVersion: 1,
                    now,
                    now.Add(IdempotencyRetention)),
                cancellationToken);
            if (idempotency.Outcome == AdventurePlanCreateIdempotencyOutcome.Conflict)
            {
                return ManualAdventurePlanCreateResult.Safe(
                    ManualAdventurePlanCreateOutcome.Conflict);
            }

            if (idempotency.Outcome == AdventurePlanCreateIdempotencyOutcome.Replay)
            {
                return idempotency.AdventurePlanId.HasValue
                    ? ManualAdventurePlanCreateResult.Success(
                        ManualAdventurePlanCreateOutcome.Replayed,
                        idempotency.AdventurePlanId.Value)
                    : ManualAdventurePlanCreateResult.Safe(ManualAdventurePlanCreateOutcome.Failed);
            }

            var plan = new AdventurePlan(
                planId,
                command.CreatorId,
                command.Title,
                description,
                AdventureLifecycleStage.Plan,
                PlanningStatus.Draft,
                new PlanningDateRange(command.StartDate, command.EndDate),
                new PlanAudit(1, now, now));
            await transaction.AdventurePlans.AddAsync(
                command.CreatorId,
                plan,
                cancellationToken);
            transaction.RequiredAuditIntents.AddRequired(new AuditEventIntent(
                identityGenerator.NewAuditEventId(),
                command.Actor,
                command.CreatorId,
                Permissions.AdventurePlanCreate,
                AuthorizationResourceScope.ForInstance(
                    command.CreatorId,
                    AuthorizationResourceTypes.AdventurePlan,
                    planId.Value),
                AuditOutcome.Succeeded,
                AuditReasonCategory.Completed,
                now,
                identityGenerator.NewCorrelationId(),
                previousVersion: null,
                resultingVersion: 1));
            await transaction.CommitAsync(cancellationToken);
            return ManualAdventurePlanCreateResult.Success(
                ManualAdventurePlanCreateOutcome.Created,
                planId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return ManualAdventurePlanCreateResult.Safe(ManualAdventurePlanCreateOutcome.Failed);
        }
    }

    private static bool TryValidate(
        ManualAdventurePlanCreateCommand command,
        out string? description)
    {
        description = string.IsNullOrEmpty(command.WorkingDescription)
            ? null
            : command.WorkingDescription;
        return !string.IsNullOrWhiteSpace(command.Title)
            && command.Title == command.Title.Trim()
            && command.Title.Length <= 200
            && (description is null
                || (description == description.Trim() && description.Length <= 2000))
            && command.EndDate >= command.StartDate;
    }

    private static PlanningRequestFingerprint CreateFingerprint(
        ManualAdventurePlanCreateCommand command,
        string? description)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(PlanningIdempotencyOperations.AdventurePlanCreateV1);
            writer.Write(FingerprintVersion);
            writer.Write(command.CreatorId.Value);
            writer.Write(command.Actor.UserId!.Value.Value);
            writer.Write(command.Title);
            writer.Write(description is not null);
            if (description is not null)
            {
                writer.Write(description);
            }

            writer.Write(command.StartDate.DayNumber);
            writer.Write(command.EndDate.DayNumber);
            writer.Write(nameof(AdventureLifecycleStage.Plan));
            writer.Write(nameof(PlanningStatus.Draft));
            writer.Write(1L);
        }

        return new PlanningRequestFingerprint(
            FingerprintVersion,
            SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length))));
    }
}
