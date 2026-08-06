using TheSimontonAdventures.Web.Models;
using TheSimontonAdventures.Web.Services;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies stable public address resolution against committed content.
/// </summary>
public sealed class AddressableContentServiceTests
{
    /// <summary>
    /// Ensures a known QR slug resolves to its canonical destination route.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_KnownSlug_ReturnsCanonicalRoute()
    {
        var service = new AddressableContentService(
            TestContentServiceFactory.Create());

        var route = await service.ResolveAsync(" venice ");

        Assert.NotNull(route);
        Assert.Equal("venice", route.Slug);
        Assert.Equal(AddressableContentType.Destination, route.ContentType);
        Assert.Equal(
            "/volumes/italy-greece-croatia/italy/venice",
            route.TargetUrl);
        Assert.True(route.Published);
    }

    /// <summary>
    /// Ensures unknown and empty slugs are not exposed as public addresses.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-destination")]
    public async Task ResolveAsync_UnknownSlug_ReturnsNull(string slug)
    {
        var service = new AddressableContentService(
            TestContentServiceFactory.Create());

        var route = await service.ResolveAsync(slug);

        Assert.Null(route);
    }

    /// <summary>
    /// Ensures address enumeration exposes only published, unique routes in a
    /// deterministic order.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ReturnsUniqueSortedPublishedRoutes()
    {
        var service = new AddressableContentService(
            TestContentServiceFactory.Create());

        var routes = await service.GetAllAsync();

        Assert.NotEmpty(routes);
        Assert.All(routes, route => Assert.True(route.Published));
        Assert.Equal(
            routes.Select(route => route.Slug)
                .OrderBy(slug => slug, StringComparer.OrdinalIgnoreCase),
            routes.Select(route => route.Slug));
        Assert.Equal(
            routes.Count,
            routes.Select(route => route.Slug)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
    }
}
