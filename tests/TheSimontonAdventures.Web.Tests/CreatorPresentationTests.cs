using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Presentation;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies Creator-owned branding and feature presentation remains isolated.
/// </summary>
public sealed class CreatorPresentationTests
{
    /// <summary>
    /// Ensures two Creator Context instances retain distinct brand and feature
    /// values even when consumed concurrently.
    /// </summary>
    [Fact]
    public async Task PresentationValues_TwoCreators_DoNotBleed()
    {
        var first = CreateContext(
            "creator_one_01",
            "Creator One",
            "#112233",
            enableCompanion: true,
            CreatorTypography.Classic);
        var second = CreateContext(
            "creator_two_01",
            "Creator Two",
            "#abcdef",
            enableCompanion: false,
            CreatorTypography.Modern);

        var results = await Task.WhenAll(
            Task.Run(() => Snapshot(first)),
            Task.Run(() => Snapshot(second)));

        Assert.Equal("Creator One", results[0].SiteName);
        Assert.Equal("#112233", results[0].PrimaryColor);
        Assert.True(results[0].EnableCompanion);
        Assert.Contains("Georgia", results[0].FontFamily);
        Assert.Equal("Creator Two", results[1].SiteName);
        Assert.Equal("#abcdef", results[1].PrimaryColor);
        Assert.False(results[1].EnableCompanion);
        Assert.Contains("system-ui", results[1].FontFamily);
    }

    /// <summary>
    /// Ensures optional brand text falls back to Creator identity rather than
    /// a shared flagship value.
    /// </summary>
    [Fact]
    public void PresentationValues_EmptyOptionalBrandText_UsesCreatorFallbacks()
    {
        var context = CreateContext(
            "creator_three_01",
            "Creator Three",
            "#123456",
            enableCompanion: false,
            CreatorTypography.Classic,
            useEmptyBrandText: true);

        Assert.Equal("Creator Three", context.GetSiteName());
        Assert.Equal("Creator Three", context.GetDefaultSeoTitle());
        Assert.Equal("Creator Three", context.GetCopyrightNotice());
        Assert.Equal(string.Empty, context.GetDefaultSeoDescription());
    }

    private static PresentationSnapshot Snapshot(CreatorContext context) =>
        new(
            context.GetSiteName(),
            context.Brand.PrimaryColor,
            context.Features.EnableCompanion,
            context.GetFontFamily());

    private static CreatorContext CreateContext(
        string creatorId,
        string displayName,
        string primaryColor,
        bool enableCompanion,
        CreatorTypography typography,
        bool useEmptyBrandText = false)
    {
        return new CreatorContext
        {
            Id = new CreatorId(creatorId),
            Slug = creatorId,
            DisplayName = displayName,
            RequestedHost = $"{creatorId}.example.test",
            PrimaryDomain = $"{creatorId}.example.test",
            Brand = new CreatorBrand
            {
                SiteName = useEmptyBrandText ? string.Empty : displayName,
                Tagline = useEmptyBrandText ? string.Empty : $"{displayName} tagline",
                CopyrightNotice = useEmptyBrandText ? string.Empty : displayName,
                DefaultSeoTitle = useEmptyBrandText ? string.Empty : displayName,
                DefaultSeoDescription = useEmptyBrandText
                    ? string.Empty
                    : $"{displayName} description",
                PrimaryColor = primaryColor,
                Typography = typography
            },
            Features = new CreatorFeatures
            {
                EnableCompanion = enableCompanion
            },
            Locale = "en-US",
            TimeZone = "UTC",
            ContentRoot = "Content/Volumes"
        };
    }

    private sealed record PresentationSnapshot(
        string SiteName,
        string PrimaryColor,
        bool EnableCompanion,
        string FontFamily);
}
