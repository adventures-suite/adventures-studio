using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>Identifies the authorized Planning subject used to discover FootSteps.</summary>
public enum PlannerFootStepContextKind
{
    /// <summary>The whole Adventure Plan.</summary>
    Adventure,
    /// <summary>One destination visit owned by the plan.</summary>
    Destination,
    /// <summary>One itinerary day owned by the plan.</summary>
    Day
}

/// <summary>Provides stable, locale-independent filters for FootStep discovery.</summary>
public sealed record PlannerFootStepFilters
{
    /// <summary>Gets country or region identifiers.</summary>
    public IReadOnlySet<string> Places { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets FootStep kind identifiers.</summary>
    public IReadOnlySet<string> Kinds { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets transportation-mode identifiers.</summary>
    public IReadOnlySet<string> TransportationModes { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets travel-category identifiers.</summary>
    public IReadOnlySet<string> Categories { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets route-style identifiers.</summary>
    public IReadOnlySet<string> RouteStyles { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets terrain or surface identifiers.</summary>
    public IReadOnlySet<string> Surfaces { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets accessibility identifiers.</summary>
    public IReadOnlySet<string> Accessibility { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets pace identifiers.</summary>
    public IReadOnlySet<string> Paces { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets season or preferred-month identifiers.</summary>
    public IReadOnlySet<string> Seasons { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets equipment-need identifiers.</summary>
    public IReadOnlySet<string> EquipmentNeeds { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets budget-band identifiers.</summary>
    public IReadOnlySet<string> BudgetBands { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets traveler-composition identifiers.</summary>
    public IReadOnlySet<string> TravelerCompositions { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets source-class identifiers.</summary>
    public IReadOnlySet<string> SourceClasses { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets source-language identifiers.</summary>
    public IReadOnlySet<string> Languages { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets the optional minimum duration in days.</summary>
    public int? MinimumDays { get; init; }
    /// <summary>Gets the optional maximum duration in days.</summary>
    public int? MaximumDays { get; init; }
}

/// <summary>Represents one immutable, allowlisted FootStep catalog card.</summary>
public sealed record PlannerFootStepDefinition
{
    /// <summary>Gets the stable source identity.</summary>
    public required string Id { get; init; }
    /// <summary>Gets the exact immutable source version.</summary>
    public required string Version { get; init; }
    /// <summary>Gets the stable kind identifier.</summary>
    public required string Kind { get; init; }
    /// <summary>Gets the localized title.</summary>
    public required string Title { get; init; }
    /// <summary>Gets the localized concise summary.</summary>
    public required string Summary { get; init; }
    /// <summary>Gets the authorized attribution label.</summary>
    public required string Attribution { get; init; }
    /// <summary>Gets the localized freshness label.</summary>
    public required string Freshness { get; init; }
    /// <summary>Gets applicable Planning context kinds.</summary>
    public IReadOnlySet<PlannerFootStepContextKind> ContextKinds { get; init; } = new HashSet<PlannerFootStepContextKind>();
    /// <summary>Gets stable country or region identifiers.</summary>
    public IReadOnlySet<string> Places { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets stable transportation-mode identifiers.</summary>
    public IReadOnlySet<string> TransportationModes { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets stable category identifiers.</summary>
    public IReadOnlySet<string> Categories { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets stable route-style identifiers.</summary>
    public IReadOnlySet<string> RouteStyles { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets stable terrain or surface identifiers.</summary>
    public IReadOnlySet<string> Surfaces { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets stable accessibility identifiers.</summary>
    public IReadOnlySet<string> Accessibility { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets stable pace identifiers.</summary>
    public IReadOnlySet<string> Paces { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets stable season identifiers.</summary>
    public IReadOnlySet<string> Seasons { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets stable equipment-need identifiers.</summary>
    public IReadOnlySet<string> EquipmentNeeds { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets stable budget-band identifiers.</summary>
    public IReadOnlySet<string> BudgetBands { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets stable traveler-composition identifiers.</summary>
    public IReadOnlySet<string> TravelerCompositions { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets stable source-class identifiers.</summary>
    public IReadOnlySet<string> SourceClasses { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets BCP 47 source-language identifiers.</summary>
    public IReadOnlySet<string> Languages { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Gets the suggested duration in days when applicable.</summary>
    public int? DurationDays { get; init; }
}

/// <summary>Describes an explicit authorized FootStep query.</summary>
/// <param name="Actor">The authenticated human actor.</param>
/// <param name="CreatorId">The customer Creator ownership boundary.</param>
/// <param name="AdventurePlanId">The exact private plan instance.</param>
/// <param name="ContextKind">The selected Planning subject kind.</param>
/// <param name="ContextId">The selected plan-owned subject identity.</param>
/// <param name="RequestedLocale">The requested BCP 47 presentation locale.</param>
/// <param name="Filters">The locale-independent combined facets.</param>
/// <param name="Page">The one-based requested page.</param>
/// <param name="PageSize">The bounded requested page size.</param>
public sealed record PlannerFootStepQuery(
    ActorIdentity Actor,
    CreatorId CreatorId,
    AdventurePlanId AdventurePlanId,
    PlannerFootStepContextKind ContextKind,
    string ContextId,
    string RequestedLocale,
    PlannerFootStepFilters Filters,
    int Page,
    int PageSize);

/// <summary>Returns a disclosure-safe page of authorized FootSteps.</summary>
/// <param name="IsAllowed">Whether the exact query context was authorized.</param>
/// <param name="Items">The allowlisted immutable card projections.</param>
/// <param name="TotalItems">The authorized filtered result count.</param>
/// <param name="Page">The effective one-based page.</param>
/// <param name="PageSize">The effective page size.</param>
public sealed record PlannerFootStepQueryResult(
    bool IsAllowed,
    IReadOnlyList<PlannerFootStepDefinition> Items,
    int TotalItems,
    int Page,
    int PageSize)
{
    /// <summary>Creates a generic denied result without catalog disclosure.</summary>
    public static PlannerFootStepQueryResult Denied(int pageSize) => new(false, [], 0, 1, pageSize);
}

/// <summary>Loads already visibility-, license-, and entitlement-approved FootSteps.</summary>
public interface IPlannerFootStepCatalogSource
{
    /// <summary>Lists source-authorized immutable FootSteps for one customer Creator and locale.</summary>
    Task<IReadOnlyList<PlannerFootStepDefinition>> ListAsync(
        CreatorId customerCreatorId,
        string requestedLocale,
        CancellationToken cancellationToken = default);
}

/// <summary>Queries FootSteps after Creator membership and exact plan authorization.</summary>
public interface IPlannerFootStepQueryService
{
    /// <summary>Returns a deterministic filtered page for an authorized Planning context.</summary>
    Task<PlannerFootStepQueryResult> QueryAsync(
        PlannerFootStepQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>Implements leakage-safe, deterministic FootStep discovery.</summary>
public sealed class PlannerFootStepQueryService(
    ICreatorMembershipProvider membershipProvider,
    IAuthorizationPolicyEvaluator authorizationPolicyEvaluator,
    IPlanningTransactionFactory transactionFactory,
    IPlannerFootStepCatalogSource source) : IPlannerFootStepQueryService
{
    /// <inheritdoc />
    public async Task<PlannerFootStepQueryResult> QueryAsync(
        PlannerFootStepQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query is null || query.Actor is null || !query.Actor.IsHuman || !query.Actor.UserId.HasValue
            || query.CreatorId == default || query.AdventurePlanId == default
            || string.IsNullOrWhiteSpace(query.ContextId) || string.IsNullOrWhiteSpace(query.RequestedLocale)
            || query.Page < 1 || query.PageSize is < 1 or > 24)
        {
            return PlannerFootStepQueryResult.Denied(query?.PageSize is > 0 and <= 24 ? query.PageSize : 6);
        }

        var membership = await membershipProvider.GetMembershipAsync(query.Actor.UserId.Value, query.CreatorId, cancellationToken);
        if (membership is null)
        {
            return PlannerFootStepQueryResult.Denied(query.PageSize);
        }

        var decision = await authorizationPolicyEvaluator.AuthorizeAsync(
            new AuthorizationRequest(query.Actor, Permissions.AdventurePlanView,
                AuthorizationResourceScope.ForInstance(query.CreatorId, AuthorizationResourceTypes.AdventurePlan, query.AdventurePlanId.Value),
                membershipVersion: membership.Version), cancellationToken);
        if (!decision.IsAllowed)
        {
            return PlannerFootStepQueryResult.Denied(query.PageSize);
        }

        await using var transaction = await transactionFactory.BeginAsync(query.CreatorId, cancellationToken);
        var plan = await transaction.AdventurePlans.GetAsync(query.CreatorId, query.AdventurePlanId, cancellationToken);
        if (plan is null || plan.CreatorId != query.CreatorId || plan.Id != query.AdventurePlanId
            || !ContextBelongsToPlan(plan, query.ContextKind, query.ContextId))
        {
            return PlannerFootStepQueryResult.Denied(query.PageSize);
        }

        var candidates = await source.ListAsync(query.CreatorId, query.RequestedLocale, cancellationToken);
        var filtered = candidates
            .Where(item => item.ContextKinds.Contains(query.ContextKind))
            .Where(item => Matches(query.Filters.Places, item.Places))
            .Where(item => query.Filters.Kinds.Count == 0 || query.Filters.Kinds.Contains(item.Kind))
            .Where(item => Matches(query.Filters.TransportationModes, item.TransportationModes))
            .Where(item => Matches(query.Filters.Categories, item.Categories))
            .Where(item => Matches(query.Filters.RouteStyles, item.RouteStyles))
            .Where(item => Matches(query.Filters.Surfaces, item.Surfaces))
            .Where(item => Matches(query.Filters.Accessibility, item.Accessibility))
            .Where(item => Matches(query.Filters.Paces, item.Paces))
            .Where(item => Matches(query.Filters.Seasons, item.Seasons))
            .Where(item => Matches(query.Filters.EquipmentNeeds, item.EquipmentNeeds))
            .Where(item => Matches(query.Filters.BudgetBands, item.BudgetBands))
            .Where(item => Matches(query.Filters.TravelerCompositions, item.TravelerCompositions))
            .Where(item => Matches(query.Filters.SourceClasses, item.SourceClasses))
            .Where(item => Matches(query.Filters.Languages, item.Languages))
            .Where(item => !query.Filters.MinimumDays.HasValue || item.DurationDays >= query.Filters.MinimumDays)
            .Where(item => !query.Filters.MaximumDays.HasValue || item.DurationDays <= query.Filters.MaximumDays)
            .OrderBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.Title, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var total = filtered.Length;
        var page = Math.Min(query.Page, Math.Max(1, (int)Math.Ceiling(total / (double)query.PageSize)));
        return new(true, filtered.Skip((page - 1) * query.PageSize).Take(query.PageSize).ToArray(), total, page, query.PageSize);
    }

    private static bool Matches(IReadOnlySet<string> selected, IReadOnlySet<string> values) =>
        selected.Count == 0 || selected.Any(values.Contains);

    private static bool ContextBelongsToPlan(AdventurePlan plan, PlannerFootStepContextKind kind, string id) => kind switch
    {
        PlannerFootStepContextKind.Adventure => string.Equals(plan.Id.Value, id, StringComparison.Ordinal),
        PlannerFootStepContextKind.Destination => plan.DestinationVisits.Any(item => string.Equals(item.Id.Value, id, StringComparison.Ordinal)),
        PlannerFootStepContextKind.Day => plan.ItineraryDays.Any(item => string.Equals(item.Id.Value, id, StringComparison.Ordinal)),
        _ => false
    };
}

/// <summary>Fails closed when no reviewed FootStep catalog source is configured.</summary>
public sealed class UnavailablePlannerFootStepCatalogSource : IPlannerFootStepCatalogSource
{
    /// <inheritdoc />
    public Task<IReadOnlyList<PlannerFootStepDefinition>> ListAsync(
        CreatorId customerCreatorId,
        string requestedLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PlannerFootStepDefinition>>([]);
}
