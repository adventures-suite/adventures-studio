using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Services;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies Creator-owned public URL construction and QR image generation.
/// </summary>
public sealed class QrCodeServiceTests
{
    /// <summary>
    /// Ensures public URLs use the resolved Creator's normalized primary domain.
    /// </summary>
    [Fact]
    public void BuildPublicUrl_UsesCreatorPrimaryDomain()
    {
        var service = new QrCodeService();
        var context = CreateContext("Creator.Example.COM.");

        var url = service.BuildPublicUrl(context, " /venice/ ");

        Assert.Equal("https://creator.example.com/go/venice", url);
    }

    /// <summary>
    /// Ensures two Creators encode the same slug beneath their own durable
    /// public domains.
    /// </summary>
    [Fact]
    public void BuildPublicUrl_SharedSlug_UsesEachCreatorDomain()
    {
        var service = new QrCodeService();

        var firstUrl = service.BuildPublicUrl(
            CreateContext("one.example.com", "creator_one_01"),
            "acropolis");
        var secondUrl = service.BuildPublicUrl(
            CreateContext("two.example.com", "creator_two_01"),
            "acropolis");

        Assert.Equal("https://one.example.com/go/acropolis", firstUrl);
        Assert.Equal("https://two.example.com/go/acropolis", secondUrl);
    }

    /// <summary>
    /// Ensures empty and multi-segment slugs are rejected rather than producing
    /// invalid QR links.
    /// </summary>
    [Theory]
    [InlineData(" ")]
    [InlineData("folder/slug")]
    [InlineData("slug?query")]
    [InlineData("slug#fragment")]
    public void BuildPublicUrl_InvalidSlug_ThrowsArgumentException(string slug)
    {
        var service = new QrCodeService();

        Assert.Throws<ArgumentException>(() =>
            service.BuildPublicUrl(CreateContext("example.com"), slug));
    }

    /// <summary>
    /// Ensures QR generation requires a valid resolved Creator Context.
    /// </summary>
    [Fact]
    public void BuildPublicUrl_DefaultCreatorId_ThrowsArgumentException()
    {
        var service = new QrCodeService();
        var context = CreateContext("example.com", creatorId: null);

        Assert.Throws<ArgumentException>(() =>
            service.BuildPublicUrl(context, "venice"));
    }

    /// <summary>
    /// Ensures generated SVG and PNG data use their expected representations.
    /// </summary>
    [Fact]
    public void GenerateImages_ProducesSvgAndPngData()
    {
        var service = new QrCodeService();
        var context = CreateContext("example.com");

        var svg = service.GenerateSvg(context, "venice");
        var png = service.GeneratePng(context, "venice");

        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
        Assert.True(png.Length > 8);
        Assert.Equal(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
            png[..8]);
    }

    private static CreatorContext CreateContext(
        string primaryDomain,
        string? creatorId = "creator_test_01")
    {
        return new CreatorContext
        {
            Id = creatorId is null ? default : new CreatorId(creatorId),
            Slug = "test-creator",
            DisplayName = "Test Creator",
            RequestedHost = primaryDomain,
            PrimaryDomain = primaryDomain,
            Brand = new CreatorBrand(),
            Features = new CreatorFeatures(),
            Locale = "en-US",
            TimeZone = "UTC",
            ContentRoot = "Content/Volumes"
        };
    }
}
