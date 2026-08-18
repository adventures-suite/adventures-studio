namespace AdventuresSuite.Companion.Mobile.Tests;

public sealed class CompanionPackageIdentityTests
{
    [Fact]
    public async Task AndroidUsesOwnedAdventuresSuitePackageIdentityWithoutChangingAppleIdentity()
    {
        var project = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Presentation", "AdventuresSuite.Companion.Mobile.csproj"));

        Assert.Contains("<ApplicationId>com.adventuresstudio.companion</ApplicationId>", project, StringComparison.Ordinal);
        Assert.Contains(
            "'$(TargetFramework)')) == 'android'\">com.adventuressuite.companion</ApplicationId>",
            project,
            StringComparison.Ordinal);
    }
}
