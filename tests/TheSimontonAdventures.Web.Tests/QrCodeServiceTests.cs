using Microsoft.Extensions.Options;
using TheSimontonAdventures.Web.Configuration;
using TheSimontonAdventures.Web.Services;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies public URL normalization and QR image generation.
/// </summary>
public sealed class QrCodeServiceTests
{
    /// <summary>
    /// Ensures public URLs normalize surrounding slashes and whitespace.
    /// </summary>
    [Fact]
    public void BuildPublicUrl_NormalizesBaseUrlAndSlug()
    {
        var service = CreateService("https://example.com/");

        var url = service.BuildPublicUrl(" /venice/ ");

        Assert.Equal("https://example.com/go/venice", url);
    }

    /// <summary>
    /// Ensures empty slugs are rejected rather than producing invalid QR links.
    /// </summary>
    [Fact]
    public void BuildPublicUrl_EmptySlug_ThrowsArgumentException()
    {
        var service = CreateService("https://example.com");

        Assert.Throws<ArgumentException>(() => service.BuildPublicUrl(" "));
    }

    /// <summary>
    /// Ensures QR generation requires a configured public base URL.
    /// </summary>
    [Fact]
    public void BuildPublicUrl_MissingBaseUrl_ThrowsInvalidOperationException()
    {
        var service = CreateService(string.Empty);

        Assert.Throws<InvalidOperationException>(() =>
            service.BuildPublicUrl("venice"));
    }

    /// <summary>
    /// Ensures generated SVG and PNG data use their expected representations.
    /// </summary>
    [Fact]
    public void GenerateImages_ProducesSvgAndPngData()
    {
        var service = CreateService("https://example.com");

        var svg = service.GenerateSvg("venice");
        var png = service.GeneratePng("venice");

        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
        Assert.True(png.Length > 8);
        Assert.Equal(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
            png[..8]);
    }

    private static QrCodeService CreateService(string publicBaseUrl)
    {
        return new QrCodeService(
            Options.Create(
                new PlatformOptions
                {
                    PublicBaseUrl = publicBaseUrl
                }));
    }
}
