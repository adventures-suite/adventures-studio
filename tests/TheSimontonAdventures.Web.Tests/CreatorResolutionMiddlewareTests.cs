using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies the request-scoped Creator Context middleware boundary.
/// </summary>
public sealed class CreatorResolutionMiddlewareTests
{
    /// <summary>
    /// Ensures an approved request establishes context before downstream
    /// middleware executes.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ApprovedHost_EstablishesContextBeforeNext()
    {
        var expectedContext = CreateContext(
            "creator_one_01",
            "one.example.com");
        var resolver = new StubCreatorResolver(
            new Dictionary<string, CreatorContext>
            {
                ["one.example.com"] = expectedContext
            });
        var accessor = new CreatorContextAccessor();
        var nextCalled = false;
        var middleware = new CreatorResolutionMiddleware(_ =>
        {
            nextCalled = true;
            Assert.Same(expectedContext, accessor.Current);
            return Task.CompletedTask;
        });
        var httpContext = CreateHttpContext("one.example.com");

        await middleware.InvokeAsync(httpContext, resolver, accessor);

        Assert.True(nextCalled);
        Assert.Same(expectedContext, accessor.Current);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// Ensures an unknown host receives a safe response without invoking
    /// downstream handlers or establishing flagship context.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_UnknownHost_Returns421WithoutCallingNext()
    {
        var resolver = new StubCreatorResolver(
            new Dictionary<string, CreatorContext>());
        var accessor = new CreatorContextAccessor();
        var nextCalled = false;
        var middleware = new CreatorResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var httpContext = CreateHttpContext("unknown.example.com");

        await middleware.InvokeAsync(httpContext, resolver, accessor);

        Assert.False(nextCalled);
        Assert.Equal(
            StatusCodes.Status421MisdirectedRequest,
            httpContext.Response.StatusCode);
        Assert.Throws<InvalidOperationException>(() => accessor.Current);

        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        Assert.DoesNotContain(
            "The Simonton Adventures",
            responseBody,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures pipeline re-execution within one request scope reuses established
    /// context instead of resolving the host again.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ContextAlreadyEstablished_ResolvesOnlyOnce()
    {
        var expectedContext = CreateContext(
            "creator_one_01",
            "one.example.com");
        var resolver = new StubCreatorResolver(
            new Dictionary<string, CreatorContext>
            {
                ["one.example.com"] = expectedContext
            });
        var accessor = new CreatorContextAccessor();
        var nextCallCount = 0;
        var middleware = new CreatorResolutionMiddleware(_ =>
        {
            nextCallCount++;
            return Task.CompletedTask;
        });
        var httpContext = CreateHttpContext("one.example.com");

        await middleware.InvokeAsync(httpContext, resolver, accessor);
        await middleware.InvokeAsync(httpContext, resolver, accessor);

        Assert.Equal(1, resolver.ResolveCallCount);
        Assert.Equal(2, nextCallCount);
        Assert.Same(expectedContext, accessor.Current);
    }

    /// <summary>
    /// Ensures concurrent requests use independent accessors so one Creator
    /// Context cannot overwrite or bleed into another request.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ConcurrentRequests_KeepContextsIsolated()
    {
        var firstContext = CreateContext(
            "creator_one_01",
            "one.example.com");
        var secondContext = CreateContext(
            "creator_two_01",
            "two.example.com");
        var resolver = new StubCreatorResolver(
            new Dictionary<string, CreatorContext>
            {
                ["one.example.com"] = firstContext,
                ["two.example.com"] = secondContext
            });
        var firstAccessor = new CreatorContextAccessor();
        var secondAccessor = new CreatorContextAccessor();
        var middleware = new CreatorResolutionMiddleware(async _ =>
            await Task.Yield());

        await Task.WhenAll(
            middleware.InvokeAsync(
                CreateHttpContext("one.example.com"),
                resolver,
                firstAccessor),
            middleware.InvokeAsync(
                CreateHttpContext("two.example.com"),
                resolver,
                secondAccessor));

        Assert.Same(firstContext, firstAccessor.Current);
        Assert.Same(secondContext, secondAccessor.Current);
        Assert.NotEqual(firstAccessor.Current.Id, secondAccessor.Current.Id);
    }

    /// <summary>
    /// Ensures dependency injection exposes one accessor instance within a
    /// request scope and different instances across request scopes.
    /// </summary>
    [Fact]
    public void DependencyInjection_ScopedAccessor_IsolatedAcrossScopes()
    {
        var services = new ServiceCollection();
        services.AddScoped<CreatorContextAccessor>();
        services.AddScoped<ICreatorContextAccessor>(provider =>
            provider.GetRequiredService<CreatorContextAccessor>());
        using var serviceProvider = services.BuildServiceProvider();
        using var firstScope = serviceProvider.CreateScope();
        using var secondScope = serviceProvider.CreateScope();

        var firstConcrete = firstScope.ServiceProvider
            .GetRequiredService<CreatorContextAccessor>();
        var firstContract = firstScope.ServiceProvider
            .GetRequiredService<ICreatorContextAccessor>();
        var secondConcrete = secondScope.ServiceProvider
            .GetRequiredService<CreatorContextAccessor>();

        Assert.Same(firstConcrete, firstContract);
        Assert.NotSame(firstConcrete, secondConcrete);
    }

    private static DefaultHttpContext CreateHttpContext(string host)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString(host);
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    private static CreatorContext CreateContext(
        string creatorId,
        string host)
    {
        return new CreatorContext
        {
            Id = new CreatorId(creatorId),
            Slug = creatorId,
            DisplayName = creatorId,
            RequestedHost = host,
            PrimaryDomain = host,
            Brand = new CreatorBrand { SiteName = creatorId },
            Features = new CreatorFeatures(),
            Locale = "en-US",
            TimeZone = "UTC",
            ContentRoot = "Content/Volumes"
        };
    }

    private sealed class StubCreatorResolver(
        IReadOnlyDictionary<string, CreatorContext> contexts) : ICreatorResolver
    {
        private int _resolveCallCount;

        internal int ResolveCallCount => _resolveCallCount;

        /// <inheritdoc />
        public Task<CreatorContext?> ResolveAsync(
            HostString host,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _resolveCallCount);
            contexts.TryGetValue(host.Host, out var context);
            return Task.FromResult(context);
        }
    }
}
