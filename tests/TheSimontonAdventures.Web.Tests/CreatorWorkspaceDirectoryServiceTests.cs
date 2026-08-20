using AdventuresSuite.Identity;
using Microsoft.Extensions.FileProviders;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies authorization-first Creator workspace discovery.</summary>
public sealed class CreatorWorkspaceDirectoryServiceTests
{
    private static readonly UserId User = new("user_workspace_directory");
    private static readonly ActorIdentity Actor = new(ActorType.Human, User.Value, User);

    /// <summary>Only active, non-development Creators with proven Planner access are returned.</summary>
    [Fact]
    public async Task ListAsync_ReturnsOnlyAuthorizedProductionWorkspaces()
    {
        var allowed = Creator("creator_allowed", "Allowed Adventures");
        var denied = Creator("creator_denied", "Denied Adventures");
        var development = Creator("creator_development", "Development Adventures", developmentOnly: true);
        var memberships = new MembershipProvider(new Dictionary<CreatorId, CreatorMembershipSnapshot>
        {
            [allowed.Id] = Membership(allowed.Id, 3),
            [denied.Id] = Membership(denied.Id, 4),
            [development.Id] = Membership(development.Id, 5)
        });
        var authorization = new AuthorizationEvaluator(allowed.Id);
        var service = new CreatorWorkspaceDirectoryService(
            new CreatorService([denied, development, allowed]),
            memberships,
            authorization,
            new HostEnvironment(Environments.Production));

        var result = await service.ListAsync(Actor);

        var choice = Assert.Single(result);
        Assert.Equal(allowed.Id, choice.CreatorId);
        Assert.Equal(allowed.DisplayName, choice.DisplayName);
        Assert.DoesNotContain(result, item => item.CreatorId == denied.Id);
        Assert.DoesNotContain(result, item => item.CreatorId == development.Id);
    }

    /// <summary>Missing membership never reaches authorization and reveals no Creator choice.</summary>
    [Fact]
    public async Task ListAsync_MissingMembership_DoesNotAuthorize()
    {
        var authorization = new AuthorizationEvaluator();
        var service = new CreatorWorkspaceDirectoryService(
            new CreatorService([Creator("creator_absent", "Absent Adventures")]),
            new MembershipProvider(new Dictionary<CreatorId, CreatorMembershipSnapshot>()),
            authorization,
            new HostEnvironment(Environments.Production));

        var result = await service.ListAsync(Actor);

        Assert.Empty(result);
        Assert.Equal(0, authorization.CallCount);
    }

    /// <summary>The membership concurrency version is preserved in the collection authorization request.</summary>
    [Fact]
    public async Task ListAsync_UsesCurrentMembershipVersionAndCollectionScope()
    {
        var creator = Creator("creator_versioned", "Versioned Adventures");
        var authorization = new AuthorizationEvaluator(creator.Id);
        var service = new CreatorWorkspaceDirectoryService(
            new CreatorService([creator]),
            new MembershipProvider(new Dictionary<CreatorId, CreatorMembershipSnapshot>
            {
                [creator.Id] = Membership(creator.Id, 19)
            }),
            authorization,
            new HostEnvironment(Environments.Production));

        await service.ListAsync(Actor);

        Assert.Equal(19, authorization.LastRequest?.MembershipVersion);
        Assert.Equal(creator.Id, authorization.LastRequest?.Resource.CreatorId);
        Assert.Equal(Permissions.AdventurePlanView, authorization.LastRequest?.Permission);
        Assert.Null(authorization.LastRequest?.Resource.ResourceId);
    }

    /// <summary>Malformed or non-human actors are denied before Creator enumeration.</summary>
    [Fact]
    public async Task ListAsync_NonHumanActor_DoesNotEnumerateCreators()
    {
        var creators = new CreatorService([Creator("creator_hidden", "Hidden Adventures")]);
        var service = new CreatorWorkspaceDirectoryService(
            creators,
            new MembershipProvider(new Dictionary<CreatorId, CreatorMembershipSnapshot>()),
            new AuthorizationEvaluator(),
            new HostEnvironment(Environments.Production));

        var result = await service.ListAsync(new ActorIdentity(ActorType.BackgroundJob, "workload_directory"));

        Assert.Empty(result);
        Assert.Equal(0, creators.CallCount);
    }

    private static Creator Creator(string id, string displayName, bool developmentOnly = false) => new()
    {
        Id = new(id),
        Slug = id,
        DisplayName = displayName,
        Status = CreatorStatus.Active,
        DevelopmentOnly = developmentOnly,
        PrimaryDomain = $"{id}.example.test",
        Domains = [$"{id}.example.test"],
        ContentRoot = id
    };

    private static CreatorMembershipSnapshot Membership(CreatorId creatorId, long version) => new(
        new($"membership_{creatorId.Value}"),
        User,
        creatorId,
        CreatorMembershipStatus.Active,
        [CreatorRole.Viewer],
        [],
        version,
        new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

    private sealed class CreatorService(IReadOnlyList<Creator> creators) : ICreatorService
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<Creator>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(creators);
        }

        public Task<Creator?> GetByIdAsync(CreatorId creatorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(creators.SingleOrDefault(item => item.Id == creatorId));

        public Task<Creator?> GetByHostAsync(string host, CancellationToken cancellationToken = default) =>
            Task.FromResult<Creator?>(null);
    }

    private sealed class MembershipProvider(IReadOnlyDictionary<CreatorId, CreatorMembershipSnapshot> memberships)
        : ICreatorMembershipProvider
    {
        public Task<CreatorMembershipSnapshot?> GetMembershipAsync(
            UserId userId,
            CreatorId creatorId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(memberships.GetValueOrDefault(creatorId));
    }

    private sealed class AuthorizationEvaluator(params CreatorId[] allowedCreators) : IAuthorizationPolicyEvaluator
    {
        private readonly HashSet<CreatorId> allowed = [.. allowedCreators];

        public int CallCount { get; private set; }
        public AuthorizationRequest? LastRequest { get; private set; }

        public Task<AuthorizationDecision> AuthorizeAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(allowed.Contains(request.Resource.CreatorId)
                ? AuthorizationDecision.Allow(AuthorizationAuditRequirement.None)
                : AuthorizationDecision.Deny(AuthorizationDenialReason.PermissionRequired));
        }
    }

    private sealed class HostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "CreatorWorkspaceDirectoryTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
