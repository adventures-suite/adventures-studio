using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AdventuresSuite.Companion.Application;
using AdventuresSuite.Companion.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AdventuresSuite.Api;

/// <summary>Maps the deterministic Companion v1 read contract.</summary>
public static class CompanionEndpoints
{
    /// <summary>Maps all seven documented Companion v1 read operations.</summary>
    public static IEndpointRouteBuilder MapCompanionApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(CompanionApiConstants.BasePath)
            .WithTags("AdventuresCompanion")
            .RequireAuthorization(CompanionApiConstants.AuthorizationPolicy);

        group.MapGet("/adventures", ListAdventuresAsync)
            .WithName("ListCompanionAdventures")
            .WithSummary("Lists Adventures available to the current traveler")
            .WithDescription("Returns a bounded, deterministic page after current identity, participation, Creator, and scope evaluation. The private projection is short-lived; inaccessible fixture state is enumeration-safe.")
            .Produces<CompanionAdventureCollectionDto>(StatusCodes.Status200OK, "application/json")
            .Produces(StatusCodes.Status304NotModified)
            .Produces<CompanionProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<CompanionProblemDto>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<CompanionProblemDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<CompanionProblemDto>(StatusCodes.Status404NotFound, "application/problem+json")
            .Produces<CompanionProblemDto>(StatusCodes.Status500InternalServerError, "application/problem+json");

        MapAdventureGet(group, "", "GetCompanionAdventure", "Gets one traveler-safe Adventure overview",
            "Returns a private Adventure overview after exact current fixture participation and Creator isolation. Unknown, cross-Creator, cross-traveler, and revoked access share one unavailable response.",
            GetAdventureAsync).Produces<CompanionAdventureDto>(StatusCodes.Status200OK, "application/json");
        MapAdventureGet(group, "/today", "GetCompanionToday", "Gets Today and Next",
            "Returns a short-lived local-date projection after itinerary visibility evaluation. It never exposes reservations, private notes, payment data, or traveler lists.",
            GetTodayAsync).Produces<CompanionTodayDto>(StatusCodes.Status200OK, "application/json");
        MapAdventureGet(group, "/itinerary", "GetCompanionItinerary", "Gets the traveler-safe itinerary",
            "Returns bounded, deterministically ordered days and items after current itinerary visibility evaluation. Inaccessible identifiers remain enumeration-safe.",
            GetItineraryAsync).Produces<CompanionItineraryDto>(StatusCodes.Status200OK, "application/json");
        MapAdventureGet(group, "/readiness", "GetCompanionReadiness", "Gets traveler-visible readiness",
            "Returns a short-lived, minimized readiness projection. Sensitive actions and another traveler's completion state are excluded.",
            GetReadinessAsync).Produces<CompanionReadinessDto>(StatusCodes.Status200OK, "application/json");
        MapAdventureGet(group, "/playbook", "GetCompanionPlaybook", "Gets the structured traveler Playbook",
            "Returns bounded typed sections after selected Playbook profile evaluation. It contains no arbitrary Planning records, HTML, or protected Resource bytes.",
            GetPlaybookAsync).Produces<CompanionPlaybookDto>(StatusCodes.Status200OK, "application/json");

        group.MapGet("/resources/{resourceId}/content", DownloadResourceAsync)
            .WithName("DownloadCompanionResource")
            .WithSummary("Downloads one currently authorized protected Resource")
            .WithDescription("Declares the protected Resource operation while production delivery remains closed. This deterministic foundation always returns the same enumeration-safe unavailable problem and never emits Resource bytes or provider URLs.")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces<CompanionProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<CompanionProblemDto>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<CompanionProblemDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<CompanionProblemDto>(StatusCodes.Status404NotFound, "application/problem+json")
            .Produces<CompanionProblemDto>(StatusCodes.Status500InternalServerError, "application/problem+json");

        return endpoints;
    }

    private static RouteHandlerBuilder MapAdventureGet(
        RouteGroupBuilder group,
        string suffix,
        string operationId,
        string summary,
        string description,
        Delegate handler) =>
        group.MapGet($"/adventures/{{adventureId}}{suffix}", handler)
            .WithName(operationId)
            .WithSummary(summary)
            .WithDescription(description)
            .Produces(StatusCodes.Status304NotModified)
            .Produces<CompanionProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<CompanionProblemDto>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<CompanionProblemDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<CompanionProblemDto>(StatusCodes.Status404NotFound, "application/problem+json")
            .Produces<CompanionProblemDto>(StatusCodes.Status500InternalServerError, "application/problem+json");

    private static async Task<IResult> ListAdventuresAsync(
        HttpContext context,
        ICompanionProjectionService service,
        ISupportIdProvider supportIds,
        [FromQuery, Range(1, CompanionContractLimits.MaximumPageSize)] int limit = CompanionContractLimits.DefaultPageSize,
        [FromQuery, StringLength(2048, MinimumLength = 1)] string? continuationToken = null,
        [FromQuery] bool includeCompleted = false,
        CancellationToken cancellationToken = default)
    {
        var supportId = supportIds.Create();
        if (limit is < 1 or > CompanionContractLimits.MaximumPageSize || continuationToken?.Length > 2048)
            return Problem(context, StatusCodes.Status400BadRequest, "invalid_request", supportId);
        var result = await service.ListAdventuresAsync(
            CreateAccessContext(context.User), limit, continuationToken, includeCompleted, supportId, cancellationToken);
        return Projection(context, result, supportId);
    }

    private static Task<IResult> GetAdventureAsync(
        HttpContext context, ICompanionProjectionService service, ISupportIdProvider supportIds,
        [FromRoute] string adventureId, CancellationToken cancellationToken) =>
        GetProjectionAsync(context, service.GetAdventureAsync, supportIds, adventureId, cancellationToken);

    private static Task<IResult> GetTodayAsync(
        HttpContext context, ICompanionProjectionService service, ISupportIdProvider supportIds,
        [FromRoute] string adventureId, CancellationToken cancellationToken) =>
        GetProjectionAsync(context, service.GetTodayAsync, supportIds, adventureId, cancellationToken);

    private static Task<IResult> GetItineraryAsync(
        HttpContext context, ICompanionProjectionService service, ISupportIdProvider supportIds,
        [FromRoute] string adventureId, CancellationToken cancellationToken) =>
        GetProjectionAsync(context, service.GetItineraryAsync, supportIds, adventureId, cancellationToken);

    private static Task<IResult> GetReadinessAsync(
        HttpContext context, ICompanionProjectionService service, ISupportIdProvider supportIds,
        [FromRoute] string adventureId, CancellationToken cancellationToken) =>
        GetProjectionAsync(context, service.GetReadinessAsync, supportIds, adventureId, cancellationToken);

    private static Task<IResult> GetPlaybookAsync(
        HttpContext context, ICompanionProjectionService service, ISupportIdProvider supportIds,
        [FromRoute] string adventureId, CancellationToken cancellationToken) =>
        GetProjectionAsync(context, service.GetPlaybookAsync, supportIds, adventureId, cancellationToken);

    private static async Task<IResult> GetProjectionAsync<T>(
        HttpContext context,
        Func<CompanionAccessContext, string, string, CancellationToken, Task<CompanionQueryResult<T>>> query,
        ISupportIdProvider supportIds,
        string identity,
        CancellationToken cancellationToken)
        where T : CompanionProjectionDto
    {
        var supportId = supportIds.Create();
        if (!IsValidIdentity(identity))
            return Problem(context, StatusCodes.Status400BadRequest, "invalid_request", supportId);
        var result = await query(CreateAccessContext(context.User), identity, supportId, cancellationToken);
        return Projection(context, result, supportId);
    }

    private static IResult DownloadResourceAsync(
        HttpContext context, ISupportIdProvider supportIds, [FromRoute] string resourceId)
    {
        var supportId = supportIds.Create();
        return !IsValidIdentity(resourceId)
            ? Problem(context, StatusCodes.Status400BadRequest, "invalid_request", supportId)
            : Problem(context, StatusCodes.Status404NotFound, "resource_unavailable", supportId);
    }

    private static IResult Projection<T>(
        HttpContext context, CompanionQueryResult<T> result, string supportId)
        where T : CompanionProjectionDto
    {
        if (!result.IsAvailable)
            return Problem(context, StatusCodes.Status404NotFound, "resource_unavailable", supportId);
        var etag = $"\"{result.ProjectionVersion}\"";
        context.Response.Headers["X-Support-Id"] = supportId;
        context.Response.Headers.ETag = etag;
        context.Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
        if (context.Request.Headers.IfNoneMatch.Any(value => value == etag || value == "*"))
            return Results.StatusCode(StatusCodes.Status304NotModified);
        return Results.Ok(result.Value);
    }

    private static IResult Problem(HttpContext context, int status, string code, string supportId)
    {
        context.Response.Headers["X-Support-Id"] = supportId;
        context.Response.Headers.CacheControl = "no-store";
        return Results.Json(
            CompanionProblems.Create(status, code, supportId),
            CompanionJsonSerializerContext.Default.CompanionProblemDto,
            statusCode: status,
            contentType: "application/problem+json");
    }

    private static CompanionAccessContext CreateAccessContext(ClaimsPrincipal principal)
    {
        var scopes = (principal.FindFirstValue("scope") ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
        return new(
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            principal.FindFirstValue("traveler_id") ?? string.Empty,
            principal.FindFirstValue("creator_id") ?? string.Empty,
            string.Equals(principal.FindFirstValue("revoked"), "true", StringComparison.Ordinal),
            scopes);
    }

    private static bool IsValidIdentity(string value) =>
        value.Length is >= 1 and <= CompanionContractLimits.MaximumIdentityLength
        && System.Text.RegularExpressions.Regex.IsMatch(
            value, CompanionContractLimits.OpaqueIdentityPattern,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
}
