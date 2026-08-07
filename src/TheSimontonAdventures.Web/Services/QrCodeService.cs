using QRCoder;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Services;

/// <summary>
/// Generates print and screen QR images that point to stable public addresses.
/// </summary>
public sealed class QrCodeService : IQrCodeService
{
    /// <inheritdoc />
    public string BuildPublicUrl(
        CreatorContext creatorContext,
        string qrSlug)
    {
        ArgumentNullException.ThrowIfNull(creatorContext);

        if (creatorContext.Id == default)
        {
            throw new ArgumentException(
                "Creator Context must contain a non-default identity.",
                nameof(creatorContext));
        }

        if (string.IsNullOrWhiteSpace(qrSlug))
        {
            throw new ArgumentException(
                "A QR slug is required.",
                nameof(qrSlug));
        }

        if (!CreatorHost.TryNormalize(
            creatorContext.PrimaryDomain,
            out var primaryDomain))
        {
            throw new ArgumentException(
                "Creator Context must contain a valid primary domain.",
                nameof(creatorContext));
        }

        var normalizedSlug = qrSlug.Trim().Trim('/');

        if (normalizedSlug.Length == 0
            || normalizedSlug.Contains('/')
            || normalizedSlug.Contains('\\')
            || normalizedSlug.Contains('?')
            || normalizedSlug.Contains('#'))
        {
            throw new ArgumentException(
                "A single-segment QR slug is required.",
                nameof(qrSlug));
        }

        return $"https://{primaryDomain}/go/{Uri.EscapeDataString(normalizedSlug)}";
    }

    /// <inheritdoc />
    public string GenerateSvg(
        CreatorContext creatorContext,
        string qrSlug)
    {
        var publicUrl = BuildPublicUrl(creatorContext, qrSlug);

        using var qrCodeData = QRCodeGenerator.GenerateQrCode(
            publicUrl,
            QRCodeGenerator.ECCLevel.Q);

        using var svgQrCode = new SvgQRCode(qrCodeData);

        return svgQrCode.GetGraphic(
            pixelsPerModule: 20,
            darkColorHex: "#000000",
            lightColorHex: "#FFFFFF",
            drawQuietZones: true);
    }

    /// <inheritdoc />
    public byte[] GeneratePng(
        CreatorContext creatorContext,
        string qrSlug)
    {
        var publicUrl = BuildPublicUrl(creatorContext, qrSlug);

        using var qrCodeData = QRCodeGenerator.GenerateQrCode(
            publicUrl,
            QRCodeGenerator.ECCLevel.Q);

        using var pngQrCode = new PngByteQRCode(qrCodeData);

        return pngQrCode.GetGraphic(
            pixelsPerModule: 20,
            darkColorRgba: new byte[] { 0, 0, 0, 255 },
            lightColorRgba: new byte[] { 255, 255, 255, 255 },
            drawQuietZones: true);
    }
}
