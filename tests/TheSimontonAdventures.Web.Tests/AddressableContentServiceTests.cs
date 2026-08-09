using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Models;
using TheSimontonAdventures.Web.Services;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies stable public address resolution within explicit Creator scope.
/// </summary>
public sealed class AddressableContentServiceTests
{
    private static readonly CreatorId FlagshipCreatorId =
        new("creator_tsa_01");

    /// <summary>
    /// Ensures a known flagship QR slug resolves to its canonical destination
    /// route without an implicit Creator fallback.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_FlagshipKnownSlug_ReturnsCanonicalRoute()
    {
        var service = CreateFlagshipService();

        var route = await service.ResolveAsync(FlagshipCreatorId, " venice ");

        Assert.NotNull(route);
        Assert.Equal(FlagshipCreatorId, route.CreatorId);
        Assert.Equal("venice", route.Slug);
        Assert.Equal(AddressableContentType.Destination, route.ContentType);
        Assert.Equal(
            "/volumes/italy-greece-croatia/italy/venice",
            route.TargetUrl);
        Assert.True(route.Published);
    }

    /// <summary>
    /// Ensures an unknown Creator cannot retrieve flagship content through the
    /// temporary Content Engine adapter.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_OtherCreatorCannotReadFlagship_ReturnsNull()
    {
        var service = CreateFlagshipService();

        var route = await service.ResolveAsync(
            new CreatorId("creator_other_01"),
            "venice");

        Assert.Null(route);
    }

    /// <summary>
    /// Ensures unknown and malformed slugs are not exposed as public addresses.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-destination")]
    [InlineData("folder/slug")]
    public async Task ResolveAsync_UnknownSlug_ReturnsNull(string slug)
    {
        var service = CreateFlagshipService();

        var route = await service.ResolveAsync(FlagshipCreatorId, slug);

        Assert.Null(route);
    }

    /// <summary>
    /// Ensures address enumeration exposes only published, unique routes in a
    /// deterministic order for the requested Creator.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ReturnsUniqueSortedPublishedRoutes()
    {
        var service = CreateFlagshipService();

        var routes = await service.GetAllAsync(FlagshipCreatorId);

        Assert.NotEmpty(routes);
        Assert.All(routes, route =>
        {
            Assert.Equal(FlagshipCreatorId, route.CreatorId);
            Assert.True(route.Published);
        });
        Assert.Equal(
            routes.Select(route => route.Slug)
                .OrderBy(slug => slug, StringComparer.OrdinalIgnoreCase),
            routes.Select(route => route.Slug));
    }

    /// <summary>
    /// Ensures two Creators may own the same public slug while resolving to
    /// distinct targets within their respective ownership boundaries.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_TwoCreatorsShareSlug_ResolveOwnTargets()
    {
        var firstCreatorId = new CreatorId("creator_one_01");
        var secondCreatorId = new CreatorId("creator_two_01");
        var service = new AddressableContentService(
            new StubTravelContentService(
                new Dictionary<CreatorId, IReadOnlyList<Destination>>
                {
                    [firstCreatorId] =
                    [
                        CreateDestination("acropolis", "athens", "greece")
                    ],
                    [secondCreatorId] =
                    [
                        CreateDestination("acropolis", "athens-ga", "usa")
                    ]
                }));

        var firstRoute = await service.ResolveAsync(
            firstCreatorId,
            "acropolis");
        var secondRoute = await service.ResolveAsync(
            secondCreatorId,
            "acropolis");

        Assert.NotNull(firstRoute);
        Assert.NotNull(secondRoute);
        Assert.Equal(firstCreatorId, firstRoute.CreatorId);
        Assert.Equal(secondCreatorId, secondRoute.CreatorId);
        Assert.NotEqual(firstRoute.TargetUrl, secondRoute.TargetUrl);
    }

    /// <summary>
    /// Ensures aliases resolve only within their owning Creator and retain the
    /// canonical primary public slug.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_CreatorAlias_ReturnsCanonicalRoute()
    {
        var creatorId = new CreatorId("creator_one_01");
        var destination = CreateDestination("acropolis", "athens", "greece");
        destination = new Destination
        {
            VolumeSlug = destination.VolumeSlug,
            CountrySlug = destination.CountrySlug,
            Slug = destination.Slug,
            QrSlug = destination.QrSlug,
            QrAliases = ["ancient-athens"],
            Title = destination.Title,
            Published = true
        };
        var service = new AddressableContentService(
            new StubTravelContentService(
                new Dictionary<CreatorId, IReadOnlyList<Destination>>
                {
                    [creatorId] = [destination]
                }));

        var route = await service.ResolveAsync(creatorId, "ancient-athens");

        Assert.NotNull(route);
        Assert.Equal("acropolis", route.Slug);
        Assert.Contains("ancient-athens", route.Aliases);
    }

    /// <summary>
    /// Ensures unpublished destinations cannot produce public routes even when
    /// a backing adapter returns them accidentally.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_UnpublishedDestination_IsExcluded()
    {
        var creatorId = new CreatorId("creator_one_01");
        var unpublished = new Destination
        {
            VolumeSlug = "volume",
            CountrySlug = "country",
            Slug = "private",
            QrSlug = "private",
            Title = "Private",
            Published = false
        };
        var service = new AddressableContentService(
            new StubTravelContentService(
                new Dictionary<CreatorId, IReadOnlyList<Destination>>
                {
                    [creatorId] = [unpublished]
                }));

        var routes = await service.GetAllAsync(creatorId);

        Assert.Empty(routes);
    }

    /// <summary>
    /// Ensures duplicate aliases within one Creator fail validation rather than
    /// producing ambiguous public resolution.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_DuplicateAlias_ThrowsInvalidDataException()
    {
        var creatorId = new CreatorId("creator_one_01");
        var first = CreateDestination("first", "first", "country");
        var secondBase = CreateDestination("second", "second", "country");
        var second = new Destination
        {
            VolumeSlug = secondBase.VolumeSlug,
            CountrySlug = secondBase.CountrySlug,
            Slug = secondBase.Slug,
            QrSlug = secondBase.QrSlug,
            QrAliases = ["first"],
            Title = secondBase.Title,
            Published = true
        };
        var service = new AddressableContentService(
            new StubTravelContentService(
                new Dictionary<CreatorId, IReadOnlyList<Destination>>
                {
                    [creatorId] = [first, second]
                }));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.GetAllAsync(creatorId));
    }

    /// <summary>Ensures an absent Creator scope is rejected by the core contract.</summary>
    [Fact]
    public async Task ResolveAsync_DefaultCreatorId_ThrowsArgumentException()
    {
        var service = CreateFlagshipService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ResolveAsync(default, "venice"));
    }

    private static AddressableContentService CreateFlagshipService()
    {
        return new AddressableContentService(
            TestContentServiceFactory.Create());
    }

    private static Destination CreateDestination(
        string qrSlug,
        string destinationSlug,
        string countrySlug)
    {
        return new Destination
        {
            VolumeSlug = "shared-volume",
            CountrySlug = countrySlug,
            Slug = destinationSlug,
            QrSlug = qrSlug,
            Title = destinationSlug,
            Published = true
        };
    }

    private sealed class StubTravelContentService(
        IReadOnlyDictionary<CreatorId, IReadOnlyList<Destination>> destinations) :
        ITravelContentService
    {
        /// <inheritdoc />
        public Task<CreatorProfile?> GetCreatorProfileAsync(
            CreatorId creatorId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<Volume>> GetPublicVolumesAsync(
            CreatorId creatorId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Volume> volumes = destinations.ContainsKey(creatorId)
                ? [new Volume { Slug = "shared-volume", Status = VolumeStatus.Current }]
                : [];
            return Task.FromResult(volumes);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<Destination>>
            GetPublishedDestinationsForVolumeAsync(
                CreatorId creatorId,
                string volumeSlug,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                destinations.TryGetValue(creatorId, out var creatorDestinations)
                    ? creatorDestinations
                    : (IReadOnlyList<Destination>)[]);
        }

        /// <inheritdoc />
        public Task<Volume?> GetVolumeAsync(
            CreatorId creatorId,
            string volumeSlug,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public Task<Destination?> GetDestinationAsync(
            CreatorId creatorId,
            string volumeSlug,
            string countrySlug,
            string destinationSlug,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<Destination>> GetDestinationsForVolumeAsync(
            CreatorId creatorId,
            string volumeSlug,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<Destination>> GetFeaturedDestinationsAsync(
            CreatorId creatorId,
            string volumeSlug,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<Volume>> GetVolumesAsync(
            CreatorId creatorId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public Task<Volume?> GetCurrentVolumeAsync(
            CreatorId creatorId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public Task<QrDestinationRoute?> GetDestinationRouteByQrSlugAsync(
            CreatorId creatorId,
            string qrSlug,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public Task<Journey?> GetJourneyAsync(
            CreatorId creatorId,
            string volumeSlug,
            string journeySlug,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
