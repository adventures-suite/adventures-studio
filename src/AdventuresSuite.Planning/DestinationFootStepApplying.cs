using System.Security.Cryptography;
using System.Text;
using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Contains the reviewed, allowlisted parameters for applying one Destination FootStep.</summary>
/// <param name="Actor">The authenticated human actor.</param>
/// <param name="CreatorId">The customer Creator ownership boundary.</param>
/// <param name="AdventurePlanId">The exact private plan instance.</param>
/// <param name="ExpectedVersion">The plan version shown during review.</param>
/// <param name="IdempotencyKey">The retry key created for the reviewed form.</param>
/// <param name="FootStepId">The immutable source identity.</param>
/// <param name="FootStepVersion">The exact immutable source version.</param>
/// <param name="StartDate">The reviewed inclusive destination start date.</param>
/// <param name="EndDate">The reviewed inclusive destination end date.</param>
/// <param name="TimeZoneId">The source-fixed IANA time-zone identifier.</param>
public sealed record ApplyDestinationFootStepCommand(
    ActorIdentity Actor,
    CreatorId CreatorId,
    AdventurePlanId AdventurePlanId,
    long ExpectedVersion,
    PlanningIdempotencyKey IdempotencyKey,
    string FootStepId,
    string FootStepVersion,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId);

/// <summary>Classifies non-disclosing Destination FootStep application outcomes.</summary>
public enum ApplyDestinationFootStepOutcome
{
    /// <summary>The destination, provenance, and audit intent committed.</summary>
    Added,
    /// <summary>The identical previously committed result was returned.</summary>
    Replayed,
    /// <summary>Authorization, ownership, or exact source use could not be established.</summary>
    Denied,
    /// <summary>The plan version or idempotency key conflicted.</summary>
    Conflict,
    /// <summary>The reviewed parameters were invalid.</summary>
    ValidationFailed,
    /// <summary>The operation failed without committing authoritative state.</summary>
    Failed
}

/// <summary>Returns only disclosure-safe Destination FootStep application data.</summary>
/// <param name="Outcome">The non-disclosing application outcome.</param>
/// <param name="Version">The committed or replayed plan version when available.</param>
public sealed record ApplyDestinationFootStepResult(
    ApplyDestinationFootStepOutcome Outcome,
    long? Version = null);

/// <summary>Applies exact-version Destination FootSteps to private Adventure Plans.</summary>
public interface IDestinationFootStepApplyService
{
    /// <summary>Reauthorizes, validates, and atomically applies one reviewed FootStep.</summary>
    Task<ApplyDestinationFootStepResult> ApplyAsync(
        ApplyDestinationFootStepCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements retry-safe Destination FootStep application with durable provenance.</summary>
public sealed class DestinationFootStepApplyService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IPlannerFootStepUseResolver useResolver,
    IPlanningTransactionFactory transactionFactory,
    IPlanningCreationIdentityGenerator identityGenerator,
    TimeProvider timeProvider) : IDestinationFootStepApplyService
{
    /// <inheritdoc />
    public async Task<ApplyDestinationFootStepResult> ApplyAsync(
        ApplyDestinationFootStepCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null || command.Actor is null || !command.Actor.IsHuman
            || !command.Actor.UserId.HasValue || command.CreatorId == default
            || command.AdventurePlanId == default || command.IdempotencyKey == default
            || command.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(command.FootStepId)
            || string.IsNullOrWhiteSpace(command.FootStepVersion))
        {
            return Result(ApplyDestinationFootStepOutcome.Denied);
        }

        try
        {
            var membership = await membershipProvider.GetMembershipAsync(
                command.Actor.UserId.Value, command.CreatorId, cancellationToken);
            if (membership is null) return Result(ApplyDestinationFootStepOutcome.Denied);
            var decision = await authorizationPolicyEvaluator.AuthorizeAsync(
                new AuthorizationRequest(command.Actor, Permissions.AdventurePlanEdit,
                    AuthorizationResourceScope.ForInstance(command.CreatorId,
                        AuthorizationResourceTypes.AdventurePlan, command.AdventurePlanId.Value),
                    membershipVersion: membership.Version), cancellationToken);
            if (!decision.IsAllowed
                || decision.AuditRequirement != AuthorizationAuditRequirement.RequiredMutation)
            {
                return Result(ApplyDestinationFootStepOutcome.Denied);
            }

            var use = await useResolver.ResolveAsync(command.Actor, command.CreatorId,
                command.FootStepId, command.FootStepVersion, cancellationToken);
            if (use?.FootStep.DestinationDraft is not { } draft
                || use.FootStep.Id != command.FootStepId
                || use.FootStep.Version != command.FootStepVersion
                || string.IsNullOrWhiteSpace(use.UseDecisionReference)
                || !string.Equals(draft.TimeZoneId, command.TimeZoneId, StringComparison.Ordinal))
            {
                return Result(ApplyDestinationFootStepOutcome.Denied);
            }

            PlanningDateRange dates;
            IanaTimeZone timeZone;
            try
            {
                dates = new(command.StartDate, command.EndDate);
                timeZone = new(command.TimeZoneId);
            }
            catch (ArgumentException)
            {
                return Result(ApplyDestinationFootStepOutcome.ValidationFailed);
            }

            await using var transaction = await transactionFactory.BeginAsync(command.CreatorId, cancellationToken);
            var current = await transaction.AdventurePlans.GetAsync(
                command.CreatorId, command.AdventurePlanId, cancellationToken);
            if (current is null || current.CreatorId != command.CreatorId
                || current.Id != command.AdventurePlanId || current.Status == PlanningStatus.Archived)
            {
                return Result(ApplyDestinationFootStepOutcome.Denied);
            }
            if (!current.Dates.Contains(dates)) return Result(ApplyDestinationFootStepOutcome.ValidationFailed);

            var destinationId = identityGenerator.NewDestinationVisitId();
            var fingerprint = CreateFingerprint(command, draft);
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var reservation = new PlannerFootStepApplicationReservation
            {
                AdventurePlanId = command.AdventurePlanId,
                IdempotencyKey = command.IdempotencyKey,
                Fingerprint = fingerprint,
                FootStepId = use.FootStep.Id,
                FootStepVersion = use.FootStep.Version,
                TargetType = "DestinationVisit",
                TargetId = destinationId.Value,
                ResultingVersion = checked(command.ExpectedVersion + 1),
                Attribution = use.FootStep.Attribution,
                UseDecisionReference = use.UseDecisionReference,
                AppliedAtUtc = now
            };
            var idempotency = await transaction.PlannerFootStepApplications.ResolveAsync(
                command.CreatorId, reservation, cancellationToken);
            if (idempotency.Outcome == PlannerFootStepApplicationOutcome.Conflict)
                return Result(ApplyDestinationFootStepOutcome.Conflict);
            if (idempotency.Outcome == PlannerFootStepApplicationOutcome.Replay)
                return new(ApplyDestinationFootStepOutcome.Replayed, idempotency.ResultingVersion);
            if (current.Audit.Version != command.ExpectedVersion)
                return Result(ApplyDestinationFootStepOutcome.Conflict);

            var visit = new DestinationVisit
            {
                Id = destinationId,
                Name = draft.Name,
                Dates = dates,
                TimeZone = timeZone,
                Sequence = current.DestinationVisits.Count == 0
                    ? 1
                    : checked(current.DestinationVisits.Max(item => item.Sequence) + 1)
            };
            var updated = current.WithDestinationVisit(visit, now);
            await transaction.AdventurePlans.AddDestinationVisitAsync(
                command.CreatorId, updated, visit, command.ExpectedVersion, cancellationToken);
            await transaction.PlannerFootStepApplications.AddAsync(
                command.CreatorId, reservation, cancellationToken);
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
            return new(ApplyDestinationFootStepOutcome.Added, updated.Audit.Version);
        }
        catch (PlanningConcurrencyException)
        {
            return Result(ApplyDestinationFootStepOutcome.Conflict);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(ApplyDestinationFootStepOutcome.Failed);
        }
    }

    private static PlanningRequestFingerprint CreateFingerprint(
        ApplyDestinationFootStepCommand command,
        PlannerFootStepDestinationDraft draft)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var value in new[]
        {
            "DestinationFootStep.Apply.v1", command.CreatorId.Value, command.AdventurePlanId.Value,
            command.ExpectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            command.FootStepId, command.FootStepVersion, draft.Name,
            command.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            command.EndDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            command.TimeZoneId
        })
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            hash.AppendData(BitConverter.GetBytes(bytes.Length));
            hash.AppendData(bytes);
        }
        return new(1, hash.GetHashAndReset());
    }

    private static ApplyDestinationFootStepResult Result(ApplyDestinationFootStepOutcome outcome) => new(outcome);
}
