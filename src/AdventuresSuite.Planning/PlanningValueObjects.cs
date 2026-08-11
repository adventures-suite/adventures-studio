namespace TheSimontonAdventures.Web.Planning;

/// <summary>Identifies the broad Adventure lifecycle stage supported by a plan.</summary>
public enum AdventureLifecycleStage
{
    /// <summary>The Creator is exploring an idea.</summary>
    Dream,
    /// <summary>The Creator is building an actionable plan.</summary>
    Plan,
    /// <summary>The Adventure is actively under way.</summary>
    Travel,
    /// <summary>The Creator is preserving the completed experience.</summary>
    Preserve,
    /// <summary>Selected material is being prepared for publication.</summary>
    Publish,
    /// <summary>Published material is being shared.</summary>
    Share,
    /// <summary>The Adventure is retained as a lasting record.</summary>
    Remember
}

/// <summary>Describes the private planning maturity of an Adventure Plan.</summary>
public enum PlanningStatus
{
    /// <summary>The Adventure is only an idea.</summary>
    Idea,
    /// <summary>The plan is being drafted.</summary>
    Draft,
    /// <summary>The principal plan is established.</summary>
    Planned,
    /// <summary>The Adventure is approaching.</summary>
    Upcoming,
    /// <summary>The Adventure is in progress.</summary>
    InProgress,
    /// <summary>The Adventure has concluded.</summary>
    Completed,
    /// <summary>The plan is retained but no longer active.</summary>
    Archived
}

/// <summary>Describes the operational state of a planned item.</summary>
public enum PlanItemStatus
{
    /// <summary>The item is being considered.</summary>
    Proposed,
    /// <summary>Space or service is held but not finally confirmed.</summary>
    Reserved,
    /// <summary>The item is confirmed.</summary>
    Confirmed,
    /// <summary>The previously recorded details have changed.</summary>
    Changed,
    /// <summary>The item was cancelled.</summary>
    Cancelled,
    /// <summary>The item was completed.</summary>
    Completed
}

/// <summary>Represents a validated IANA time-zone identifier.</summary>
public readonly record struct IanaTimeZone
{
    /// <summary>Initializes an IANA time-zone identity available to the runtime.</summary>
    /// <exception cref="ArgumentException">The value is empty or is not an IANA identifier.</exception>
    public IanaTimeZone(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value != value.Trim()
            || (!value.Contains('/', StringComparison.Ordinal) && value != "UTC"))
        {
            throw new ArgumentException("An IANA time-zone identifier is required.", nameof(value));
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(value);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new ArgumentException("The IANA time-zone identifier is unknown.", nameof(value), exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new ArgumentException("The IANA time-zone identifier is invalid.", nameof(value), exception);
        }

        Value = value;
    }

    /// <summary>Gets the canonical IANA identifier.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Defines an inclusive local calendar-date range.</summary>
public readonly record struct PlanningDateRange
{
    /// <summary>Initializes a date range without converting either date to an instant.</summary>
    public PlanningDateRange(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new ArgumentException("The end date cannot precede the start date.", nameof(end));
        }

        Start = start;
        End = end;
    }

    /// <summary>Gets the inclusive first local calendar date.</summary>
    public DateOnly Start { get; }
    /// <summary>Gets the inclusive last local calendar date.</summary>
    public DateOnly End { get; }

    /// <summary>Determines whether the range includes a local calendar date.</summary>
    public bool Contains(DateOnly date) => date >= Start && date <= End;

    /// <summary>Determines whether this range contains another range.</summary>
    public bool Contains(PlanningDateRange range) => range.Start >= Start && range.End <= End;
}

/// <summary>Captures optimistic-concurrency and UTC system-audit metadata.</summary>
public readonly record struct PlanAudit
{
    /// <summary>Initializes audit metadata for one aggregate version.</summary>
    public PlanAudit(long version, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be positive.");
        }

        if (createdAtUtc.Offset != TimeSpan.Zero || updatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Planning audit timestamps must use UTC.");
        }

        if (updatedAtUtc < createdAtUtc)
        {
            throw new ArgumentException("The update timestamp cannot precede creation.", nameof(updatedAtUtc));
        }

        Version = version;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    /// <summary>Gets the positive optimistic-concurrency version.</summary>
    public long Version { get; }
    /// <summary>Gets the UTC time at which the plan was created.</summary>
    public DateTimeOffset CreatedAtUtc { get; }
    /// <summary>Gets the UTC time of the latest authoritative change.</summary>
    public DateTimeOffset UpdatedAtUtc { get; }
}
