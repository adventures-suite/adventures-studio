namespace TheSimontonAdventures.Web.Validation;

/// <summary>Describes the operational impact of a content validation issue.</summary>
public enum ContentValidationSeverity
{
    /// <summary>The issue is observable but does not prevent startup.</summary>
    Warning,

    /// <summary>The issue violates a public-content or isolation invariant.</summary>
    Error
}
