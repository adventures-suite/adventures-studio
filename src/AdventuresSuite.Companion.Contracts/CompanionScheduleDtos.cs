using System.ComponentModel.DataAnnotations;

namespace AdventuresSuite.Companion.Contracts;

/// <summary>Represents one traveler-safe schedule item.</summary>
public sealed record CompanionScheduleItemDto
{
    /// <summary>Gets the opaque item identity.</summary>
    [Required, StringLength(128, MinimumLength = 1), RegularExpression(CompanionContractLimits.OpaqueIdentityPattern)] public required string ItemId { get; init; }
    /// <summary>Gets the bounded item type.</summary>
    [Required, StringLength(64, MinimumLength = 1)] public required string ItemType { get; init; }
    /// <summary>Gets the safe title.</summary>
    [Required, StringLength(200, MinimumLength = 1)] public required string Title { get; init; }
    /// <summary>Gets an optional safe summary.</summary>
    [StringLength(2000)] public string? Summary { get; init; }
    /// <summary>Gets the local calendar date.</summary>
    public required DateOnly LocalDate { get; init; }
    /// <summary>Gets the optional local start time.</summary>
    public TimeOnly? StartLocalTime { get; init; }
    /// <summary>Gets the optional local end time.</summary>
    public TimeOnly? EndLocalTime { get; init; }
    /// <summary>Gets the IANA time zone.</summary>
    [Required, StringLength(100, MinimumLength = 1)] public required string TimeZone { get; init; }
    /// <summary>Gets the timing status.</summary>
    public required CompanionTimeStatus TimeStatus { get; init; }
    /// <summary>Gets the operational status.</summary>
    public required CompanionOperationalStatus OperationalStatus { get; init; }
    /// <summary>Gets an optional safe place summary.</summary>
    [StringLength(300)] public string? PlaceSummary { get; init; }
    /// <summary>Gets an optional safe transportation summary.</summary>
    [StringLength(300)] public string? TransportationSummary { get; init; }
    /// <summary>Gets authorized Resource metadata.</summary>
    public required IReadOnlyList<CompanionResourceSummaryDto> Resources { get; init; }
    /// <summary>Gets whether the traveler must acknowledge a material change.</summary>
    public required bool RequiresAcknowledgment { get; init; }
    /// <summary>Gets an optional safe action label.</summary>
    [StringLength(100, MinimumLength = 1)] public string? ActionLabel { get; init; }
    /// <summary>Gets an optional same-origin action path.</summary>
    [StringLength(2048, MinimumLength = 1)] public string? ActionPath { get; init; }
}

/// <summary>Provides Today and Next for one Adventure.</summary>
public sealed record CompanionTodayDto : CompanionProjectionDto
{
    /// <summary>Gets the opaque Adventure identity.</summary>
    [Required, StringLength(128, MinimumLength = 1), RegularExpression(CompanionContractLimits.OpaqueIdentityPattern)] public required string AdventureId { get; init; }
    /// <summary>Gets the local date used for evaluation.</summary>
    public required DateOnly LocalDate { get; init; }
    /// <summary>Gets the IANA time zone used for evaluation.</summary>
    [Required, StringLength(100, MinimumLength = 1)] public required string TimeZone { get; init; }
    /// <summary>Gets the Today state.</summary>
    public required CompanionTodayState State { get; init; }
    /// <summary>Gets deterministically ordered items for the local day.</summary>
    public required IReadOnlyList<CompanionScheduleItemDto> TodayItems { get; init; }
    /// <summary>Gets the next item when one is visible.</summary>
    public CompanionScheduleItemDto? NextItem { get; init; }
    /// <summary>Gets an optional safe notice.</summary>
    [StringLength(300)] public string? Notice { get; init; }
}

/// <summary>Represents one itinerary day.</summary>
public sealed record CompanionItineraryDayDto
{
    /// <summary>Gets the opaque itinerary-day identity.</summary>
    [Required, StringLength(128, MinimumLength = 1), RegularExpression(CompanionContractLimits.OpaqueIdentityPattern)] public required string ItineraryDayId { get; init; }
    /// <summary>Gets the local date.</summary>
    public required DateOnly LocalDate { get; init; }
    /// <summary>Gets the IANA time zone.</summary>
    [Required, StringLength(100, MinimumLength = 1)] public required string TimeZone { get; init; }
    /// <summary>Gets the one-based day number.</summary>
    [Range(1, 180)] public required int DayNumber { get; init; }
    /// <summary>Gets an optional traveler-safe title.</summary>
    [StringLength(200)] public string? Title { get; init; }
    /// <summary>Gets the destination visit identity.</summary>
    [Required, StringLength(128, MinimumLength = 1), RegularExpression(CompanionContractLimits.OpaqueIdentityPattern)] public required string DestinationVisitId { get; init; }
    /// <summary>Gets the safe destination name.</summary>
    [Required, StringLength(200, MinimumLength = 1)] public required string DestinationName { get; init; }
    /// <summary>Gets the ordered schedule items.</summary>
    [MaxLength(250)] public required IReadOnlyList<CompanionScheduleItemDto> Items { get; init; }
    /// <summary>Gets an optional safe day summary.</summary>
    [StringLength(2000)] public string? Summary { get; init; }
    /// <summary>Gets whether a material change exists.</summary>
    public required bool HasMaterialChange { get; init; }
    /// <summary>Gets an optional opaque acknowledgment identity.</summary>
    [StringLength(128, MinimumLength = 1), RegularExpression(CompanionContractLimits.OpaqueIdentityPattern)] public string? AcknowledgmentId { get; init; }
}

/// <summary>Provides the authorized deterministic itinerary.</summary>
public sealed record CompanionItineraryDto : CompanionProjectionDto
{
    /// <summary>Gets the opaque Adventure identity.</summary>
    [Required, StringLength(128, MinimumLength = 1), RegularExpression(CompanionContractLimits.OpaqueIdentityPattern)] public required string AdventureId { get; init; }
    /// <summary>Gets the ordered itinerary days.</summary>
    [MaxLength(180)] public required IReadOnlyList<CompanionItineraryDayDto> Days { get; init; }
}
