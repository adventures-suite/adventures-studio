namespace TheSimontonAdventures.Web.Services;

/// <summary>
/// Defines QR URL construction and image generation for stable public slugs.
/// </summary>
public interface IQrCodeService
{
    /// <summary>Builds the absolute public redirect URL encoded by a QR code.</summary>
    /// <param name="qrSlug">The stable slug without a <c>/go/</c> prefix.</param>
    /// <returns>The absolute public URL for the stable address.</returns>
    /// <exception cref="ArgumentException">The slug is empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// The public platform base URL is not configured.
    /// </exception>
    string BuildPublicUrl(string qrSlug);

    /// <summary>Generates a scalable QR image for a stable public slug.</summary>
    /// <param name="qrSlug">The stable slug to encode.</param>
    /// <returns>An SVG document containing the QR image.</returns>
    string GenerateSvg(string qrSlug);

    /// <summary>Generates a raster QR image for a stable public slug.</summary>
    /// <param name="qrSlug">The stable slug to encode.</param>
    /// <returns>The encoded PNG file bytes.</returns>
    byte[] GeneratePng(string qrSlug);
}
