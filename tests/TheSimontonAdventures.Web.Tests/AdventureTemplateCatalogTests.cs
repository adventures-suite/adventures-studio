using AdventuresSuite.Identity;
using Microsoft.Extensions.FileProviders;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies authorized catalog discovery and the JSON-backed Alpha source.</summary>
public sealed class AdventureTemplateCatalogTests
{
    private static readonly UserId User = new("user_template_catalog_01");
    private static readonly CreatorId Creator = new("creator_template_catalog_01");
    private static readonly ActorIdentity Actor = new(ActorType.Human, User.Value, User);

    /// <summary>An authorized customer sees the source result only after membership and policy evaluation.</summary>
    [Fact]
    public async Task ListAsync_AuthorizedContext_ReturnsCatalog()
    {
        var source = new RecordingSource([Blueprint()]);
        var service = new AdventureTemplateCatalogQueryService(
            Membership(), new DecisionEvaluator(AuthorizationDecision.Allow(
                AuthorizationAuditRequirement.None)), source);

        var result = await service.ListAsync(Actor, Creator, "en-US");

        Assert.True(result.IsAllowed);
        Assert.Single(result.Templates);
        Assert.Equal(Creator, source.CreatorId);
    }

    /// <summary>A missing membership never queries the catalog source.</summary>
    [Fact]
    public async Task ListAsync_MissingMembership_FailsClosedBeforeSource()
    {
        var source = new RecordingSource([Blueprint()]);
        var service = new AdventureTemplateCatalogQueryService(
            new MembershipProvider(null), new DecisionEvaluator(
                AuthorizationDecision.Allow(AuthorizationAuditRequirement.None)), source);

        var result = await service.ListAsync(Actor, Creator, "en-US");

        Assert.False(result.IsAllowed);
        Assert.Empty(result.Templates);
        Assert.Null(source.CreatorId);
    }

    /// <summary>The development source loads complete deterministic templates from JSON.</summary>
    [Fact]
    public async Task DevelopmentSource_LoadsCompleteJsonTemplatesAndExactVersions()
    {
        var source = new DevelopmentAdventureTemplateCatalogSource(new TestHostEnvironment
        {
            ContentRootPath = FindApplicationRoot()
        });

        var templates = await source.ListAsync(Creator, "en-US");
        var portugal = Assert.Single(templates, item =>
            item.VersionId.TemplateId == "platform.portugal-by-rail");
        Assert.Equal("1.0", portugal.VersionId.Version);
        Assert.Equal(8, portugal.Days.Count);
        Assert.Equal(3, portugal.Destinations.Count);
        Assert.Equal(2, portugal.Transportation.Count);
        Assert.Equal(3, portugal.Accommodations.Count);

        var authorized = await source.ResolveUseAsync(
            Actor, Creator, portugal.VersionId, "en-US");
        Assert.NotNull(authorized);
        Assert.Contains("platform.portugal-by-rail", authorized.UseDecisionReference,
            StringComparison.Ordinal);
        Assert.Null(await source.ResolveUseAsync(
            Actor, Creator, new("platform.portugal-by-rail", "2.0"), "en-US"));
    }

    private static AdventureTemplateBlueprint Blueprint() => new()
    {
        VersionId = new("platform.test", "1.0"),
        OwnerType = AdventureTemplateOwnerType.Platform,
        OwnerId = "adventures-suite",
        SourceLocale = "en-US",
        Attribution = "Test catalog",
        Title = "Test Journey",
        DurationDays = 1
    };

    private static ICreatorMembershipProvider Membership() => new MembershipProvider(new(
        new CreatorMembershipId("membership_template_catalog_01"), User, Creator,
        CreatorMembershipStatus.Active, [CreatorRole.Owner], [], 2,
        new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero)));

    private static string FindApplicationRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "TheSimontonAdventures.Web");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the web application root.");
    }

    private sealed class MembershipProvider(CreatorMembershipSnapshot? membership)
        : ICreatorMembershipProvider
    {
        public Task<CreatorMembershipSnapshot?> GetMembershipAsync(
            UserId userId,
            CreatorId creatorId,
            CancellationToken cancellationToken = default) => Task.FromResult(membership);
    }

    private sealed class DecisionEvaluator(AuthorizationDecision decision)
        : IAuthorizationPolicyEvaluator
    {
        public Task<AuthorizationDecision> AuthorizeAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(decision);
    }

    private sealed class RecordingSource(IReadOnlyList<AdventureTemplateBlueprint> templates)
        : IAdventureTemplateCatalogSource
    {
        public CreatorId? CreatorId { get; private set; }

        public Task<IReadOnlyList<AdventureTemplateBlueprint>> ListAsync(
            CreatorId customerCreatorId,
            string requestedLocale,
            CancellationToken cancellationToken = default)
        {
            CreatorId = customerCreatorId;
            return Task.FromResult(templates);
        }

        public Task<AuthorizedAdventureTemplateUse?> ResolveUseAsync(
            ActorIdentity actor,
            CreatorId customerCreatorId,
            AdventureTemplateVersionId templateVersion,
            string requestedLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AuthorizedAdventureTemplateUse?>(null);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "TheSimontonAdventures.Web.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
