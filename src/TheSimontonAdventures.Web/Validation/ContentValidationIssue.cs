using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Validation;

/// <summary>Represents one Creator-scoped content validation diagnostic.</summary>
public sealed record ContentValidationIssue
{
    /// <summary>Gets the Creator whose content produced the issue.</summary>
    public required CreatorId CreatorId { get; init; }

    /// <summary>Gets the diagnostic severity.</summary>
    public required ContentValidationSeverity Severity { get; init; }

    /// <summary>Gets a stable machine-readable diagnostic code.</summary>
    public required string Code { get; init; }

    /// <summary>Gets the human-readable diagnostic explanation.</summary>
    public required string Message { get; init; }
}
