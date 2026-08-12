using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning.Imports;

/// <summary>Classifies one day in a supplier-published cruise itinerary.</summary>
public enum PublishedCruiseDayKind
{
    /// <summary>The source did not provide enough information to classify the day.</summary>
    Unknown,
    /// <summary>The sailing begins and guests embark.</summary>
    Embarkation,
    /// <summary>The ship is scheduled to call at a port.</summary>
    PortCall,
    /// <summary>The published itinerary identifies a day at sea.</summary>
    SeaDay,
    /// <summary>The sailing ends and guests disembark.</summary>
    Disembarkation
}

/// <summary>Identifies a sailing within one provider without exposing provider schema.</summary>
public readonly record struct PublishedCruiseSailingReference
{
    /// <summary>Initializes an opaque provider sailing reference.</summary>
    public PublishedCruiseSailingReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200)
        {
            throw new ArgumentException("A provider sailing reference is required.", nameof(value));
        }

        Value = value;
    }

    /// <summary>Gets the opaque reference value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Defines a bounded search of a published cruise catalog.</summary>
public sealed record PublishedCruiseSailingSearch
{
    /// <summary>Initializes a cruise sailing search.</summary>
    public PublishedCruiseSailingSearch(
        string cruiseLine,
        string shipName,
        DateOnly earliestDepartureDate,
        DateOnly latestDepartureDate)
    {
        CruiseLine = RequireText(cruiseLine, nameof(cruiseLine), 200);
        ShipName = RequireText(shipName, nameof(shipName), 200);
        if (latestDepartureDate < earliestDepartureDate
            || latestDepartureDate.DayNumber - earliestDepartureDate.DayNumber > 366)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latestDepartureDate),
                "The departure-date window must be ordered and no longer than 366 days.");
        }

        EarliestDepartureDate = earliestDepartureDate;
        LatestDepartureDate = latestDepartureDate;
    }

    /// <summary>Gets the source cruise-line label.</summary>
    public string CruiseLine { get; }
    /// <summary>Gets the source ship label.</summary>
    public string ShipName { get; }
    /// <summary>Gets the inclusive earliest departure date.</summary>
    public DateOnly EarliestDepartureDate { get; }
    /// <summary>Gets the inclusive latest departure date.</summary>
    public DateOnly LatestDepartureDate { get; }

    private static string RequireText(string? value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > maximumLength)
        {
            throw new ArgumentException("A trimmed source label is required.", name);
        }

        return value;
    }
}

/// <summary>Provides the least data needed to select one published sailing.</summary>
public sealed record PublishedCruiseSailingSummary(
    PublishedCruiseSailingReference SailingReference,
    string CruiseLine,
    string ShipName,
    string? VoyageLabel,
    DateOnly DepartureDate,
    DateOnly ReturnDate,
    DateTimeOffset RetrievedAtUtc);

/// <summary>Represents one ordered day in a normalized published sailing snapshot.</summary>
public sealed record PublishedCruiseItineraryDay(
    int Sequence,
    DateOnly LocalDate,
    PublishedCruiseDayKind Kind,
    string RawPlaceLabel,
    TimeOnly? ArrivalLocalTime,
    TimeOnly? DepartureLocalTime,
    string? ProposedIanaTimeZone,
    decimal? Latitude,
    decimal? Longitude,
    string SourceReference,
    decimal Confidence);

/// <summary>Represents an untrusted, normalized snapshot returned by a cruise catalog.</summary>
public sealed record PublishedCruiseSailing(
    PublishedCruiseSailingReference SailingReference,
    string CruiseLine,
    string ShipName,
    string? VoyageLabel,
    string? ItineraryLabel,
    DateOnly DepartureDate,
    DateOnly ReturnDate,
    DateTimeOffset RetrievedAtUtc,
    DateTimeOffset? SourceUpdatedAtUtc,
    string AttributionReference,
    IReadOnlyList<PublishedCruiseItineraryDay> Days);

/// <summary>Reports whether the provider can establish source freshness.</summary>
public sealed record PublishedCruiseItineraryFreshness(
    DateTimeOffset CheckedAtUtc,
    DateTimeOffset? SourceUpdatedAtUtc,
    bool IsAvailable);

/// <summary>Retrieves published cruise data without granting it Planning authority.</summary>
public interface IPublishedCruiseItineraryProvider
{
    /// <summary>Finds candidate sailings within one explicit Creator operation scope.</summary>
    Task<IReadOnlyList<PublishedCruiseSailingSummary>> SearchSailingsAsync(
        CreatorId creatorId,
        PublishedCruiseSailingSearch search,
        CancellationToken cancellationToken = default);

    /// <summary>Gets one normalized, untrusted sailing snapshot.</summary>
    Task<PublishedCruiseSailing?> GetSailingAsync(
        CreatorId creatorId,
        PublishedCruiseSailingReference sailingReference,
        CancellationToken cancellationToken = default);

    /// <summary>Checks source freshness without treating retrieval time as source authority.</summary>
    Task<PublishedCruiseItineraryFreshness> GetFreshnessAsync(
        CreatorId creatorId,
        PublishedCruiseSailingReference sailingReference,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides the safe dormant behavior until an approved commercial provider is configured.
/// </summary>
public sealed class UnavailablePublishedCruiseItineraryProvider(TimeProvider timeProvider)
    : IPublishedCruiseItineraryProvider
{
    /// <inheritdoc />
    public Task<IReadOnlyList<PublishedCruiseSailingSummary>> SearchSailingsAsync(
        CreatorId creatorId,
        PublishedCruiseSailingSearch search,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCreator(creatorId);
        ArgumentNullException.ThrowIfNull(search);
        return Task.FromResult<IReadOnlyList<PublishedCruiseSailingSummary>>([]);
    }

    /// <inheritdoc />
    public Task<PublishedCruiseSailing?> GetSailingAsync(
        CreatorId creatorId,
        PublishedCruiseSailingReference sailingReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCreator(creatorId);
        ValidateReference(sailingReference);
        return Task.FromResult<PublishedCruiseSailing?>(null);
    }

    /// <inheritdoc />
    public Task<PublishedCruiseItineraryFreshness> GetFreshnessAsync(
        CreatorId creatorId,
        PublishedCruiseSailingReference sailingReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCreator(creatorId);
        ValidateReference(sailingReference);
        return Task.FromResult(new PublishedCruiseItineraryFreshness(
            timeProvider.GetUtcNow(),
            SourceUpdatedAtUtc: null,
            IsAvailable: false));
    }

    private static void ValidateCreator(CreatorId creatorId)
    {
        if (creatorId == default)
        {
            throw new ArgumentException("Creator identity is required.", nameof(creatorId));
        }
    }

    private static void ValidateReference(PublishedCruiseSailingReference sailingReference)
    {
        if (sailingReference == default)
        {
            throw new ArgumentException("A sailing reference is required.", nameof(sailingReference));
        }
    }
}
