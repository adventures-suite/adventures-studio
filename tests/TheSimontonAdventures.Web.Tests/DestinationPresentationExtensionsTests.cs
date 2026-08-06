using TheSimontonAdventures.Web.Models;
using TheSimontonAdventures.Web.Presentation;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies shared destination presentation fallback rules.
/// </summary>
public sealed class DestinationPresentationExtensionsTests
{
    /// <summary>
    /// Ensures a homepage-specific image takes precedence on cards.
    /// </summary>
    [Fact]
    public void GetCardImage_HomepageImagePresent_ReturnsHomepageImage()
    {
        var destination = new Destination
        {
            HomepageImage = "/images/homepage.jpg",
            HeroImage = "/images/hero.jpg"
        };

        Assert.Equal("/images/homepage.jpg", destination.GetCardImage());
    }

    /// <summary>
    /// Ensures an empty homepage image falls back to the hero image.
    /// </summary>
    [Fact]
    public void GetCardImage_HomepageImageEmpty_ReturnsHeroImage()
    {
        var destination = new Destination
        {
            HomepageImage = "  ",
            HeroImage = "/images/hero.jpg"
        };

        Assert.Equal("/images/hero.jpg", destination.GetCardImage());
    }

    /// <summary>
    /// Ensures an empty destination image selection remains empty.
    /// </summary>
    [Fact]
    public void GetCardImage_AllImagesEmpty_ReturnsEmptyString()
    {
        var destination = new Destination();

        Assert.Equal(string.Empty, destination.GetCardImage());
    }

    /// <summary>
    /// Ensures a homepage-specific summary takes precedence on cards.
    /// </summary>
    [Fact]
    public void GetCardSummary_HomepageSummaryPresent_ReturnsHomepageSummary()
    {
        var destination = new Destination
        {
            HomepageSummary = "Homepage summary",
            Summary = "General summary"
        };

        Assert.Equal("Homepage summary", destination.GetCardSummary());
    }

    /// <summary>
    /// Ensures an empty homepage summary falls back to the general summary.
    /// </summary>
    [Fact]
    public void GetCardSummary_HomepageSummaryEmpty_ReturnsGeneralSummary()
    {
        var destination = new Destination
        {
            HomepageSummary = "  ",
            Summary = "General summary"
        };

        Assert.Equal("General summary", destination.GetCardSummary());
    }

    /// <summary>
    /// Ensures an empty destination summary selection remains empty.
    /// </summary>
    [Fact]
    public void GetCardSummary_AllSummariesEmpty_ReturnsEmptyString()
    {
        var destination = new Destination();

        Assert.Equal(string.Empty, destination.GetCardSummary());
    }
}
