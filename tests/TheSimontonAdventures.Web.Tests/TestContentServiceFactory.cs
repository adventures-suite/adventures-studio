using Microsoft.Extensions.FileProviders;
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
        return new JsonTravelContentService(
            new TestHostEnvironment
            {
                ContentRootPath = AppContext.BaseDirectory,
                ContentRootFileProvider =
                    new PhysicalFileProvider(AppContext.BaseDirectory)
            });
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } =
            typeof(TestHostEnvironment).Assembly.FullName!;

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
