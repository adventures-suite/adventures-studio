using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies deterministic selection of reviewed Journey travel-stop suggestions.</summary>
public sealed class AdventureTemplateTravelStopSuggestionTests
{
    private static readonly AdventureTemplateTravelStopSuggestion Suggestion = new(
        ["Phoenix AZ", "Phoenix, Arizona"],
        1300,
        450,
        ["Albuquerque, New Mexico", "Denver, Colorado"],
        ["Denver, Colorado", "Albuquerque, New Mexico"],
        "Reviewed planning starting points.");

    /// <summary>Equivalent reviewed origin labels select the exact catalog suggestion.</summary>
    [Fact]
    public void Find_EquivalentOriginAndExactPace_ReturnsSuggestion()
    {
        var result = AdventureTemplateTravelStopSuggestionSelector.Find(
            [Suggestion], "Phoenix, AZ", 1300, 450, 2);

        Assert.Same(Suggestion, result);
    }

    /// <summary>An unsupported pace returns no result rather than inventing route stops.</summary>
    [Fact]
    public void Find_UnsupportedPace_ReturnsNoSuggestion()
    {
        var result = AdventureTemplateTravelStopSuggestionSelector.Find(
            [Suggestion], "Phoenix AZ", 1300, 300, 4);

        Assert.Null(result);
    }

    /// <summary>A mismatched stop count cannot populate an incompatible reviewed Journey shape.</summary>
    [Fact]
    public void Find_MismatchedStopCount_ReturnsNoSuggestion()
    {
        var result = AdventureTemplateTravelStopSuggestionSelector.Find(
            [Suggestion], "Phoenix AZ", 1300, 450, 3);

        Assert.Null(result);
    }
}
