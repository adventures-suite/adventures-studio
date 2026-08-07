using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies approved-host and development-alias Creator resolution behavior.
/// </summary>
public sealed class CreatorResolverTests
{
    /// <summary>
    /// Ensures an approved host is normalized and resolved to an immutable
    /// active Creator Context.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_ApprovedHostWithPort_ReturnsContext()
    {
        var creator = CreateCreator();
        var resolver = CreateResolver(
            Environments.Production,
            [creator]);

        var context = await resolver.ResolveAsync(
            new HostString("EXAMPLE.COM", 443));

        Assert.NotNull(context);
        Assert.Equal(creator.Id, context.Id);
        Assert.Equal("example.com", context.RequestedHost);
        Assert.Equal(creator.Brand, context.Brand);
    }

    /// <summary>
    /// Ensures a configured localhost alias resolves its explicit Creator and
    /// correctly removes the development port.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_DevelopmentAliasWithPort_ReturnsContext()
    {
        var creator = CreateCreator();
        var resolver = CreateResolver(
            Environments.Development,
            [creator],
            new Dictionary<string, string>
            {
                ["localhost"] = creator.Id.Value
            });

        var context = await resolver.ResolveAsync(
            new HostString("localhost", 7041));

        Assert.NotNull(context);
        Assert.Equal(creator.Id, context.Id);
        Assert.Equal("localhost", context.RequestedHost);
    }

    /// <summary>
    /// Ensures development aliases cannot silently select the flagship Creator
    /// in a production environment.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_ProductionIgnoresDevelopmentAlias_ReturnsNull()
    {
        var creator = CreateCreator();
        var resolver = CreateResolver(
            Environments.Production,
            [creator],
            new Dictionary<string, string>
            {
                ["localhost"] = creator.Id.Value
            });

        var context = await resolver.ResolveAsync(new HostString("localhost"));

        Assert.Null(context);
    }

    /// <summary>Ensures an unknown production host never receives Creator Context.</summary>
    [Fact]
    public async Task ResolveAsync_UnknownProductionHost_ReturnsNull()
    {
        var resolver = CreateResolver(
            Environments.Production,
            [CreateCreator()]);

        var context = await resolver.ResolveAsync(
            new HostString("unknown.example.com"));

        Assert.Null(context);
    }

    /// <summary>
    /// Ensures Azure's environment-specific default hostname resolves only to
    /// the explicitly assigned Creator identity.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_AzureEnvironmentHost_ReturnsAssignedCreator()
    {
        var creator = CreateCreator();
        var resolver = CreateResolver(
            Environments.Production,
            [creator],
            azureHost: "suite-dev.azurewebsites.net");

        var context = await resolver.ResolveAsync(
            new HostString("SUITE-DEV.AZUREWEBSITES.NET", 443));

        Assert.NotNull(context);
        Assert.Equal(creator.Id, context.Id);
        Assert.Equal("suite-dev.azurewebsites.net", context.RequestedHost);
    }

    /// <summary>Ensures inactive Creators expose no public context.</summary>
    [Theory]
    [InlineData(CreatorStatus.Draft)]
    [InlineData(CreatorStatus.Inactive)]
    [InlineData(CreatorStatus.Disabled)]
    public async Task ResolveAsync_NonActiveCreator_ReturnsNull(CreatorStatus status)
    {
        var creator = CreateCreator(status);
        var resolver = CreateResolver(
            Environments.Production,
            [creator]);

        var context = await resolver.ResolveAsync(new HostString("example.com"));

        Assert.Null(context);
    }

    /// <summary>
    /// Ensures aliases that collide after normalization fail configuration
    /// instead of selecting an arbitrary Creator.
    /// </summary>
    [Fact]
    public void Constructor_DuplicateNormalizedAliases_ThrowsInvalidDataException()
    {
        var creator = CreateCreator();

        Assert.Throws<InvalidDataException>(() => CreateResolver(
            Environments.Development,
            [creator],
            new Dictionary<string, string>
            {
                ["LOCALHOST"] = creator.Id.Value,
                ["localhost."] = creator.Id.Value
            }));
    }

    /// <summary>Ensures empty request hosts do not resolve a Creator.</summary>
    [Fact]
    public async Task ResolveAsync_EmptyHost_ReturnsNull()
    {
        var resolver = CreateResolver(
            Environments.Production,
            [CreateCreator()]);

        var context = await resolver.ResolveAsync(new HostString());

        Assert.Null(context);
    }

    private static CreatorResolver CreateResolver(
        string environmentName,
        IReadOnlyList<Creator> creators,
        Dictionary<string, string>? aliases = null,
        string? azureHost = null)
    {
        return new CreatorResolver(
            new StubCreatorService(creators),
            TestContentServiceFactory.CreateHostEnvironment(environmentName),
            Options.Create(
                new CreatorResolutionOptions
                {
                    DevelopmentAliases = aliases ?? [],
                    AzureDefaultCreatorId = creators[0].Id.Value
                }),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["WEBSITE_HOSTNAME"] = azureHost
                })
                .Build());
    }

    private static Creator CreateCreator(
        CreatorStatus status = CreatorStatus.Active)
    {
        return new Creator
        {
            Id = new CreatorId("creator_test_01"),
            Slug = "test-creator",
            DisplayName = "Test Creator",
            Status = status,
            PrimaryDomain = "example.com",
            Domains = ["example.com"],
            Brand = new CreatorBrand { SiteName = "Test Creator" },
            Features = new CreatorFeatures { EnableCompanion = true },
            ContentRoot = "Content/Volumes"
        };
    }

    private sealed class StubCreatorService(
        IReadOnlyList<Creator> creators) : ICreatorService
    {
        /// <inheritdoc />
        public Task<IReadOnlyList<Creator>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(creators);

        /// <inheritdoc />
        public Task<Creator?> GetByIdAsync(
            CreatorId creatorId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(creators.FirstOrDefault(
                creator => creator.Id == creatorId));
        }

        /// <inheritdoc />
        public Task<Creator?> GetByHostAsync(
            string host,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(creators.FirstOrDefault(creator =>
                creator.Domains.Contains(
                    host,
                    StringComparer.OrdinalIgnoreCase)));
        }
    }
}
