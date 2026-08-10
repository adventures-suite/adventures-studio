using System.Text.Json;
using AdventuresSuite.Companion.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace AdventuresSuite.Api;

/// <summary>Creates bounded server support identifiers.</summary>
public interface ISupportIdProvider
{
    /// <summary>Creates the next support identifier.</summary>
    string Create();
}

/// <summary>Creates deterministic process-local support identifiers without embedding request data.</summary>
public sealed class SequentialSupportIdProvider : ISupportIdProvider
{
    private long _value;
    /// <inheritdoc />
    public string Create() => $"req_{Interlocked.Increment(ref _value):D8}";
}

/// <summary>Writes allowlisted authentication and authorization problems.</summary>
public sealed class CompanionAuthorizationResultHandler(ISupportIdProvider supportIds)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    /// <inheritdoc />
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var supportId = supportIds.Create();
        var status = authorizeResult.Challenged ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden;
        var code = authorizeResult.Challenged ? "authentication_required" : "insufficient_scope";
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers["X-Support-Id"] = supportId;
        context.Response.Headers.CacheControl = "no-store";
        var problem = CompanionProblems.Create(status, code, supportId);
        await JsonSerializer.SerializeAsync(
            context.Response.Body, problem, CompanionJsonSerializerContext.Default.CompanionProblemDto,
            context.RequestAborted);
    }
}

internal static class CompanionProblems
{
    internal static CompanionProblemDto Create(int status, string code, string supportId) => new()
    {
        Type = new Uri($"https://errors.adventuressuite.example/problems/{code.Replace('_', '-')}", UriKind.Absolute),
        Title = code switch
        {
            "invalid_request" => "The request is invalid.",
            "authentication_required" => "Authentication is required.",
            "insufficient_scope" => "The required capability is unavailable.",
            "temporarily_unavailable" => "The service is temporarily unavailable.",
            _ => "The requested resource is unavailable."
        },
        Status = status,
        Code = code,
        SupportId = supportId,
        Retryable = status is StatusCodes.Status429TooManyRequests or StatusCodes.Status503ServiceUnavailable
    };
}
