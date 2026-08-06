using TheSimontonAdventures.Web.Models;
using TheSimontonAdventures.Web.Presentation;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>
/// Verifies shared volume-number presentation formatting.
/// </summary>
public sealed class VolumePresentationExtensionsTests
{
    /// <summary>
    /// Ensures supported volume numbers use their expected Roman numeral.
    /// </summary>
    [Theory]
    [InlineData(1, "I")]
    [InlineData(2, "II")]
    [InlineData(3, "III")]
    [InlineData(4, "IV")]
    [InlineData(5, "V")]
    public void GetNumberLabel_SupportedNumber_ReturnsRomanNumeral(
        int number,
        string expected)
    {
        var volume = new Volume { Number = number };

        var label = volume.GetNumberLabel();

        Assert.Equal(expected, label);
    }

    /// <summary>
    /// Ensures unsupported values retain their invariant numeric representation.
    /// </summary>
    [Theory]
    [InlineData(0, "0")]
    [InlineData(6, "6")]
    public void GetNumberLabel_UnsupportedNumber_ReturnsNumericValue(
        int number,
        string expected)
    {
        var volume = new Volume { Number = number };

        var label = volume.GetNumberLabel();

        Assert.Equal(expected, label);
    }
}
