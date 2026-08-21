using Microsoft.Extensions.Configuration;
using TheSimontonAdventures.Web.Components;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies fail-closed workspace navigation destination configuration.</summary>
public sealed class WorkspaceNavigationConfigurationTests
{
    /// <summary>Missing configuration leaves the Web application unavailable.</summary>
    [Fact]
    public void MissingUrl_LeavesDestinationUnavailable()
    {
        var configuration = BuildConfiguration(null);

        var result = WorkspaceNavigationConfiguration.FromConfiguration(configuration);

        Assert.Null(result.SimontonAdventuresUrl);
    }

    /// <summary>An absolute HTTPS destination is accepted without rewriting it.</summary>
    [Fact]
    public void HttpsUrl_IsAccepted()
    {
        const string configuredUrl = "https://simonton.example/public/story";

        var result = WorkspaceNavigationConfiguration.FromConfiguration(
            BuildConfiguration(configuredUrl));

        Assert.Equal(configuredUrl, result.SimontonAdventuresUrl?.AbsoluteUri);
    }

    /// <summary>Unsafe, relative, or credential-bearing destinations fail startup validation.</summary>
    [Theory]
    [InlineData("http://simonton.example/")]
    [InlineData("/public/story")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:password@simonton.example/")]
    public void UnsafeUrl_IsRejected(string configuredUrl)
    {
        Assert.Throws<InvalidOperationException>(() =>
            WorkspaceNavigationConfiguration.FromConfiguration(BuildConfiguration(configuredUrl)));
    }

    private static IConfiguration BuildConfiguration(string? configuredUrl)
    {
        var values = configuredUrl is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?>
            {
                [$"{WorkspaceNavigationConfiguration.SectionName}:SimontonAdventuresUrl"] = configuredUrl
            };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
