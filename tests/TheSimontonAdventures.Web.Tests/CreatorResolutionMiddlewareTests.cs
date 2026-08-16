using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TheSimontonAdventures.Web.Authorization;
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

        await InvokeAsync(middleware, httpContext, resolver, accessor);

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

        await InvokeAsync(middleware, httpContext, resolver, accessor);

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

        var trustedHostAccessor = new TrustedRequestHostContextAccessor();
        await InvokeAsync(middleware, httpContext, resolver, accessor, trustedHostAccessor);
        await InvokeAsync(middleware, httpContext, resolver, accessor, trustedHostAccessor);

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
            InvokeAsync(
                middleware,
                CreateHttpContext("one.example.com"),
                resolver,
                firstAccessor),
            InvokeAsync(
                middleware,
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

    /// <summary>
    /// Ensures the exact workspace origin is classified without inventing a
    /// public Creator context.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WorkspaceOrigin_EstablishesWorkspaceWithoutCreator()
    {
        var resolver = new StubCreatorResolver(new Dictionary<string, CreatorContext>
        {
            ["workspace.example.com"] = CreateContext(
                "creator_collision_01",
                "workspace.example.com")
        });
        var creatorAccessor = new CreatorContextAccessor();
        var trustedHostAccessor = new TrustedRequestHostContextAccessor();
        var nextCalled = false;
        var middleware = new CreatorResolutionMiddleware(_ =>
        {
            nextCalled = true;
            Assert.Equal(
                TrustedRequestHostType.PlatformWorkspace,
                trustedHostAccessor.Current.Type);
            Assert.Null(trustedHostAccessor.Current.Creator);
            Assert.Throws<InvalidOperationException>(() => creatorAccessor.Current);
            return Task.CompletedTask;
        });
        var context = CreateHttpContext("workspace.example.com");
        context.Request.Scheme = "https";

        await middleware.InvokeAsync(
            context,
            resolver,
            creatorAccessor,
            trustedHostAccessor,
            DenyPlatformHostClassifier.Instance,
            ExternalConfiguration());

        Assert.True(nextCalled);
        Assert.Equal(0, resolver.ResolveCallCount);
    }

    /// <summary>
    /// Ensures an explicitly approved public platform host does not acquire a
    /// Creator tenant or enter the private workspace branch.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_PublicPlatformHost_EstablishesPlatformWithoutCreator()
    {
        var resolver = new StubCreatorResolver(new Dictionary<string, CreatorContext>());
        var creatorAccessor = new CreatorContextAccessor();
        var trustedHostAccessor = new TrustedRequestHostContextAccessor();
        var nextCalled = false;
        var middleware = new CreatorResolutionMiddleware(_ =>
        {
            nextCalled = true;
            Assert.Equal(TrustedRequestHostType.PublicPlatform, trustedHostAccessor.Current.Type);
            Assert.Null(trustedHostAccessor.Current.Creator);
            Assert.Throws<InvalidOperationException>(() => creatorAccessor.Current);
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            CreateHttpContext("platform.example.com"),
            resolver,
            creatorAccessor,
            trustedHostAccessor,
            new ExactPlatformHostClassifier("platform.example.com"),
            AuthenticationConfiguration.Disabled());

        Assert.True(nextCalled);
        Assert.Equal(0, resolver.ResolveCallCount);
    }

    /// <summary>Ensures public and forged forwarded hosts cannot enter the workspace branch.</summary>
    [Theory]
    [InlineData("creator.example.com", "workspace.example.com")]
    [InlineData("unknown.example.com", "workspace.example.com")]
    public async Task InvokeAsync_ForwardedWorkspaceHost_DoesNotExpandTrust(
        string requestHost,
        string forwardedHost)
    {
        var publicContext = CreateContext("creator_one_01", "creator.example.com");
        var resolver = new StubCreatorResolver(new Dictionary<string, CreatorContext>
        {
            ["creator.example.com"] = publicContext
        });
        var creatorAccessor = new CreatorContextAccessor();
        var trustedHostAccessor = new TrustedRequestHostContextAccessor();
        var nextCalled = false;
        var middleware = new CreatorResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext(requestHost);
        context.Request.Scheme = "https";
        context.Request.Headers["X-Forwarded-Host"] = forwardedHost;
        context.Request.Headers["X-Forwarded-Proto"] = "https";

        await middleware.InvokeAsync(
            context,
            resolver,
            creatorAccessor,
            trustedHostAccessor,
            DenyPlatformHostClassifier.Instance,
            ExternalConfiguration());

        if (requestHost == "creator.example.com")
        {
            Assert.True(nextCalled);
            Assert.Equal(TrustedRequestHostType.PublicCreator, trustedHostAccessor.Current.Type);
        }
        else
        {
            Assert.False(nextCalled);
            Assert.Equal(StatusCodes.Status421MisdirectedRequest, context.Response.StatusCode);
        }
    }

    private static DefaultHttpContext CreateHttpContext(string host)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString(host);
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    private static Task InvokeAsync(
        CreatorResolutionMiddleware middleware,
        HttpContext httpContext,
        ICreatorResolver resolver,
        CreatorContextAccessor creatorAccessor,
        TrustedRequestHostContextAccessor? trustedHostAccessor = null) =>
        middleware.InvokeAsync(
            httpContext,
            resolver,
            creatorAccessor,
            trustedHostAccessor ?? new TrustedRequestHostContextAccessor(),
            DenyPlatformHostClassifier.Instance,
            AuthenticationConfiguration.Disabled());

    private static AuthenticationConfiguration ExternalConfiguration() => new(
        AuthenticationMode.ExternalProvider,
        "https://workspace.example.com",
        new ExternalIdentityProviderId("entra_external_id"),
        "https://tenant.ciamlogin.com/tenant/v2.0",
        "client-id",
        "certificate-reference",
        "/signin-oidc",
        "/signout-callback-oidc",
        TimeSpan.FromHours(8),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(5));

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

    private sealed class DenyPlatformHostClassifier : IPlatformHostClassifier
    {
        internal static DenyPlatformHostClassifier Instance { get; } = new();

        public bool IsPublicPlatformHost(HostString host) => false;
    }

    private sealed class ExactPlatformHostClassifier(string approvedHost) : IPlatformHostClassifier
    {
        public bool IsPublicPlatformHost(HostString host) =>
            string.Equals(host.Host, approvedHost, StringComparison.OrdinalIgnoreCase);
    }
}
