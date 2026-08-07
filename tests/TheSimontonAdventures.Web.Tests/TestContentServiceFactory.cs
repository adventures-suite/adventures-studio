using Microsoft.Extensions.FileProviders;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Services;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Creates content services rooted at the test output directory, where the
/// committed JSON content is copied during the build.
/// </summary>
internal static class TestContentServiceFactory
{
    /// <summary>
    /// Creates a JSON-backed travel-content service for integration tests.
    /// </summary>
    /// <returns>A service that reads the copied repository content.</returns>
    internal static JsonTravelContentService Create()
    {
        var hostEnvironment = CreateHostEnvironment();

        return new JsonTravelContentService(
            hostEnvironment,
            new JsonCreatorService(hostEnvironment));
    }

    /// <summary>
    /// Creates a host environment rooted at the test output directory.
    /// </summary>
    /// <param name="environmentName">
    /// The environment name exposed to services; Development is used when
    /// omitted.
    /// </param>
    /// <param name="contentRootPath">
    /// An optional content root; the copied test output is used when omitted.
    /// </param>
    /// <returns>A host environment suitable for JSON-backed service tests.</returns>
    internal static IHostEnvironment CreateHostEnvironment(
        string? environmentName = null,
        string? contentRootPath = null)
    {
        var resolvedContentRoot = contentRootPath ?? AppContext.BaseDirectory;

        return new TestHostEnvironment
        {
            EnvironmentName = environmentName ?? Environments.Development,
            ContentRootPath = resolvedContentRoot,
            ContentRootFileProvider =
                new PhysicalFileProvider(resolvedContentRoot)
        };
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        /// <summary>
        /// Gets or sets the name of the environment used by the test service.
        /// </summary>
        public string EnvironmentName { get; set; } = Environments.Development;

        /// <summary>
        /// Gets or sets the application name reported to framework services.
        /// </summary>
        public string ApplicationName { get; set; } =
            typeof(TestHostEnvironment).Assembly.FullName!;

        /// <summary>
        /// Gets or sets the directory containing the copied test content.
        /// </summary>
        public string ContentRootPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file provider used to read copied test content.
        /// </summary>
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
