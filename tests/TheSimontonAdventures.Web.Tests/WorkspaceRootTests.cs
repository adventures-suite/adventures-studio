using System.Security.Claims;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AdventuresSuite.Identity.ExternalId;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Components;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the Creator-independent workspace landing surface.</summary>
public sealed class WorkspaceRootTests
{
    /// <summary>
    /// Ensures sign-in uses a full navigation so the browser can follow the
    /// cross-origin External ID challenge instead of an enhanced fetch.
    /// </summary>
    [Fact]
    public async Task AnonymousWorkspace_DisablesEnhancedSignInNavigation()
    {
        var html = await RenderAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Contains("href=\"/authentication/sign-in\"", html);
        Assert.Contains("data-enhance-nav=\"false\"", html);
    }

    /// <summary>
    /// Ensures an authenticated workspace request renders a protected sign-out
    /// mutation without requiring public Creator Context.
    /// </summary>
    [Fact]
    public async Task AuthenticatedWorkspace_RendersProtectedPostSignOut()
    {
        var html = await RenderAsync(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "opaque-user")],
            authenticationType: "test")));

        Assert.Contains("You are signed in", html);
        Assert.Contains("method=\"post\"", html);
        Assert.Contains("action=\"/authentication/sign-out\"", html);
        Assert.DoesNotContain("opaque-user", html);
    }

    /// <summary>An explicitly addressed and authorized Creator renders only its dashboard projection.</summary>
    [Fact]
    public async Task AddressedCreatorRoute_RendersAuthorizedDashboard()
    {
        var query = new StubPlannerWorkspaceQueryService(
            PlannerWorkspaceResult.Allowed([new AdventurePlanDashboardItem
            {
                Id = new("plan_spain_2027"),
                Title = "Spain and Atlantic",
                LifecycleStage = AdventureLifecycleStage.Plan,
                Status = PlanningStatus.Planned,
                Dates = new(new(2027, 10, 25), new(2027, 11, 15)),
                Version = 7,
                IsArchived = false
            }]));
        var html = await RenderAsync(ApplicationPrincipal(),
            "/workspace/creators/creator_alpha_01/plans",
            services =>
            {
                services.AddSingleton<IWorkspaceActorResolver, WorkspaceActorResolver>();
                services.AddSingleton<IPlannerWorkspaceQueryService>(query);
            });

        Assert.Contains("Spain and Atlantic", html);
        Assert.Contains("Plan version", html);
        Assert.Equal(new CreatorId("creator_alpha_01"), query.LastCreatorId);
    }

    /// <summary>An authorized collection route renders only the approved manual creation fields.</summary>
    [Fact]
    public async Task AddressedCreatorRoute_RendersAntiforgeryProtectedCreateForm()
    {
        var html = await RenderAsync(ApplicationPrincipal(),
            "/workspace/creators/creator_alpha_01/plans",
            services =>
            {
                services.AddSingleton<IWorkspaceActorResolver, WorkspaceActorResolver>();
                services.AddSingleton<IPlannerWorkspaceQueryService>(
                    new StubPlannerWorkspaceQueryService(PlannerWorkspaceResult.Allowed([])));
            });

        Assert.Contains("action=\"/workspace/creators/creator_alpha_01/plans/create\"", html);
        Assert.Contains("name=\"idempotencyKey\"", html);
        Assert.Contains("name=\"title\"", html);
        Assert.Contains("name=\"description\"", html);
        Assert.Contains("name=\"startDate\"", html);
        Assert.Contains("name=\"endDate\"", html);
        Assert.DoesNotContain("name=\"status\"", html);
        Assert.DoesNotContain("name=\"lifecycle", html);
    }

    /// <summary>Creation failure states are allowlisted and never reflect query content.</summary>
    [Fact]
    public async Task AddressedCreatorRoute_CreateFailure_IsGenericAndDoesNotReflectInput()
    {
        const string secret = "PRIVATE-PLAN-TITLE";
        var html = await RenderAsync(ApplicationPrincipal(),
            $"/workspace/creators/creator_alpha_01/plans?create=failure&title={secret}",
            services =>
            {
                services.AddSingleton<IWorkspaceActorResolver, WorkspaceActorResolver>();
                services.AddSingleton<IPlannerWorkspaceQueryService>(
                    new StubPlannerWorkspaceQueryService(PlannerWorkspaceResult.Allowed([])));
            });

        Assert.Contains("The plan could not be created. Please try again.", html);
        Assert.DoesNotContain(secret, html);
    }

    /// <summary>An authorized instance route renders allowlisted details without sensitive values.</summary>
    [Fact]
    public async Task AddressedPlanRoute_RendersReadOnlyDetailWithoutSensitiveValues()
    {
        var query = new StubPlannerWorkspaceQueryService(
            PlannerWorkspaceResult.Denied(),
            PlannerPlanDetailResult.Allowed(new AdventurePlanDetail
            {
                Id = new("plan_spain_2027"),
                Title = "Spain and Atlantic",
                WorkingDescription = "Private working plan",
                LifecycleStage = AdventureLifecycleStage.Plan,
                Status = PlanningStatus.Planned,
                Dates = new(new(2027, 10, 25), new(2027, 11, 15)),
                Version = 7,
                TravelerCount = 2,
                Destinations = [new(new("visit_madrid"), "Madrid",
                    new(new(2027, 10, 26), new(2027, 10, 29)), new("Europe/Madrid"), 1)]
            }));
        var html = await RenderAsync(ApplicationPrincipal(),
            "/workspace/creators/creator_alpha_01/plans/plan_spain_2027",
            services =>
            {
                services.AddSingleton<IWorkspaceActorResolver, WorkspaceActorResolver>();
                services.AddSingleton<IPlannerWorkspaceQueryService>(query);
            });

        Assert.Contains("Spain and Atlantic", html);
        Assert.Contains("Madrid", html);
        Assert.Contains("Sensitive reservation references", html);
        Assert.DoesNotContain("RESERVATION-SECRET-123", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Traveler Private Name", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/overview", html, StringComparison.Ordinal);
        Assert.Equal(new AdventurePlanId("plan_spain_2027"), query.LastPlanId);
    }

    /// <summary>The detail route renders only the reviewed overview-edit fields and expected version.</summary>
    [Fact]
    public async Task AddressedPlanRoute_RendersMinimalOverviewEditForm()
    {
        var query = new StubPlannerWorkspaceQueryService(
            PlannerWorkspaceResult.Denied(),
            PlannerPlanDetailResult.Allowed(new AdventurePlanDetail
            {
                Id = new("plan_spain_2027"),
                Title = "Spain and Atlantic",
                WorkingDescription = "Private working plan",
                LifecycleStage = AdventureLifecycleStage.Plan,
                Status = PlanningStatus.Draft,
                Dates = new(new(2027, 10, 25), new(2027, 11, 15)),
                Version = 7,
                TravelerCount = 0,
                Destinations =
                [
                    new(new("visit_madrid_01"), "Madrid",
                        new(new(2027, 10, 25), new(2027, 10, 28)),
                        new("Europe/Madrid"), 1)
                ]
            }, canEdit: true));
        var html = await RenderAsync(ApplicationPrincipal(),
            "/workspace/creators/creator_alpha_01/plans/plan_spain_2027",
            services =>
            {
                services.AddSingleton<IWorkspaceActorResolver, WorkspaceActorResolver>();
                services.AddSingleton<IPlannerWorkspaceQueryService>(query);
            });

        Assert.Contains("action=\"/workspace/creators/creator_alpha_01/plans/plan_spain_2027/overview\"", html);
        Assert.Contains("name=\"expectedVersion\" value=\"7\"", html);
        Assert.Contains("name=\"title\" value=\"Spain and Atlantic\"", html);
        Assert.Contains("name=\"description\"", html);
        Assert.Contains("name=\"startDate\" value=\"2027-10-25\"", html);
        Assert.Contains("name=\"endDate\" value=\"2027-11-15\"", html);
        Assert.Contains("action=\"/workspace/creators/creator_alpha_01/plans/plan_spain_2027/destinations\"", html);
        Assert.Contains("name=\"timeZoneId\"", html);
        Assert.Contains("placeholder=\"Europe/Rome\"", html);
        Assert.Contains("action=\"/workspace/creators/creator_alpha_01/plans/plan_spain_2027/days\"", html);
        Assert.Contains("name=\"destinationVisitId\"", html);
        Assert.Contains("name=\"date\"", html);
        Assert.Contains("placeholder=\"Arrival in Rome\"", html);
        Assert.DoesNotContain("name=\"status\"", html);
        Assert.DoesNotContain("name=\"lifecycle", html);
    }

    /// <summary>Overview status messages use only allowlisted state and never reflect private input.</summary>
    [Fact]
    public async Task AddressedPlanRoute_EditFailure_DoesNotReflectSubmittedContent()
    {
        const string secret = "PRIVATE-OVERVIEW-TITLE";
        var query = new StubPlannerWorkspaceQueryService(
            PlannerWorkspaceResult.Denied(),
            PlannerPlanDetailResult.Allowed(new AdventurePlanDetail
            {
                Id = new("plan_spain_2027"),
                Title = "Current title",
                LifecycleStage = AdventureLifecycleStage.Plan,
                Status = PlanningStatus.Draft,
                Dates = new(new(2027, 10, 25), new(2027, 11, 15)),
                Version = 7,
                TravelerCount = 0
            }, canEdit: true));
        var html = await RenderAsync(ApplicationPrincipal(),
            $"/workspace/creators/creator_alpha_01/plans/plan_spain_2027?edit=failure&title={secret}",
            services =>
            {
                services.AddSingleton<IWorkspaceActorResolver, WorkspaceActorResolver>();
                services.AddSingleton<IPlannerWorkspaceQueryService>(query);
            });

        Assert.Contains("The plan overview could not be updated. Please try again.", html);
        Assert.DoesNotContain(secret, html);
    }

    /// <summary>Denied Creator routes return a generic state without protected plan content.</summary>
    [Fact]
    public async Task AddressedCreatorRoute_DeniedAccess_DoesNotRevealPlans()
    {
        var html = await RenderAsync(ApplicationPrincipal(),
            "/workspace/creators/creator_forged_01/plans",
            services =>
            {
                services.AddSingleton<IWorkspaceActorResolver, WorkspaceActorResolver>();
                services.AddSingleton<IPlannerWorkspaceQueryService>(
                    new StubPlannerWorkspaceQueryService(PlannerWorkspaceResult.Denied()));
            });

        Assert.Contains("Workspace unavailable", html);
        Assert.DoesNotContain("Spain and Atlantic", html);
        Assert.DoesNotContain("creator_forged_01", html);
    }

    /// <summary>Malformed or non-dashboard paths never invoke the private Planning query.</summary>
    [Fact]
    public async Task UnaddressedWorkspacePath_DoesNotQueryPlanning()
    {
        var query = new StubPlannerWorkspaceQueryService(PlannerWorkspaceResult.Denied());
        var html = await RenderAsync(ApplicationPrincipal(),
            "/workspace/creators/INVALID/plans",
            services =>
            {
                services.AddSingleton<IWorkspaceActorResolver, WorkspaceActorResolver>();
                services.AddSingleton<IPlannerWorkspaceQueryService>(query);
            });

        Assert.Contains("Choose a Creator workspace", html);
        Assert.Equal(0, query.CallCount);
    }

    /// <summary>Read failures render a safe retry state without exception or scope details.</summary>
    [Fact]
    public async Task AddressedCreatorRoute_ReadFailure_RendersSafeFailure()
    {
        var html = await RenderAsync(ApplicationPrincipal(),
            "/workspace/creators/creator_alpha_01/plans",
            services =>
            {
                services.AddSingleton<IWorkspaceActorResolver, WorkspaceActorResolver>();
                services.AddSingleton<IPlannerWorkspaceQueryService>(
                    new ThrowingPlannerWorkspaceQueryService());
            });

        Assert.Contains("Planner temporarily unavailable", html);
        Assert.DoesNotContain("creator_alpha_01", html);
        Assert.DoesNotContain("database detail", html);
    }

    private static ClaimsPrincipal ApplicationPrincipal() => new(new ClaimsIdentity(
        [new Claim(ApplicationUserClaims.UserId, "user_planner_01")],
        authenticationType: "test"));

    private static async Task<string> RenderAsync(
        ClaimsPrincipal user,
        string path = "/",
        Action<ServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAntiforgery();
        services.AddHttpContextAccessor();
        configure?.Invoke(services);
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = provider,
            User = user
        };
        context.Request.Path = path;
        var queryIndex = path.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            context.Request.Path = path[..queryIndex];
            context.Request.QueryString = new QueryString(path[queryIndex..]);
        }
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;

        await using var renderer = new HtmlRenderer(
            provider,
            provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<WorkspaceRoot>();
            return output.ToHtmlString();
        });

        return html;
    }

    private sealed class StubPlannerWorkspaceQueryService(
        PlannerWorkspaceResult result,
        PlannerPlanDetailResult? detailResult = null)
        : IPlannerWorkspaceQueryService
    {
        public int CallCount { get; private set; }
        public CreatorId LastCreatorId { get; private set; }
        public AdventurePlanId LastPlanId { get; private set; }

        public Task<PlannerWorkspaceResult> ListAsync(
            AdventuresSuite.Identity.ActorIdentity actor,
            CreatorId creatorId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastCreatorId = creatorId;
            return Task.FromResult(result);
        }

        public Task<PlannerPlanDetailResult> GetAsync(
            AdventuresSuite.Identity.ActorIdentity actor,
            CreatorId creatorId,
            AdventurePlanId planId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastCreatorId = creatorId;
            LastPlanId = planId;
            return Task.FromResult(detailResult ?? PlannerPlanDetailResult.Denied());
        }
    }

    private sealed class ThrowingPlannerWorkspaceQueryService : IPlannerWorkspaceQueryService
    {
        public Task<PlannerWorkspaceResult> ListAsync(
            AdventuresSuite.Identity.ActorIdentity actor,
            CreatorId creatorId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database detail");

        public Task<PlannerPlanDetailResult> GetAsync(
            AdventuresSuite.Identity.ActorIdentity actor,
            CreatorId creatorId,
            AdventurePlanId planId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database detail");
    }
}
