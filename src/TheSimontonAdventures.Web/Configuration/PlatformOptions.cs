using System.ComponentModel.DataAnnotations;

namespace TheSimontonAdventures.Web.Configuration;

/// <summary>
/// Defines deployment-specific platform URLs and feature switches bound from
/// the <c>Platform</c> configuration section.
/// </summary>
public sealed class PlatformOptions
{
    /// <summary>Identifies the configuration section bound to these options.</summary>
    public const string SectionName = "Platform";

    /// <summary>
    /// Gets the absolute public origin used when generating durable QR URLs.
    /// </summary>
    [Required]
    [Url]
    public string PublicBaseUrl { get; init; } = string.Empty;

    /// <summary>Gets whether Adventures Companion UI is available.</summary>
    public bool EnableCompanion { get; init; }

    /// <summary>Gets whether reservation capabilities are available.</summary>
    public bool EnableReservations { get; init; }

    /// <summary>Gets whether platform telemetry collection is enabled.</summary>
    public bool EnableTelemetry { get; init; }
}
