using TheSimontonAdventures.Web.Routing;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies canonical route construction for travel content.
/// </summary>
public sealed class TravelRoutesTests
{
    /// <summary>
    /// Ensures a volume route uses the canonical root-relative format.
    /// </summary>
    [Fact]
    public void Volume_ValidSlug_ReturnsCanonicalRoute()
    {
        var route = TravelRoutes.Volume("italy-greece-croatia");

        Assert.Equal("/volumes/italy-greece-croatia", route);
    }

    /// <summary>
    /// Ensures a destination route uses the canonical root-relative format.
    /// </summary>
    [Fact]
    public void Destination_ValidSlugs_ReturnsCanonicalRoute()
    {
        var route = TravelRoutes.Destination(
            "italy-greece-croatia",
            "italy",
            "venice");

        Assert.Equal(
            "/volumes/italy-greece-croatia/italy/venice",
            route);
    }

    /// <summary>
    /// Ensures each route segment is trimmed and escaped independently.
    /// </summary>
    [Fact]
    public void Destination_SegmentsNeedingEscaping_ReturnsSafeRoute()
    {
        var route = TravelRoutes.Destination(
            " caribbean cruise ",
            "st. maarten",
            "philipsburg/harbor");

        Assert.Equal(
            "/volumes/caribbean%20cruise/st.%20maarten/philipsburg%2Fharbor",
            route);
    }

    /// <summary>
    /// Ensures missing route identity is rejected rather than producing an
    /// ambiguous or malformed URL.
    /// </summary>
    [Theory]
    [InlineData("", "italy", "venice")]
    [InlineData("volume", " ", "venice")]
    [InlineData("volume", "italy", "")]
    public void Destination_MissingSegment_ThrowsArgumentException(
        string volumeSlug,
        string countrySlug,
        string destinationSlug)
    {
        Assert.Throws<ArgumentException>(() =>
            TravelRoutes.Destination(
                volumeSlug,
                countrySlug,
                destinationSlug));
    }
}
