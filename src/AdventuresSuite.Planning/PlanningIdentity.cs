namespace TheSimontonAdventures.Web.Planning;

internal static class PlanningIdentity
{
    public static string Require(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length is < 3 or > 64
            || value[0] is < 'a' or > 'z'
            || value.Any(character => character is not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9') and not '_'))
        {
            throw new ArgumentException(
                "Planning identities must contain 3-64 lowercase letters, digits, or underscores and begin with a letter.",
                parameterName);
        }

        return value;
    }
}

/// <summary>Identifies one private Adventure Plan independently of its title.</summary>
public readonly record struct AdventurePlanId
{
    /// <summary>Initializes a stable Adventure Plan identity.</summary>
    public AdventurePlanId(string value) => Value = PlanningIdentity.Require(value, nameof(value));

    /// <summary>Gets the canonical identity value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifies one traveler within an Adventure Plan.</summary>
public readonly record struct TravelerId
{
    /// <summary>Initializes a stable traveler identity.</summary>
    public TravelerId(string value) => Value = PlanningIdentity.Require(value, nameof(value));
    /// <summary>Gets the canonical identity value.</summary>
    public string Value { get; }
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifies one planned destination visit.</summary>
public readonly record struct DestinationVisitId
{
    /// <summary>Initializes a stable destination-visit identity.</summary>
    public DestinationVisitId(string value) => Value = PlanningIdentity.Require(value, nameof(value));
    /// <summary>Gets the canonical identity value.</summary>
    public string Value { get; }
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifies one local itinerary day.</summary>
public readonly record struct ItineraryDayId
{
    /// <summary>Initializes a stable itinerary-day identity.</summary>
    public ItineraryDayId(string value) => Value = PlanningIdentity.Require(value, nameof(value));
    /// <summary>Gets the canonical identity value.</summary>
    public string Value { get; }
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifies one planned activity.</summary>
public readonly record struct PlannedActivityId
{
    /// <summary>Initializes a stable planned-activity identity.</summary>
    public PlannedActivityId(string value) => Value = PlanningIdentity.Require(value, nameof(value));
    /// <summary>Gets the canonical identity value.</summary>
    public string Value { get; }
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifies one transportation segment.</summary>
public readonly record struct TransportationSegmentId
{
    /// <summary>Initializes a stable transportation-segment identity.</summary>
    public TransportationSegmentId(string value) => Value = PlanningIdentity.Require(value, nameof(value));
    /// <summary>Gets the canonical identity value.</summary>
    public string Value { get; }
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifies one planned accommodation.</summary>
public readonly record struct AccommodationId
{
    /// <summary>Initializes a stable accommodation identity.</summary>
    public AccommodationId(string value) => Value = PlanningIdentity.Require(value, nameof(value));
    /// <summary>Gets the canonical identity value.</summary>
    public string Value { get; }
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifies one private reservation summary.</summary>
public readonly record struct ReservationId
{
    /// <summary>Initializes a stable reservation identity.</summary>
    public ReservationId(string value) => Value = PlanningIdentity.Require(value, nameof(value));
    /// <summary>Gets the canonical identity value.</summary>
    public string Value { get; }
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifies one private planning note.</summary>
public readonly record struct PlanningNoteId
{
    /// <summary>Initializes a stable planning-note identity.</summary>
    public PlanningNoteId(string value) => Value = PlanningIdentity.Require(value, nameof(value));
    /// <summary>Gets the canonical identity value.</summary>
    public string Value { get; }
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifies one planning task.</summary>
public readonly record struct PlanningTaskId
{
    /// <summary>Initializes a stable planning-task identity.</summary>
    public PlanningTaskId(string value) => Value = PlanningIdentity.Require(value, nameof(value));
    /// <summary>Gets the canonical identity value.</summary>
    public string Value { get; }
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifies one planned budget item.</summary>
public readonly record struct BudgetItemId
{
    /// <summary>Initializes a stable budget-item identity.</summary>
    public BudgetItemId(string value) => Value = PlanningIdentity.Require(value, nameof(value));
    /// <summary>Gets the canonical identity value.</summary>
    public string Value { get; }
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifies one packing item.</summary>
public readonly record struct PackingItemId
{
    /// <summary>Initializes a stable packing-item identity.</summary>
    public PackingItemId(string value) => Value = PlanningIdentity.Require(value, nameof(value));
    /// <summary>Gets the canonical identity value.</summary>
    public string Value { get; }
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
