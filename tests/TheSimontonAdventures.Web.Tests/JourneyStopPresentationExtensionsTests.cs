using TheSimontonAdventures.Web.Models;
using TheSimontonAdventures.Web.Presentation;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies shared journey-stop ordering and navigation behavior.
/// </summary>
public sealed class JourneyStopPresentationExtensionsTests
{
    /// <summary>
    /// Ensures stops are presented in their declared display order.
    /// </summary>
    [Fact]
    public void InDisplayOrder_UnorderedStops_ReturnsAscendingOrder()
    {
        JourneyStop[] stops =
        [
            new() { Title = "Third", DisplayOrder = 3 },
            new() { Title = "First", DisplayOrder = 1 },
            new() { Title = "Second", DisplayOrder = 2 }
        ];

        var ordered = stops.InDisplayOrder();

        Assert.Equal(
            ["First", "Second", "Third"],
            ordered.Select(stop => stop.Title));
    }

    /// <summary>
    /// Ensures a destination stop produces its canonical destination route.
    /// </summary>
    [Fact]
    public void GetDestinationRoute_LinkedStop_ReturnsCanonicalRoute()
    {
        var stop = new JourneyStop
        {
            CountrySlug = "italy",
            DestinationSlug = "venice"
        };

        var route = stop.GetDestinationRoute("italy-greece-croatia");

        Assert.Equal(
            "/volumes/italy-greece-croatia/italy/venice",
            route);
    }

    /// <summary>
    /// Ensures a journey stop without complete destination identity remains
    /// unlinked.
    /// </summary>
    [Theory]
    [InlineData("", "venice")]
    [InlineData("italy", "")]
    public void GetDestinationRoute_UnlinkedStop_ReturnsEmptyString(
        string countrySlug,
        string destinationSlug)
    {
        var stop = new JourneyStop
        {
            CountrySlug = countrySlug,
            DestinationSlug = destinationSlug
        };

        var route = stop.GetDestinationRoute("italy-greece-croatia");

        Assert.Equal(string.Empty, route);
    }
}
