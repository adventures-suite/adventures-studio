namespace TheSimontonAdventures.Web.Creators;

/// <summary>
/// Defines environment-specific host aliases used only for local Creator
/// resolution during development.
/// </summary>
public sealed class CreatorResolutionOptions
{
    /// <summary>Identifies the configuration section bound to these options.</summary>
    public const string SectionName = "CreatorResolution";

    /// <summary>
    /// Gets explicit development host-to-Creator identity mappings. These
    /// aliases are ignored outside the Development environment.
    /// </summary>
    public Dictionary<string, string> DevelopmentAliases { get; init; } = [];

    /// <summary>
    /// Gets the Creator identity assigned to Azure's environment-provided
    /// <c>WEBSITE_HOSTNAME</c> registration.
    /// </summary>
    public string AzureDefaultCreatorId { get; init; } = string.Empty;
}
