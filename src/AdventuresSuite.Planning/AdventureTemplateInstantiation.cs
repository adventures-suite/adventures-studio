using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Identifies one immutable published Adventure Template version.</summary>
public readonly record struct AdventureTemplateVersionId
{
    /// <summary>Initializes a case-sensitive template and version identity.</summary>
    public AdventureTemplateVersionId(string templateId, string version)
    {
        TemplateId = Require(templateId, 64, nameof(templateId));
        Version = Require(version, 32, nameof(version));
    }

    /// <summary>Gets the stable template identity.</summary>
    public string TemplateId { get; }

    /// <summary>Gets the exact immutable published version.</summary>
    public string Version { get; }

    private static string Require(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > maximumLength)
        {
            throw new ArgumentException("A bounded, trimmed identity is required.", parameterName);
        }

        return value;
    }
}

/// <summary>Classifies the authoring owner without conveying access to resulting plans.</summary>
public enum AdventureTemplateOwnerType
{
    /// <summary>The template is curated by AdventuresSuite.</summary>
    Platform,
    /// <summary>The template is owned by a customer Creator.</summary>
    Creator,
    /// <summary>The template is owned by a travel-professional Creator.</summary>
    Agency
}

/// <summary>Defines one destination relative to the instantiated plan start date.</summary>
/// <param name="Key">The template-local destination key.</param>
/// <param name="Name">The proposed destination name.</param>
/// <param name="StartDayOffset">The zero-based inclusive start offset.</param>
/// <param name="EndDayOffset">The zero-based inclusive end offset.</param>
/// <param name="TimeZone">The destination IANA time zone.</param>
/// <param name="Guidance">Optional planning guidance copied into private notes.</param>
/// <param name="UsesConfiguredOrigin">Whether the destination is materialized from the reviewed origin parameter.</param>
public sealed record AdventureTemplateDestination(
    string Key,
    string Name,
    int StartDayOffset,
    int EndDayOffset,
    IanaTimeZone TimeZone,
    string? Guidance = null,
    bool UsesConfiguredOrigin = false);

/// <summary>Contains the reviewed starting-place parameter for an origin-aware template.</summary>
/// <param name="Name">The bounded user-facing starting-place name.</param>
/// <param name="TimeZone">The starting place's reviewed IANA time zone.</param>
public sealed record AdventureTemplateConfiguredOrigin(
    string Name,
    IanaTimeZone TimeZone);

/// <summary>Contains the reviewed distance assumptions used to expand a round-trip Journey.</summary>
/// <param name="OneWayDistanceMiles">The estimated one-way road distance in whole miles.</param>
/// <param name="DailyDistanceMiles">The reviewed maximum planning distance per riding day.</param>
public sealed record AdventureTemplateTravelEstimate(
    int OneWayDistanceMiles,
    int DailyDistanceMiles)
{
    /// <summary>Gets the calculated number of riding days required in each direction.</summary>
    public int DaysEachWay => (int)Math.Ceiling((double)OneWayDistanceMiles / DailyDistanceMiles);
}

/// <summary>Defines one local itinerary day relative to the plan start date.</summary>
/// <param name="Key">The template-local day key.</param>
/// <param name="DayOffset">The zero-based date offset.</param>
/// <param name="DestinationKey">The optional template-local destination key.</param>
/// <param name="TimeZone">The local IANA time zone.</param>
/// <param name="Title">The proposed day title.</param>
public sealed record AdventureTemplateDay(
    string Key,
    int DayOffset,
    string? DestinationKey,
    IanaTimeZone TimeZone,
    string Title);

/// <summary>Defines one proposed activity attached to a template day.</summary>
/// <param name="DayKey">The template-local itinerary-day key.</param>
/// <param name="Title">The proposed activity title.</param>
/// <param name="StartsAtLocal">The optional local start time.</param>
/// <param name="EndsAtLocal">The optional local end time.</param>
public sealed record AdventureTemplateActivity(
    string DayKey,
    string Title,
    TimeOnly? StartsAtLocal = null,
    TimeOnly? EndsAtLocal = null);

/// <summary>Defines one provider-neutral proposed transportation pattern.</summary>
/// <param name="Mode">The provider-neutral transportation mode.</param>
/// <param name="From">The proposed departure description.</param>
/// <param name="To">The proposed arrival description.</param>
/// <param name="DepartureDayOffset">The zero-based departure-date offset.</param>
/// <param name="DepartureTimeLocal">The optional local departure time.</param>
/// <param name="DepartureTimeZone">The departure IANA time zone.</param>
/// <param name="ArrivalDayOffset">The zero-based arrival-date offset.</param>
/// <param name="ArrivalTimeLocal">The optional local arrival time.</param>
/// <param name="ArrivalTimeZone">The arrival IANA time zone.</param>
/// <param name="DepartureDestinationKey">The optional template destination from which the segment departs.</param>
/// <param name="ArrivalDestinationKey">The optional template destination at which the segment arrives.</param>
public sealed record AdventureTemplateTransportation(
    string Mode,
    string From,
    string To,
    int DepartureDayOffset,
    TimeOnly? DepartureTimeLocal,
    IanaTimeZone DepartureTimeZone,
    int ArrivalDayOffset,
    TimeOnly? ArrivalTimeLocal,
    IanaTimeZone ArrivalTimeZone,
    string? DepartureDestinationKey = null,
    string? ArrivalDestinationKey = null);

/// <summary>Defines one provider-neutral proposed stay pattern.</summary>
/// <param name="Name">The proposed accommodation name.</param>
/// <param name="StartDayOffset">The zero-based inclusive start-date offset.</param>
/// <param name="EndDayOffset">The zero-based inclusive end-date offset.</param>
/// <param name="TimeZone">The accommodation IANA time zone.</param>
/// <param name="DestinationKey">The optional template destination containing the stay.</param>
public sealed record AdventureTemplateAccommodation(
    string Name,
    int StartDayOffset,
    int EndDayOffset,
    IanaTimeZone TimeZone,
    string? DestinationKey = null);

/// <summary>Contains a validated immutable blueprint safe for plan materialization.</summary>
public sealed record AdventureTemplateBlueprint
{
    /// <summary>Gets the exact published template version.</summary>
    public required AdventureTemplateVersionId VersionId { get; init; }
    /// <summary>Gets the source owner classification.</summary>
    public required AdventureTemplateOwnerType OwnerType { get; init; }
    /// <summary>Gets the stable source-owner identity.</summary>
    public required string OwnerId { get; init; }
    /// <summary>Gets the BCP 47 source content locale.</summary>
    public required string SourceLocale { get; init; }
    /// <summary>Gets the immutable attribution snapshot.</summary>
    public required string Attribution { get; init; }
    /// <summary>Gets the proposed private plan title.</summary>
    public required string Title { get; init; }
    /// <summary>Gets the optional proposed private working description.</summary>
    public string? WorkingDescription { get; init; }
    /// <summary>Gets the inclusive template duration in days.</summary>
    public required int DurationDays { get; init; }
    /// <summary>Gets ordered destination blueprints.</summary>
    public IReadOnlyList<AdventureTemplateDestination> Destinations { get; init; } = [];
    /// <summary>Gets itinerary-day blueprints.</summary>
    public IReadOnlyList<AdventureTemplateDay> Days { get; init; } = [];
    /// <summary>Gets proposed activity blueprints.</summary>
    public IReadOnlyList<AdventureTemplateActivity> Activities { get; init; } = [];
    /// <summary>Gets proposed transportation blueprints.</summary>
    public IReadOnlyList<AdventureTemplateTransportation> Transportation { get; init; } = [];
    /// <summary>Gets proposed accommodation blueprints.</summary>
    public IReadOnlyList<AdventureTemplateAccommodation> Accommodations { get; init; } = [];
    /// <summary>Gets whether this template requires a reviewed starting place and time zone.</summary>
    public bool RequiresConfiguredOrigin => Destinations.Any(item => item.UsesConfiguredOrigin);
}

/// <summary>Returns an authorized template together with the use-decision evidence.</summary>
/// <param name="Template">The authorized immutable blueprint.</param>
/// <param name="UseDecisionReference">The bounded authorization, entitlement, and license decision reference.</param>
public sealed record AuthorizedAdventureTemplateUse(
    AdventureTemplateBlueprint Template,
    string UseDecisionReference);

/// <summary>Resolves template use only after source authorization, entitlement, and licensing.</summary>
public interface IAdventureTemplateUseResolver
{
    /// <summary>Returns an approved immutable template use or no result without source disclosure.</summary>
    Task<AuthorizedAdventureTemplateUse?> ResolveAsync(
        ActorIdentity actor,
        CreatorId customerCreatorId,
        AdventureTemplateVersionId templateVersion,
        string requestedLocale,
        CancellationToken cancellationToken = default);
}

/// <summary>Requests creation of one independent private plan from an exact template version.</summary>
/// <param name="Actor">The authenticated human actor.</param>
/// <param name="CreatorId">The customer Creator that will own the new plan.</param>
/// <param name="IdempotencyKey">The Creator-scoped retry key.</param>
/// <param name="TemplateVersion">The exact immutable source template version.</param>
/// <param name="StartDate">The requested local calendar start date.</param>
/// <param name="RequestedLocale">The requested BCP 47 presentation locale.</param>
/// <param name="ConfiguredOrigin">The optional reviewed origin required by an origin-aware template.</param>
/// <param name="TravelEstimate">The optional reviewed road-distance assumptions for round-trip adaptation.</param>
public sealed record AdventureTemplateInstantiateCommand(
    ActorIdentity Actor,
    CreatorId CreatorId,
    PlanningIdempotencyKey IdempotencyKey,
    AdventureTemplateVersionId TemplateVersion,
    DateOnly StartDate,
    string RequestedLocale,
    AdventureTemplateConfiguredOrigin? ConfiguredOrigin = null,
    AdventureTemplateTravelEstimate? TravelEstimate = null);

/// <summary>Classifies safe template-instantiation outcomes.</summary>
public enum AdventureTemplateInstantiateOutcome
{
    /// <summary>A new independent private plan was committed.</summary>
    Created,
    /// <summary>An exact retry returned the original committed plan.</summary>
    Replayed,
    /// <summary>The actor or source use was not authorized.</summary>
    Denied,
    /// <summary>The idempotency key belongs to another request.</summary>
    Conflict,
    /// <summary>The request or resolved blueprint was invalid.</summary>
    ValidationFailed,
    /// <summary>The operation failed without committing partial state.</summary>
    Failed
}

/// <summary>Returns only the safe result needed by the web boundary.</summary>
/// <param name="Outcome">The disclosure-safe result classification.</param>
/// <param name="AdventurePlanId">The created or replayed customer-owned plan identity.</param>
public sealed record AdventureTemplateInstantiateResult(
    AdventureTemplateInstantiateOutcome Outcome,
    AdventurePlanId? AdventurePlanId);

/// <summary>Creates independent private plans from authorized immutable templates.</summary>
public interface IAdventureTemplateInstantiateService
{
    /// <summary>Creates or safely replays one template-instantiation request.</summary>
    Task<AdventureTemplateInstantiateResult> InstantiateAsync(
        AdventureTemplateInstantiateCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements authorized, retry-safe, atomic Adventure Template instantiation.</summary>
public sealed class AdventureTemplateInstantiateService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IAdventureTemplateUseResolver templateUseResolver,
    IPlanningTransactionFactory transactionFactory,
    IPlanningCreationIdentityGenerator identityGenerator,
    TimeProvider timeProvider) : IAdventureTemplateInstantiateService
{
    private const int FingerprintVersion = 1;
    private const int OriginFingerprintVersion = 2;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(30);

    /// <inheritdoc />
    public async Task<AdventureTemplateInstantiateResult> InstantiateAsync(
        AdventureTemplateInstantiateCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null || command.Actor is null || !command.Actor.IsHuman
            || !command.Actor.UserId.HasValue || command.CreatorId == default
            || command.IdempotencyKey == default || command.TemplateVersion == default
            || string.IsNullOrWhiteSpace(command.RequestedLocale))
        {
            return Safe(AdventureTemplateInstantiateOutcome.Denied);
        }

        try
        {
            var membership = await membershipProvider.GetMembershipAsync(
                command.Actor.UserId.Value, command.CreatorId, cancellationToken);
            if (membership is null)
            {
                return Safe(AdventureTemplateInstantiateOutcome.Denied);
            }

            var authorization = await authorizationPolicyEvaluator.AuthorizeAsync(
                new AuthorizationRequest(
                    command.Actor,
                    Permissions.AdventurePlanCreate,
                    AuthorizationResourceScope.ForCollection(
                        command.CreatorId, AuthorizationResourceTypes.AdventurePlan),
                    membershipVersion: membership.Version),
                cancellationToken);
            if (!authorization.IsAllowed
                || authorization.AuditRequirement != AuthorizationAuditRequirement.RequiredMutation)
            {
                return Safe(AdventureTemplateInstantiateOutcome.Denied);
            }

            var use = await templateUseResolver.ResolveAsync(
                command.Actor, command.CreatorId, command.TemplateVersion,
                command.RequestedLocale, cancellationToken);
            if (use is null)
            {
                return Safe(AdventureTemplateInstantiateOutcome.Denied);
            }

            if (!TryMaterialize(command, use, out var materialized))
            {
                return Safe(AdventureTemplateInstantiateOutcome.ValidationFailed);
            }

            var now = timeProvider.GetUtcNow();
            var planId = identityGenerator.NewAdventurePlanId();
            var fingerprint = CreateFingerprint(command, use);
            await using var transaction = await transactionFactory.BeginAsync(
                command.CreatorId, cancellationToken);
            var idempotency = await transaction.AdventurePlanCreateIdempotency.ReserveAsync(
                command.CreatorId,
                new AdventurePlanCreateReservation(
                    PlanningIdempotencyOperations.AdventurePlanTemplateInstantiateV1,
                    command.IdempotencyKey, fingerprint, planId, 1, now,
                    now.Add(IdempotencyRetention)),
                cancellationToken);
            if (idempotency.Outcome == AdventurePlanCreateIdempotencyOutcome.Conflict)
            {
                return Safe(AdventureTemplateInstantiateOutcome.Conflict);
            }

            if (idempotency.Outcome == AdventurePlanCreateIdempotencyOutcome.Replay)
            {
                return idempotency.AdventurePlanId.HasValue
                    ? new(AdventureTemplateInstantiateOutcome.Replayed, idempotency.AdventurePlanId)
                    : Safe(AdventureTemplateInstantiateOutcome.Failed);
            }

            var plan = materialized(planId, now, identityGenerator);
            await transaction.AdventurePlans.AddAsync(command.CreatorId, plan, cancellationToken);
            await transaction.AdventurePlanTemplateOrigins.AddAsync(
                command.CreatorId,
                new AdventurePlanTemplateOrigin
                {
                    CreatorId = command.CreatorId,
                    AdventurePlanId = planId,
                    TemplateVersion = use.Template.VersionId,
                    TemplateOwnerType = use.Template.OwnerType,
                    TemplateOwnerId = use.Template.OwnerId,
                    SourceLocale = use.Template.SourceLocale,
                    Attribution = use.Template.Attribution,
                    UseDecisionReference = use.UseDecisionReference,
                    ParameterFingerprint = fingerprint,
                    InstantiatedAtUtc = now
                }, cancellationToken);
            transaction.RequiredAuditIntents.AddRequired(new AuditEventIntent(
                identityGenerator.NewAuditEventId(), command.Actor, command.CreatorId,
                Permissions.AdventurePlanCreate,
                AuthorizationResourceScope.ForInstance(
                    command.CreatorId, AuthorizationResourceTypes.AdventurePlan, planId.Value),
                AuditOutcome.Succeeded, AuditReasonCategory.Completed, now,
                identityGenerator.NewCorrelationId(),
                previousVersion: null,
                resultingVersion: 1));
            await transaction.CommitAsync(cancellationToken);
            return new(AdventureTemplateInstantiateOutcome.Created, planId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Safe(AdventureTemplateInstantiateOutcome.Failed);
        }
    }

    private static AdventureTemplateInstantiateResult Safe(AdventureTemplateInstantiateOutcome outcome) =>
        new(outcome, null);

    private static bool TryMaterialize(
        AdventureTemplateInstantiateCommand command,
        AuthorizedAdventureTemplateUse use,
        out Func<AdventurePlanId, DateTimeOffset, IPlanningCreationIdentityGenerator, AdventurePlan> materialize)
    {
        materialize = null!;
        var template = use.Template;
        if (template is null || template.VersionId != command.TemplateVersion
            || template.DurationDays < 1 || template.DurationDays > 366
            || !Enum.IsDefined(template.OwnerType)
            || !ValidText(template.Title, 200) || !OptionalText(template.WorkingDescription, 2000)
            || !ValidText(template.OwnerId, 64) || !ValidLocale(template.SourceLocale)
            || !ValidText(template.Attribution, 300)
            || !ValidText(use.UseDecisionReference, 128)
            || template.Destinations is null || template.Days is null
            || template.Activities is null || template.Transportation is null
            || template.Accommodations is null)
        {
            return false;
        }

        // Snapshot every collection before validation so a resolver cannot change
        // the authorized blueprint between validation, fingerprinting, and persistence.
        var destinations = template.Destinations.ToArray();
        var days = template.Days.ToArray();
        var activities = template.Activities.ToArray();
        var transportation = template.Transportation.ToArray();
        var accommodations = template.Accommodations.ToArray();
        var originDestinations = destinations.Where(item => item.UsesConfiguredOrigin).ToArray();
        var travelDaysEachWay = command.TravelEstimate?.DaysEachWay ?? 1;
        var travelExpansionDays = travelDaysEachWay - 1;
        var lastOriginOffset = originDestinations.Length == 0
            ? int.MaxValue
            : originDestinations.Max(item => item.StartDayOffset);
        int AdaptOffset(int offset) => offset >= lastOriginOffset
            ? offset + (2 * travelExpansionDays)
            : offset > 0
                ? offset + travelExpansionDays
                : offset;
        int AdaptTransportationDepartureOffset(AdventureTemplateTransportation item) =>
            item.ArrivalDestinationKey is not null
            && destinations.First(destination => destination.Key == item.ArrivalDestinationKey)
                .UsesConfiguredOrigin
            && item.DepartureDestinationKey is not null
            && !destinations.First(destination => destination.Key == item.DepartureDestinationKey)
                .UsesConfiguredOrigin
                ? item.DepartureDayOffset + travelExpansionDays
                : AdaptOffset(item.DepartureDayOffset);
        if (destinations.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != destinations.Length
            || days.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != days.Length
            || destinations.Any(item => !ValidText(item.Key, 64) || !ValidText(item.Name, 200)
                || item.StartDayOffset < 0 || item.EndDayOffset < item.StartDayOffset
                || item.EndDayOffset >= template.DurationDays)
            || days.Any(item => !ValidText(item.Key, 64) || !ValidText(item.Title, 200)
                || item.DayOffset < 0 || item.DayOffset >= template.DurationDays
                || (item.DestinationKey is not null
                    && !destinations.Any(destination => destination.Key == item.DestinationKey)))
            || activities.Any(item => !days.Any(day => day.Key == item.DayKey)
                || !ValidText(item.Title, 200)
                || (item.StartsAtLocal.HasValue && item.EndsAtLocal.HasValue
                    && item.EndsAtLocal < item.StartsAtLocal))
            || transportation.Any(item => !ValidText(item.Mode, 100)
                || !ValidText(item.From, 200) || !ValidText(item.To, 200)
                || item.DepartureDayOffset < 0
                || item.ArrivalDayOffset < item.DepartureDayOffset
                || item.ArrivalDayOffset >= template.DurationDays
                || (item.DepartureDestinationKey is not null
                    && !destinations.Any(destination => destination.Key == item.DepartureDestinationKey))
                || (item.ArrivalDestinationKey is not null
                    && !destinations.Any(destination => destination.Key == item.ArrivalDestinationKey)))
            || accommodations.Any(item => !ValidText(item.Name, 200)
                || item.StartDayOffset < 0 || item.EndDayOffset < item.StartDayOffset
                || item.EndDayOffset >= template.DurationDays
                || (item.DestinationKey is not null
                    && !destinations.Any(destination => destination.Key == item.DestinationKey)))
            || (originDestinations.Length == 0) != (command.ConfiguredOrigin is null)
            || (command.ConfiguredOrigin is not null
                && (!ValidText(command.ConfiguredOrigin.Name, 200)
                    || command.ConfiguredOrigin.TimeZone == default))
            || (command.TravelEstimate is not null) != (originDestinations.Length == 2)
            || (command.TravelEstimate is not null
                && (command.TravelEstimate.OneWayDistanceMiles < 25
                    || command.TravelEstimate.OneWayDistanceMiles > 10000
                    || command.TravelEstimate.DailyDistanceMiles < 100
                    || command.TravelEstimate.DailyDistanceMiles > 1000
                    || travelDaysEachWay > 30)))
        {
            return false;
        }

        var adaptedDays = days
            .Select(item => item with { DayOffset = AdaptOffset(item.DayOffset) })
            .Concat(Enumerable.Range(1, travelExpansionDays).Select(index =>
                new AdventureTemplateDay(
                    $"adapt-outbound-{index + 1}", index, null,
                    command.ConfiguredOrigin!.TimeZone,
                    $"Ride toward the destination — day {index + 1} of {travelDaysEachWay}")))
            .Concat(Enumerable.Range(1, travelExpansionDays).Select(index =>
                new AdventureTemplateDay(
                    $"adapt-return-{index}", lastOriginOffset + travelExpansionDays + index - 1,
                    null, command.ConfiguredOrigin!.TimeZone,
                    $"Ride toward home — day {index} of {travelDaysEachWay}")))
            .OrderBy(item => item.DayOffset)
            .ToArray();
        var adaptedDurationDays = template.DurationDays + (2 * travelExpansionDays);

        materialize = (planId, now, ids) =>
        {
            var visitIds = destinations.ToDictionary(
                item => item.Key, _ => ids.NewDestinationVisitId(), StringComparer.Ordinal);
            var dayIds = adaptedDays.ToDictionary(
                item => item.Key, _ => ids.NewItineraryDayId(), StringComparer.Ordinal);
            string DestinationName(AdventureTemplateDestination item) =>
                item.UsesConfiguredOrigin ? command.ConfiguredOrigin!.Name : item.Name;

            IanaTimeZone DestinationTimeZone(AdventureTemplateDestination item) =>
                item.UsesConfiguredOrigin ? command.ConfiguredOrigin!.TimeZone : item.TimeZone;

            var destinationByKey = destinations.ToDictionary(item => item.Key, StringComparer.Ordinal);
            string TransportationPlace(string value, string? destinationKey) =>
                destinationKey is not null
                && destinationByKey[destinationKey].UsesConfiguredOrigin
                    ? command.ConfiguredOrigin!.Name
                    : value;
            IanaTimeZone TransportationTimeZone(IanaTimeZone value, string? destinationKey) =>
                destinationKey is not null
                && destinationByKey[destinationKey].UsesConfiguredOrigin
                    ? command.ConfiguredOrigin!.TimeZone
                    : value;
            IanaTimeZone ItineraryTimeZone(AdventureTemplateDay item) =>
                item.DestinationKey is not null
                && destinationByKey[item.DestinationKey].UsesConfiguredOrigin
                    ? command.ConfiguredOrigin!.TimeZone
                    : item.TimeZone;

            return new AdventurePlan(
                planId, command.CreatorId, template.Title, template.WorkingDescription,
                AdventureLifecycleStage.Plan, PlanningStatus.Draft,
                new PlanningDateRange(
                    command.StartDate, command.StartDate.AddDays(adaptedDurationDays - 1)),
                new PlanAudit(1, now, now),
                destinationVisits: destinations.Select((item, index) => new DestinationVisit
                {
                    Id = visitIds[item.Key],
                    Name = DestinationName(item),
                    Dates = new PlanningDateRange(
                        command.StartDate.AddDays(AdaptOffset(item.StartDayOffset)),
                        command.StartDate.AddDays(AdaptOffset(item.EndDayOffset))),
                    TimeZone = DestinationTimeZone(item),
                    Sequence = index + 1,
                    Notes = item.Guidance
                }).ToArray(),
                itineraryDays: adaptedDays.Select(item => new ItineraryDay
                {
                    Id = dayIds[item.Key],
                    Date = command.StartDate.AddDays(item.DayOffset),
                    DestinationVisitId = item.DestinationKey is null ? null : visitIds[item.DestinationKey],
                    TimeZone = ItineraryTimeZone(item),
                    Title = item.Title
                }).ToArray(),
                activities: activities.Select(item => new PlannedActivity
                {
                    Id = ids.NewPlannedActivityId(),
                    ItineraryDayId = dayIds[item.DayKey],
                    Title = item.Title,
                    StartsAtLocal = item.StartsAtLocal,
                    EndsAtLocal = item.EndsAtLocal,
                    Status = PlanItemStatus.Proposed
                }).ToArray(),
                transportation: transportation.Select(item => new TransportationSegment
                {
                    Id = ids.NewTransportationSegmentId(),
                    DepartureDestinationVisitId = item.DepartureDestinationKey is null ? null : visitIds[item.DepartureDestinationKey],
                    ArrivalDestinationVisitId = item.ArrivalDestinationKey is null ? null : visitIds[item.ArrivalDestinationKey],
                    Mode = item.Mode,
                    From = TransportationPlace(item.From, item.DepartureDestinationKey),
                    To = TransportationPlace(item.To, item.ArrivalDestinationKey),
                    DepartureDate = command.StartDate.AddDays(AdaptTransportationDepartureOffset(item)),
                    DepartureTimeLocal = item.DepartureTimeLocal,
                    DepartureTimeZone = TransportationTimeZone(
                        item.DepartureTimeZone, item.DepartureDestinationKey),
                    ArrivalDate = command.StartDate.AddDays(AdaptOffset(item.ArrivalDayOffset)),
                    ArrivalTimeLocal = item.ArrivalTimeLocal,
                    ArrivalTimeZone = TransportationTimeZone(
                        item.ArrivalTimeZone, item.ArrivalDestinationKey),
                    Status = PlanItemStatus.Proposed
                }).ToArray(),
                accommodations: accommodations.Select(item => new Accommodation
                {
                    Id = ids.NewAccommodationId(),
                    DestinationVisitId = item.DestinationKey is null ? null : visitIds[item.DestinationKey],
                    Name = item.Name,
                    Dates = new PlanningDateRange(
                        command.StartDate.AddDays(AdaptOffset(item.StartDayOffset)),
                        command.StartDate.AddDays(AdaptOffset(item.EndDayOffset))),
                    TimeZone = item.TimeZone,
                    Status = PlanItemStatus.Proposed
                }).ToArray());
        };
        return true;
    }

    private static bool ValidText(string value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value == value.Trim() && value.Length <= maximumLength;

    private static bool OptionalText(string? value, int maximumLength) =>
        value is null || ValidText(value, maximumLength);

    private static bool ValidLocale(string value)
    {
        if (!ValidText(value, 35))
        {
            return false;
        }

        try
        {
            return string.Equals(
                CultureInfo.GetCultureInfo(value).Name,
                value,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    private static PlanningRequestFingerprint CreateFingerprint(
        AdventureTemplateInstantiateCommand command,
        AuthorizedAdventureTemplateUse use)
    {
        var fingerprintVersion = command.TravelEstimate is not null
            ? 3
            : command.ConfiguredOrigin is null
                ? FingerprintVersion
                : OriginFingerprintVersion;
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(PlanningIdempotencyOperations.AdventurePlanTemplateInstantiateV1);
            writer.Write(fingerprintVersion);
            writer.Write(command.CreatorId.Value);
            writer.Write(command.Actor.UserId!.Value.Value);
            writer.Write(command.TemplateVersion.TemplateId);
            writer.Write(command.TemplateVersion.Version);
            writer.Write(command.StartDate.DayNumber);
            writer.Write(command.RequestedLocale);
            if (command.ConfiguredOrigin is not null)
            {
                writer.Write(command.ConfiguredOrigin.Name);
                writer.Write(command.ConfiguredOrigin.TimeZone.Value);
            }
            if (command.TravelEstimate is not null)
            {
                writer.Write(command.TravelEstimate.OneWayDistanceMiles);
                writer.Write(command.TravelEstimate.DailyDistanceMiles);
            }
            writer.Write(use.UseDecisionReference);
        }

        return new PlanningRequestFingerprint(
            fingerprintVersion,
            SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length))));
    }
}
