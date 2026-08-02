namespace TheSimontonAdventures.Web.Options;

public sealed class QrCodeOptions
{
    public const string SectionName = "QrCodes";

    public string PublicBaseUrl { get; init; } = string.Empty;
}