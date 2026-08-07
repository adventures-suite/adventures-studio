namespace TheSimontonAdventures.Web.Creators;

/// <summary>
/// Resolves the incoming request host once and establishes immutable Creator
/// Context before downstream platform capabilities execute.
/// </summary>
public sealed class CreatorResolutionMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Initializes the Creator-resolution request boundary.</summary>
    /// <param name="next">The next middleware in the application pipeline.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="next"/> is <see langword="null"/>.
    /// </exception>
    public CreatorResolutionMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>Resolves and establishes Creator Context for one HTTP request.</summary>
    /// <param name="httpContext">The current HTTP request and response.</param>
    /// <param name="creatorResolver">The approved-host Creator resolver.</param>
    /// <param name="contextAccessor">The scoped Creator Context holder.</param>
    /// <returns>A task representing request-pipeline execution.</returns>
    public async Task InvokeAsync(
        HttpContext httpContext,
        ICreatorResolver creatorResolver,
        CreatorContextAccessor contextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(creatorResolver);
        ArgumentNullException.ThrowIfNull(contextAccessor);

        if (!contextAccessor.IsEstablished)
        {
            var creatorContext = await creatorResolver.ResolveAsync(
                httpContext.Request.Host,
                httpContext.RequestAborted);

            if (creatorContext is null)
            {
                httpContext.Response.StatusCode =
                    StatusCodes.Status421MisdirectedRequest;
                await httpContext.Response.WriteAsync(
                    "The requested host is not configured for this application.",
                    httpContext.RequestAborted);
                return;
            }

            contextAccessor.Establish(creatorContext);
        }

        await _next(httpContext);
    }
}
