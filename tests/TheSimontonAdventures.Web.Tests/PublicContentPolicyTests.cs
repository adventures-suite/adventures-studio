using TheSimontonAdventures.Web.Models;
using TheSimontonAdventures.Web.Presentation;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies publication rules applied by public Razor routes.</summary>
public sealed class PublicContentPolicyTests
{
    /// <summary>Ensures draft volumes cannot be rendered publicly.</summary>
    [Fact]
    public void IsPublic_DraftVolume_ReturnsFalse()
    {
        var volume = new Volume { Status = VolumeStatus.Draft };

        Assert.False(PublicContentPolicy.IsPublic(volume));
    }

    /// <summary>Ensures planned and current volumes remain publicly visible.</summary>
    [Theory]
    [InlineData(VolumeStatus.Planned)]
    [InlineData(VolumeStatus.Upcoming)]
    [InlineData(VolumeStatus.Current)]
    [InlineData(VolumeStatus.Published)]
    public void IsPublic_PublicVolumeStatus_ReturnsTrue(VolumeStatus status)
    {
        var volume = new Volume { Status = status };

        Assert.True(PublicContentPolicy.IsPublic(volume));
    }

    /// <summary>
    /// Ensures an unpublished destination remains private even inside a public
    /// volume.
    /// </summary>
    [Fact]
    public void IsPublic_UnpublishedDestination_ReturnsFalse()
    {
        var volume = new Volume { Status = VolumeStatus.Current };
        var destination = new Destination { Published = false };

        Assert.False(PublicContentPolicy.IsPublic(volume, destination));
    }

    /// <summary>
    /// Ensures an unpublished journey remains private even inside a public
    /// volume.
    /// </summary>
    [Fact]
    public void IsPublic_UnpublishedJourney_ReturnsFalse()
    {
        var volume = new Volume { Status = VolumeStatus.Current };
        var journey = new Journey { Published = false };

        Assert.False(PublicContentPolicy.IsPublic(volume, journey));
    }

    /// <summary>
    /// Ensures a published child cannot escape a private owning volume.
    /// </summary>
    [Fact]
    public void IsPublic_PublishedChildrenInDraftVolume_ReturnFalse()
    {
        var volume = new Volume { Status = VolumeStatus.Draft };

        Assert.False(PublicContentPolicy.IsPublic(
            volume,
            new Destination { Published = true }));
        Assert.False(PublicContentPolicy.IsPublic(
            volume,
            new Journey { Published = true }));
    }
}
