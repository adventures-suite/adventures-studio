using System.ComponentModel.DataAnnotations;

namespace AdventuresSuite.Companion.Contracts;

/// <summary>Provides common version and freshness metadata for a Companion projection.</summary>
public abstract record CompanionProjectionDto
{
    /// <summary>Gets the wire schema version.</summary>
    [Required, StringLength(64, MinimumLength = 1)] public required string SchemaVersion { get; init; }
    /// <summary>Gets the opaque authorized projection version.</summary>
    [Required, StringLength(64, MinimumLength = 1)] public required string ProjectionVersion { get; init; }
    /// <summary>Gets the authoritative generation instant.</summary>
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    /// <summary>Gets the instant after which this projection is stale.</summary>
    public required DateTimeOffset FreshUntilUtc { get; init; }
    /// <summary>Gets an optional opaque synchronization cursor.</summary>
    [StringLength(2048, MinimumLength = 1)] public string? SyncCursor { get; init; }
    /// <summary>Gets the safe server support identifier.</summary>
    [Required, StringLength(128, MinimumLength = 1)] public required string SupportId { get; init; }
}

/// <summary>Represents safe RFC 9457-compatible problem details.</summary>
public sealed record CompanionProblemDto
{
    /// <summary>Gets the safe problem type URI.</summary>
    public required Uri Type { get; init; }
    /// <summary>Gets the safe problem title.</summary>
    [Required, StringLength(300, MinimumLength = 1)] public required string Title { get; init; }
    /// <summary>Gets the HTTP status.</summary>
    [Range(400, 599)] public required int Status { get; init; }
    /// <summary>Gets the stable safe problem code.</summary>
    [Required, RegularExpression("^[a-z][a-z0-9_]{0,63}$")] public required string Code { get; init; }
    /// <summary>Gets the server-generated support identifier.</summary>
    [Required, StringLength(128, MinimumLength = 1)] public required string SupportId { get; init; }
    /// <summary>Gets whether a bounded retry may succeed.</summary>
    public required bool Retryable { get; init; }
    /// <summary>Gets an optional bounded retry delay.</summary>
    [Range(1, 86400)] public int? RetryAfterSeconds { get; init; }
}

/// <summary>Provides authorized Resource display metadata without protected bytes.</summary>
public sealed record CompanionResourceSummaryDto
{
    /// <summary>Gets the opaque Resource identity.</summary>
    [Required, StringLength(128, MinimumLength = 1), RegularExpression(CompanionContractLimits.OpaqueIdentityPattern)] public required string ResourceId { get; init; }
    /// <summary>Gets the declared media type.</summary>
    [Required, StringLength(127, MinimumLength = 3)] public required string MediaType { get; init; }
    /// <summary>Gets the bounded byte length when known.</summary>
    [Range(0, long.MaxValue)] public long? ByteLength { get; init; }
    /// <summary>Gets the safe title.</summary>
    [Required, StringLength(200, MinimumLength = 1)] public required string Title { get; init; }
    /// <summary>Gets accessible alternative text.</summary>
    [StringLength(500, MinimumLength = 1)] public string? AlternativeText { get; init; }
    /// <summary>Gets safe attribution.</summary>
    [StringLength(300)] public string? Attribution { get; init; }
    /// <summary>Gets the current availability.</summary>
    public required CompanionResourceAvailability Availability { get; init; }
    /// <summary>Gets whether an approved future package may retain the Resource offline.</summary>
    public required bool OfflineEligible { get; init; }
    /// <summary>Gets the approved retention boundary.</summary>
    public DateTimeOffset? RetainUntilUtc { get; init; }
    /// <summary>Gets the same-origin delivery path only when delivery is available.</summary>
    [StringLength(2048, MinimumLength = 1)] public string? ContentPath { get; init; }
}

/// <summary>Provides deterministic countdown inputs.</summary>
public sealed record CompanionCountdownDto
{
    /// <summary>Gets the destination-local target date.</summary>
    public required DateOnly TargetDate { get; init; }
    /// <summary>Gets an optional destination-local target time.</summary>
    public TimeOnly? TargetLocalTime { get; init; }
    /// <summary>Gets the IANA time-zone identifier.</summary>
    [Required, StringLength(100, MinimumLength = 1)] public required string TimeZone { get; init; }
    /// <summary>Gets the authoritative evaluation instant.</summary>
    public required DateTimeOffset EvaluatedAtUtc { get; init; }
    /// <summary>Gets the countdown state.</summary>
    public required CompanionCountdownState State { get; init; }
}

/// <summary>Summarizes one Adventure in the collection.</summary>
public sealed record CompanionAdventureSummaryDto
{
    /// <summary>Gets the opaque Adventure identity.</summary>
    [Required, StringLength(128, MinimumLength = 1), RegularExpression(CompanionContractLimits.OpaqueIdentityPattern)] public required string AdventureId { get; init; }
    /// <summary>Gets the traveler-safe title.</summary>
    [Required, StringLength(200, MinimumLength = 1)] public required string Title { get; init; }
    /// <summary>Gets optional safe context.</summary>
    [StringLength(300)] public string? Subtitle { get; init; }
    /// <summary>Gets the traveler-facing status.</summary>
    public required CompanionAdventureStatus Status { get; init; }
    /// <summary>Gets the Adventure-local start date.</summary>
    public required DateOnly StartDate { get; init; }
    /// <summary>Gets the Adventure-local end date.</summary>
    public required DateOnly EndDate { get; init; }
    /// <summary>Gets the primary IANA time zone.</summary>
    [Required, StringLength(100, MinimumLength = 1)] public required string PrimaryTimeZone { get; init; }
    /// <summary>Gets the countdown inputs.</summary>
    public required CompanionCountdownDto Countdown { get; init; }
    /// <summary>Gets optional authorized hero metadata.</summary>
    public CompanionResourceSummaryDto? HeroResource { get; init; }
    /// <summary>Gets the offline projection state.</summary>
    public required CompanionOfflineState OfflineState { get; init; }
}

/// <summary>Contains a deterministic page of Adventures.</summary>
public sealed record CompanionAdventureCollectionDto : CompanionProjectionDto
{
    /// <summary>Gets the deterministically ordered Adventures.</summary>
    public required IReadOnlyList<CompanionAdventureSummaryDto> Adventures { get; init; }
    /// <summary>Gets the opaque continuation token.</summary>
    [StringLength(2048, MinimumLength = 1)] public string? ContinuationToken { get; init; }
}

/// <summary>Summarizes an authorized destination visit.</summary>
public sealed record CompanionDestinationSummaryDto
{
    /// <summary>Gets the opaque destination visit identity.</summary>
    [Required, StringLength(128, MinimumLength = 1), RegularExpression(CompanionContractLimits.OpaqueIdentityPattern)] public required string DestinationVisitId { get; init; }
    /// <summary>Gets the traveler-safe destination name.</summary>
    [Required, StringLength(200, MinimumLength = 1)] public required string Name { get; init; }
    /// <summary>Gets the visit start date.</summary>
    public required DateOnly StartDate { get; init; }
    /// <summary>Gets the visit end date.</summary>
    public required DateOnly EndDate { get; init; }
    /// <summary>Gets the IANA time zone.</summary>
    [Required, StringLength(100, MinimumLength = 1)] public required string TimeZone { get; init; }
    /// <summary>Gets the stable sequence.</summary>
    [Range(1, 100)] public required int Sequence { get; init; }
    /// <summary>Gets optional authorized hero metadata.</summary>
    public CompanionResourceSummaryDto? HeroResource { get; init; }
}

/// <summary>Provides the traveler-safe Adventure overview.</summary>
public sealed record CompanionAdventureDto : CompanionProjectionDto
{
    /// <summary>Gets the opaque Adventure identity.</summary>
    [Required, StringLength(128, MinimumLength = 1), RegularExpression(CompanionContractLimits.OpaqueIdentityPattern)] public required string AdventureId { get; init; }
    /// <summary>Gets the title.</summary>
    [Required, StringLength(200, MinimumLength = 1)] public required string Title { get; init; }
    /// <summary>Gets optional context.</summary>
    [StringLength(300)] public string? Subtitle { get; init; }
    /// <summary>Gets the safe description.</summary>
    [Required, StringLength(2000, MinimumLength = 1)] public required string Description { get; init; }
    /// <summary>Gets the presentation status.</summary>
    public required CompanionAdventureStatus Status { get; init; }
    /// <summary>Gets the start date.</summary>
    public required DateOnly StartDate { get; init; }
    /// <summary>Gets the end date.</summary>
    public required DateOnly EndDate { get; init; }
    /// <summary>Gets the primary IANA time zone.</summary>
    [Required, StringLength(100, MinimumLength = 1)] public required string PrimaryTimeZone { get; init; }
    /// <summary>Gets countdown inputs.</summary>
    public required CompanionCountdownDto Countdown { get; init; }
    /// <summary>Gets ordered destination visits.</summary>
    [MaxLength(100)] public required IReadOnlyList<CompanionDestinationSummaryDto> Destinations { get; init; }
    /// <summary>Gets an optional next-item summary.</summary>
    [StringLength(300)] public string? NextItemSummary { get; init; }
    /// <summary>Gets a safe readiness summary.</summary>
    [Required, StringLength(300, MinimumLength = 1)] public required string ReadinessSummary { get; init; }
    /// <summary>Gets same-origin capability links.</summary>
    public required IReadOnlyDictionary<string, string> CapabilityLinks { get; init; }
    /// <summary>Gets the opaque information-profile version.</summary>
    [Required, StringLength(64, MinimumLength = 1)] public required string InformationProfileVersion { get; init; }
}
