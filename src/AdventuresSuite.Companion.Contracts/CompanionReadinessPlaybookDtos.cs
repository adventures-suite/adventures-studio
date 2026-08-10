using System.ComponentModel.DataAnnotations;

namespace AdventuresSuite.Companion.Contracts;

/// <summary>Summarizes one readiness category.</summary>
public sealed record CompanionReadinessCategoryDto
{
    /// <summary>Gets the closed category.</summary>
    public required CompanionReadinessCategory Category { get; init; }
    /// <summary>Gets the category state.</summary>
    public required CompanionReadinessState State { get; init; }
    /// <summary>Gets the safe category title.</summary>
    [Required, StringLength(200, MinimumLength = 1)] public required string Title { get; init; }
    /// <summary>Gets the total visible item count.</summary>
    [Range(0, 10000)] public required int TotalCount { get; init; }
    /// <summary>Gets the completed visible item count.</summary>
    [Range(0, 10000)] public required int CompletedCount { get; init; }
}

/// <summary>Represents one traveler-visible readiness action.</summary>
public sealed record CompanionReadinessActionDto
{
    /// <summary>Gets the opaque action identity.</summary>
    [Required, StringLength(128, MinimumLength = 1), RegularExpression(CompanionContractLimits.OpaqueIdentityPattern)] public required string ActionId { get; init; }
    /// <summary>Gets the action category.</summary>
    public required CompanionReadinessCategory Category { get; init; }
    /// <summary>Gets the safe title.</summary>
    [Required, StringLength(200, MinimumLength = 1)] public required string Title { get; init; }
    /// <summary>Gets the optional due date.</summary>
    public DateOnly? DueDate { get; init; }
    /// <summary>Gets an optional due local time.</summary>
    public TimeOnly? DueLocalTime { get; init; }
    /// <summary>Gets an optional IANA time zone.</summary>
    [StringLength(100, MinimumLength = 1)] public string? TimeZone { get; init; }
    /// <summary>Gets the bounded urgency.</summary>
    [Range(0, 3)] public required int Urgency { get; init; }
    /// <summary>Gets whether the action is complete.</summary>
    public required bool IsComplete { get; init; }
    /// <summary>Gets an optional same-origin action route.</summary>
    [StringLength(2048, MinimumLength = 1)] public string? ActionPath { get; init; }
}

/// <summary>Provides the traveler-visible readiness projection.</summary>
public sealed record CompanionReadinessDto : CompanionProjectionDto
{
    /// <summary>Gets the opaque Adventure identity.</summary>
    [Required, StringLength(128, MinimumLength = 1), RegularExpression(CompanionContractLimits.OpaqueIdentityPattern)] public required string AdventureId { get; init; }
    /// <summary>Gets the overall readiness state.</summary>
    public required CompanionReadinessState OverallState { get; init; }
    /// <summary>Gets the authoritative evaluation instant.</summary>
    public required DateTimeOffset EvaluatedAtUtc { get; init; }
    /// <summary>Gets the readiness categories.</summary>
    public required IReadOnlyList<CompanionReadinessCategoryDto> Categories { get; init; }
    /// <summary>Gets traveler-visible actions.</summary>
    public required IReadOnlyList<CompanionReadinessActionDto> Actions { get; init; }
}

/// <summary>Represents one typed, safe Playbook entry.</summary>
public sealed record CompanionPlaybookEntryDto
{
    /// <summary>Gets the stable entry identity.</summary>
    [Required, StringLength(128, MinimumLength = 1), RegularExpression(CompanionContractLimits.OpaqueIdentityPattern)] public required string EntryId { get; init; }
    /// <summary>Gets the safe title.</summary>
    [Required, StringLength(200, MinimumLength = 1)] public required string Title { get; init; }
    /// <summary>Gets an optional safe summary.</summary>
    [StringLength(2000)] public string? Summary { get; init; }
}

/// <summary>Represents one ordered Playbook section.</summary>
public sealed record CompanionPlaybookSectionDto
{
    /// <summary>Gets the stable section identity.</summary>
    [Required, StringLength(128, MinimumLength = 1), RegularExpression(CompanionContractLimits.OpaqueIdentityPattern)] public required string SectionId { get; init; }
    /// <summary>Gets the closed section type.</summary>
    [Required, StringLength(64, MinimumLength = 1)] public required string SectionType { get; init; }
    /// <summary>Gets the safe title.</summary>
    [Required, StringLength(200, MinimumLength = 1)] public required string Title { get; init; }
    /// <summary>Gets an optional safe introduction.</summary>
    [StringLength(2000)] public string? Introduction { get; init; }
    /// <summary>Gets typed safe entries.</summary>
    [MaxLength(500)] public required IReadOnlyList<CompanionPlaybookEntryDto> Entries { get; init; }
}

/// <summary>Provides the structured traveler Playbook.</summary>
public sealed record CompanionPlaybookDto : CompanionProjectionDto
{
    /// <summary>Gets the opaque Adventure identity.</summary>
    [Required, StringLength(128, MinimumLength = 1), RegularExpression(CompanionContractLimits.OpaqueIdentityPattern)] public required string AdventureId { get; init; }
    /// <summary>Gets the opaque Playbook version.</summary>
    [Required, StringLength(64, MinimumLength = 1)] public required string PlaybookVersion { get; init; }
    /// <summary>Gets the opaque source plan version.</summary>
    [Required, StringLength(64, MinimumLength = 1)] public required string PlanVersion { get; init; }
    /// <summary>Gets the Playbook generation instant.</summary>
    public required DateTimeOffset PlaybookGeneratedAtUtc { get; init; }
    /// <summary>Gets the Playbook expiry instant.</summary>
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    /// <summary>Gets the freshness state.</summary>
    public required CompanionPlaybookStaleState StaleState { get; init; }
    /// <summary>Gets the ordered sections.</summary>
    [MaxLength(50)] public required IReadOnlyList<CompanionPlaybookSectionDto> Sections { get; init; }
    /// <summary>Gets selected Resource summaries without content bytes.</summary>
    [MaxLength(500)] public required IReadOnlyList<CompanionResourceSummaryDto> Resources { get; init; }
}
