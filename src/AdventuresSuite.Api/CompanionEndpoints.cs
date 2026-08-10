using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Claims;
using AdventuresSuite.Companion.Application;
using AdventuresSuite.Companion.Contracts;
using AdventuresSuite.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using TheSimontonAdventures.Web.Creators;

namespace AdventuresSuite.Api;

/// <summary>Maps the deterministic Companion v1 read contract.</summary>
public static class CompanionEndpoints
{
    /// <summary>Maps the first bounded Companion v1 read operation.</summary>
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
            .Produces<CompanionProblemDto>(StatusCodes.Status500InternalServerError, "application/problem+json")
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
                {
                    parameter.Description = parameter.Name switch
                    {
                        "limit" => $"Maximum results to return, from 1 through {CompanionContractLimits.MaximumPageSize}.",
                        "continuationToken" => "Opaque continuation token returned by an earlier authorized collection response.",
                        "includeCompleted" => "Includes completed Adventures when true; defaults to false.",
                        _ => parameter.Description
                    };
                }

                return Task.CompletedTask;
            });

        group.MapGet("/adventures/{adventureId}", GetAdventureAsync)
            .WithName("GetCompanionAdventure")
            .WithSummary("Gets one Adventure available to the current traveler")
            .WithDescription("Returns one bounded traveler-safe overview after current identity, Creator membership, authoritative ownership, and AdventurePlan.View evaluation. Unknown and inaccessible identifiers produce the same safe response.")
            .Produces<CompanionAdventureDto>(StatusCodes.Status200OK, "application/json")
            .Produces(StatusCodes.Status304NotModified)
            .Produces<CompanionProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<CompanionProblemDto>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<CompanionProblemDto>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<CompanionProblemDto>(StatusCodes.Status404NotFound, "application/problem+json")
            .Produces<CompanionProblemDto>(StatusCodes.Status500InternalServerError, "application/problem+json")
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                var parameter = operation.Parameters?.OfType<OpenApiParameter>()
                    .SingleOrDefault(value => value.Name == "adventureId");
                if (parameter is not null)
                    parameter.Description = "Bounded opaque Adventure identity; matching is case-sensitive.";
                return Task.CompletedTask;
            });

        return endpoints;
    }

    private static async Task<IResult> GetAdventureAsync(
        HttpContext context,
        ICompanionProjectionService service,
        ISupportIdProvider supportIds,
        string adventureId,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        using var activity = CompanionTelemetry.StartGetAdventure();
        var outcome = "error";
        var supportId = supportIds.Create();
        try
        {
            if (!IsValidOpaqueIdentity(adventureId))
            {
                outcome = "invalid";
                return Problem(context, StatusCodes.Status400BadRequest, "invalid_request", supportId);
            }

            var result = await service.GetAdventureAsync(
                CreateAccessContext(context.User), adventureId, supportId, cancellationToken);
            if (!result.IsAvailable)
            {
                outcome = "unavailable";
                return Problem(context, StatusCodes.Status404NotFound, "resource_unavailable", supportId);
            }

            var etag = $"\"{result.ProjectionVersion}\"";
            context.Response.Headers["X-Support-Id"] = supportId;
            context.Response.Headers.ETag = etag;
            context.Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
            if (context.Request.Headers.IfNoneMatch.Any(value => value == etag || value == "*"))
            {
                outcome = "not_modified";
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }

            outcome = "allowed";
            return Results.Ok(result.Value);
        }
        finally
        {
            CompanionTelemetry.Record(
                CompanionTelemetry.GetAdventureOperation,
                outcome,
                Stopwatch.GetElapsedTime(started),
                activity);
        }
    }

    private static async Task<IResult> ListAdventuresAsync(
        HttpContext context,
        ICompanionProjectionService service,
        ISupportIdProvider supportIds,
        [FromQuery, Range(1, CompanionContractLimits.MaximumPageSize)] int limit = CompanionContractLimits.DefaultPageSize,
        [FromQuery, StringLength(2048, MinimumLength = 1)] string? continuationToken = null,
        [FromQuery] bool includeCompleted = false,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        using var activity = CompanionTelemetry.StartListAdventures();
        var outcome = "error";
        var supportId = supportIds.Create();
        try
        {
            if (limit is < 1 or > CompanionContractLimits.MaximumPageSize || continuationToken?.Length > 2048)
            {
                outcome = "invalid";
                return Problem(context, StatusCodes.Status400BadRequest, "invalid_request", supportId);
            }

            var result = await service.ListAdventuresAsync(
                CreateAccessContext(context.User), limit, continuationToken, includeCompleted, supportId, cancellationToken);
            if (!result.IsAvailable)
            {
                outcome = "unavailable";
                return Problem(context, StatusCodes.Status404NotFound, "resource_unavailable", supportId);
            }

            var etag = $"\"{result.ProjectionVersion}\"";
            context.Response.Headers["X-Support-Id"] = supportId;
            context.Response.Headers.ETag = etag;
            context.Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
            if (context.Request.Headers.IfNoneMatch.Any(value => value == etag || value == "*"))
            {
                outcome = "not_modified";
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }

            outcome = "allowed";
            return Results.Ok(result.Value);
        }
        finally
        {
            CompanionTelemetry.Record(outcome, Stopwatch.GetElapsedTime(started), activity);
        }
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
        var userId = new UserId(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
        return new(
            new ActorIdentity(ActorType.Human, userId.Value, userId),
            principal.FindFirstValue("traveler_id") ?? string.Empty,
            new CreatorId(principal.FindFirstValue("creator_id") ?? string.Empty),
            long.TryParse(principal.FindFirstValue("membership_version"), out var membershipVersion)
                ? membershipVersion
                : 0,
            string.Equals(principal.FindFirstValue("revoked"), "true", StringComparison.Ordinal),
            scopes);
    }

    private static bool IsValidOpaqueIdentity(string value) =>
        value.Length is >= 1 and <= CompanionContractLimits.MaximumIdentityLength
        && char.IsAsciiLetterOrDigit(value[0])
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '.' or '_' or ':' or '-');
}
