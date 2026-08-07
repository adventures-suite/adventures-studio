using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Validation;

/// <summary>Contains all validation diagnostics for one Creator snapshot.</summary>
public sealed record CreatorContentValidationResult
{
    /// <summary>Gets the Creator whose content was validated.</summary>
    public required CreatorId CreatorId { get; init; }

    /// <summary>Gets the immutable diagnostics produced by validation.</summary>
    public required IReadOnlyList<ContentValidationIssue> Issues { get; init; }

    /// <summary>Gets whether any startup-blocking diagnostics were found.</summary>
    public bool HasErrors => Issues.Any(issue =>
        issue.Severity == ContentValidationSeverity.Error);
}
