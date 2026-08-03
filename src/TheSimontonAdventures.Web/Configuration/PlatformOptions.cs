using System.ComponentModel.DataAnnotations;

namespace TheSimontonAdventures.Web.Configuration;

public sealed class PlatformOptions
{
    public const string SectionName = "Platform";

    [Required]
    [Url]
    public string PublicBaseUrl { get; init; } = string.Empty;

    public bool EnableCompanion { get; init; }

    public bool EnableReservations { get; init; }

    public bool EnableTelemetry { get; init; }
}