using Microsoft.Extensions.Options;
using QRCoder;
using TheSimontonAdventures.Web.Configuration;

namespace TheSimontonAdventures.Web.Services;

/// <summary>
/// Generates print and screen QR images that point to stable public addresses.
/// </summary>
public sealed class QrCodeService : IQrCodeService
{
    private readonly PlatformOptions _options;

    /// <summary>Initializes a QR service with validated platform configuration.</summary>
    /// <param name="options">The configured public platform options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public QrCodeService(IOptions<PlatformOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    /// <inheritdoc />
    public string BuildPublicUrl(string qrSlug)
    {
        if (string.IsNullOrWhiteSpace(qrSlug))
        {
            throw new ArgumentException(
                "A QR slug is required.",
                nameof(qrSlug));
        }

        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            throw new InvalidOperationException(
                "Platform:PublicBaseUrl has not been configured.");
        }

        var baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        var normalizedSlug = qrSlug.Trim().Trim('/');

        return $"{baseUrl}/go/{normalizedSlug}";
    }

    /// <inheritdoc />
    public string GenerateSvg(string qrSlug)
    {
        var publicUrl = BuildPublicUrl(qrSlug);

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
    public byte[] GeneratePng(string qrSlug)
    {
        var publicUrl = BuildPublicUrl(qrSlug);

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
