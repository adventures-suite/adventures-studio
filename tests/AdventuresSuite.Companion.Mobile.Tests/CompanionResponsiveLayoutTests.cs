namespace AdventuresSuite.Companion.Mobile.Tests;

public sealed class CompanionResponsiveLayoutTests
{
    [Fact]
    public async Task TabletLayoutExpandsBeyondPhoneWidthAndBoundsReadableContent()
    {
        var css = await LoadStylesAsync();

        Assert.Contains("@media (min-width: 700px)", css, StringComparison.Ordinal);
        Assert.Contains("max-width: 1120px", css, StringComparison.Ordinal);
        Assert.Contains("width: min(100%, 960px)", css, StringComparison.Ordinal);
        Assert.Contains("width: min(720px, calc(100% - 48px))", css, StringComparison.Ordinal);
        Assert.DoesNotContain("@media (min-width: 700px) {\n    body { padding: 24px", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LandscapeLayoutUsesAdaptiveColumnsWithoutChangingPhoneLayout()
    {
        var css = await LoadStylesAsync();

        Assert.Contains("@media (min-width: 900px) and (min-height: 600px)", css, StringComparison.Ordinal);
        Assert.Contains(".home-overview { display: grid", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: minmax(0, 1.45fr) minmax(280px, .75fr)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 430px)", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NativePageKeepsInteractiveHeaderOutsideSystemBars()
    {
        var page = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Presentation", "MainPage.xaml"));

        Assert.Contains("SafeAreaEdges=\"Container\"", page, StringComparison.Ordinal);
    }

    private static Task<string> LoadStylesAsync() =>
        File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Presentation", "app.css"));
}
