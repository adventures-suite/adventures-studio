namespace TheSimontonAdventures.Web.Creators;

/// <summary>
/// Defines Creator-scoped platform capabilities without placing mutable feature
/// state in global platform configuration.
/// </summary>
public sealed class CreatorFeatures
{
    /// <summary>Gets whether Creator-authored About content is available.</summary>
    public bool EnableAbout { get; init; }

    /// <summary>Gets whether Adventures Companion is enabled for the Creator.</summary>
    public bool EnableCompanion { get; init; }

    /// <summary>Gets whether reservation capabilities are enabled for the Creator.</summary>
    public bool EnableReservations { get; init; }

    /// <summary>Gets whether Creator-scoped telemetry is enabled.</summary>
    public bool EnableTelemetry { get; init; }
}
