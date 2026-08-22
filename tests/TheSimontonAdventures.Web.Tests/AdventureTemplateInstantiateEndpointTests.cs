using System.Security.Claims;
using AdventuresSuite.Identity;
using AdventuresSuite.Identity.ExternalId;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the cookie form boundary for Adventure Template instantiation.</summary>
public sealed class AdventureTemplateInstantiateEndpointTests
{
    /// <summary>The endpoint is POST-only and explicitly requires antiforgery validation.</summary>
    [Fact]
    public void MapEndpoint_IsPostOnlyAndRequiresAntiforgery()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        using var routes = builder.Build();

        routes.MapAdventureTemplateInstantiateEndpoint();

        var endpoint = ((IEndpointRouteBuilder)routes).DataSources
            .SelectMany(source => source.Endpoints).Single();
        Assert.Equal(["POST"], endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods);
        Assert.True(endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation);
    }

    /// <summary>A valid request reaches the service and redirects to the existing plan detail route.</summary>
    [Fact]
    public async Task HandleAsync_ValidForm_RedirectsToCreatedPrivatePlan()
    {
        var service = new RecordingService(new(
            AdventureTemplateInstantiateOutcome.Created, new("plan_template_01")));
        var context = Context(Form());

        await AdventureTemplateInstantiateEndpoints.HandleAsync(
            context, "creator_alpha_01", new WorkspaceActorResolver(), service, default);

        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal("/workspace/creators/creator_alpha_01/plans/plan_template_01",
            context.Response.Headers.Location);
        Assert.Equal("platform.portugal-by-rail", service.Command!.TemplateVersion.TemplateId);
        Assert.Equal("1.0", service.Command.TemplateVersion.Version);
        Assert.Equal(new DateOnly(2026, 10, 4), service.Command.StartDate);
        Assert.Equal("en-US", service.Command.RequestedLocale);
        Assert.Null(service.Command.ConfiguredOrigin);
    }

    /// <summary>An origin-aware form passes only the reviewed bounded origin and IANA time zone.</summary>
    [Fact]
    public async Task HandleAsync_OriginAwareForm_PassesConfiguredOrigin()
    {
        var service = new RecordingService(new(
            AdventureTemplateInstantiateOutcome.Created, new("plan_template_02")));
        var fields = Form().ToDictionary(item => item.Key, item => item.Value);
        fields["originName"] = "Phoenix, Arizona";
        fields["originTimeZone"] = "America/Phoenix";
        fields["oneWayDistanceMiles"] = "1300";
        fields["dailyDistanceMiles"] = "450";
        fields["outboundStop"] = new StringValues(["Albuquerque, New Mexico", "Denver, Colorado"]);
        fields["returnStop"] = new StringValues(["Cheyenne, Wyoming", "Moab, Utah"]);
        var context = Context(new FormCollection(fields));

        await AdventureTemplateInstantiateEndpoints.HandleAsync(
            context, "creator_alpha_01", new WorkspaceActorResolver(), service, default);

        Assert.Equal("Phoenix, Arizona", service.Command!.ConfiguredOrigin!.Name);
        Assert.Equal(new IanaTimeZone("America/Phoenix"), service.Command.ConfiguredOrigin.TimeZone);
        Assert.Equal(1300, service.Command.TravelEstimate!.OneWayDistanceMiles);
        Assert.Equal(450, service.Command.TravelEstimate.DailyDistanceMiles);
        Assert.Equal(3, service.Command.TravelEstimate.DaysEachWay);
        Assert.Collection(service.Command.TravelStops!,
            stop => Assert.Equal("Albuquerque, New Mexico", stop.Name),
            stop => Assert.Equal("Denver, Colorado", stop.Name),
            stop => Assert.Equal("Cheyenne, Wyoming", stop.Name),
            stop => Assert.Equal("Moab, Utah", stop.Name));
    }

    /// <summary>A partial origin cannot cross the web boundary.</summary>
    [Fact]
    public async Task HandleAsync_PartialOrigin_IsValidationFailure()
    {
        var service = new RecordingService(new(
            AdventureTemplateInstantiateOutcome.Created, new("plan_template_03")));
        var fields = Form().ToDictionary(item => item.Key, item => item.Value);
        fields["originName"] = "Phoenix, Arizona";
        var context = Context(new FormCollection(fields));

        await AdventureTemplateInstantiateEndpoints.HandleAsync(
            context, "creator_alpha_01", new WorkspaceActorResolver(), service, default);

        Assert.Null(service.Command);
        Assert.Equal(
            "/workspace/creators/creator_alpha_01/plans?template=validation",
            context.Response.Headers.Location);
    }

    /// <summary>Anonymous requests fail closed before the service sees template identity.</summary>
    [Fact]
    public async Task HandleAsync_AnonymousRequest_IsSafelyDenied()
    {
        var service = new RecordingService(new(AdventureTemplateInstantiateOutcome.Failed, null));
        var context = Context(Form());
        context.User = new ClaimsPrincipal(new ClaimsIdentity());

        await AdventureTemplateInstantiateEndpoints.HandleAsync(
            context, "creator_alpha_01", new WorkspaceActorResolver(), service, default);

        Assert.Null(service.Command);
        Assert.Equal("/workspace/creators/creator_alpha_01/plans?template=denied",
            context.Response.Headers.Location);
    }

    /// <summary>Failure redirects do not disclose the submitted template identity.</summary>
    [Theory]
    [InlineData(AdventureTemplateInstantiateOutcome.Denied, "denied")]
    [InlineData(AdventureTemplateInstantiateOutcome.Conflict, "conflict")]
    [InlineData(AdventureTemplateInstantiateOutcome.Failed, "failure")]
    public async Task HandleAsync_NonSuccess_DoesNotLeakTemplate(
        AdventureTemplateInstantiateOutcome outcome,
        string state)
    {
        const string templateId = "private-template-do-not-reflect";
        var service = new RecordingService(new(outcome, null));
        var context = Context(Form(templateId));

        await AdventureTemplateInstantiateEndpoints.HandleAsync(
            context, "creator_alpha_01", new WorkspaceActorResolver(), service, default);

        Assert.Equal($"/workspace/creators/creator_alpha_01/plans?template={state}",
            context.Response.Headers.Location);
        Assert.DoesNotContain(templateId, context.Response.Headers.Location.ToString());
    }

    private static DefaultHttpContext Context(IFormCollection form)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ApplicationUserClaims.UserId, "user_planner_01")], "cookie"))
        };
        context.Features.Set<IFormFeature>(new FormFeature(form));
        return context;
    }

    private static IFormCollection Form(string templateId = "platform.portugal-by-rail") =>
        new FormCollection(new Dictionary<string, StringValues>
        {
            ["idempotencyKey"] = "request_1234567890",
            ["templateId"] = templateId,
            ["templateVersion"] = "1.0",
            ["startDate"] = "2026-10-04",
            ["locale"] = "en-US"
        });

    private sealed class RecordingService(AdventureTemplateInstantiateResult result)
        : IAdventureTemplateInstantiateService
    {
        public AdventureTemplateInstantiateCommand? Command { get; private set; }

        public Task<AdventureTemplateInstantiateResult> InstantiateAsync(
            AdventureTemplateInstantiateCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(result);
        }
    }
}
