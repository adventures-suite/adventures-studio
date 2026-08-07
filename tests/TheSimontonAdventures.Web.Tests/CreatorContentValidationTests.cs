using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Validation;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the committed flagship content validation baseline.</summary>
public sealed class CreatorContentValidationTests
{
    /// <summary>
    /// Ensures committed content contains no startup-blocking Creator-scoped
    /// validation diagnostics.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_FlagshipContent_HasNoErrors()
    {
        var environment = TestContentServiceFactory.CreateHostEnvironment();
        var creatorService = new JsonCreatorService(environment);
        var validator = new CreatorContentValidator(
            environment,
            creatorService,
            TestContentServiceFactory.Create(),
            new StubResourceService(
                knownHeroUrl: "/images/home/adventures-studio-hero.jpeg"));

        var result = await validator.ValidateAsync(
            new CreatorId("creator_tsa_01"));

        Assert.False(
            result.HasErrors,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(issue => issue.Message)));
    }
}
