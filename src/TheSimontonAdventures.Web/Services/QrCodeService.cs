using Microsoft.Extensions.Options;
using QRCoder;
using TheSimontonAdventures.Web.Options;

namespace TheSimontonAdventures.Web.Services;

public sealed class QrCodeService : IQrCodeService
{
    private readonly QrCodeOptions _options;

    public QrCodeService(IOptions<QrCodeOptions> options)
    {
        _options = options.Value;
    }

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
                "QrCodes:PublicBaseUrl has not been configured.");
        }

        var baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        var normalizedSlug = qrSlug.Trim().Trim('/');

        return $"{baseUrl}/go/{normalizedSlug}";
    }

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