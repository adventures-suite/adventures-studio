using AdventuresSuite.Companion.Mobile.Services;

namespace AdventuresSuite.Companion.Mobile.Tests;

public sealed class CompanionAppearanceServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unsupported")]
    public void MissingOrInvalidPreferenceFallsBackToCurrentSystemPalette(string? storedValue)
    {
        var store = new FakeStore(storedValue);
        var system = new FakeSystemAppearance(CompanionPalette.Dark);

        using var service = new CompanionAppearanceService(store, system);

        Assert.Equal(CompanionAppearancePreference.System, service.Preference);
        Assert.Equal(CompanionPalette.Dark, service.EffectivePalette);
        Assert.Empty(store.Writes);
    }

    [Fact]
    public void ExplicitPreferencePersistsAndIgnoresLaterSystemChanges()
    {
        var store = new FakeStore(null);
        var system = new FakeSystemAppearance(CompanionPalette.Light);
        using var service = new CompanionAppearanceService(store, system);
        var changes = 0;
        service.Changed += (_, _) => changes++;

        service.SetPreference(CompanionAppearancePreference.Dark);
        system.ChangeTo(CompanionPalette.Dark);
        system.ChangeTo(CompanionPalette.Light);

        Assert.Equal(CompanionAppearancePreference.Dark, service.Preference);
        Assert.Equal(CompanionPalette.Dark, service.EffectivePalette);
        Assert.Equal(["Dark"], store.Writes);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void SystemPreferenceTracksOnlyEffectiveLiveChanges()
    {
        var system = new FakeSystemAppearance(CompanionPalette.Light);
        using var service = new CompanionAppearanceService(new FakeStore("System"), system);
        var changes = 0;
        service.Changed += (_, _) => changes++;

        system.ChangeTo(CompanionPalette.Dark);
        system.ChangeTo(CompanionPalette.Dark);

        Assert.Equal(CompanionPalette.Dark, service.EffectivePalette);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void ReturningToSystemImmediatelyAdoptsCurrentPaletteAndPersists()
    {
        var store = new FakeStore("Light");
        var system = new FakeSystemAppearance(CompanionPalette.Dark);
        using var service = new CompanionAppearanceService(store, system);

        service.SetPreference(CompanionAppearancePreference.System);

        Assert.Equal(CompanionAppearancePreference.System, service.Preference);
        Assert.Equal(CompanionPalette.Dark, service.EffectivePalette);
        Assert.Equal(["System"], store.Writes);
    }

    [Fact]
    public void UnsupportedProgrammaticPreferenceFailsSafelyToSystem()
    {
        var store = new FakeStore("Dark");
        using var service = new CompanionAppearanceService(store, new FakeSystemAppearance(CompanionPalette.Light));

        service.SetPreference((CompanionAppearancePreference)999);

        Assert.Equal(CompanionAppearancePreference.System, service.Preference);
        Assert.Equal(CompanionPalette.Light, service.EffectivePalette);
        Assert.Equal(["System"], store.Writes);
    }

    [Fact]
    public void DisposalStopsLiveSystemObservation()
    {
        var system = new FakeSystemAppearance(CompanionPalette.Light);
        var service = new CompanionAppearanceService(new FakeStore("System"), system);
        service.Dispose();

        system.ChangeTo(CompanionPalette.Dark);

        Assert.Equal(CompanionPalette.Light, service.EffectivePalette);
    }

    private sealed class FakeStore(string? value) : IAppearancePreferenceStore
    {
        public List<string> Writes { get; } = [];

        public string? Get() => value;

        public void Set(string storedValue) => Writes.Add(storedValue);
    }

    private sealed class FakeSystemAppearance(CompanionPalette palette) : ISystemAppearanceSource
    {
        public event EventHandler? Changed;

        public CompanionPalette CurrentPalette { get; private set; } = palette;

        public void ChangeTo(CompanionPalette next)
        {
            CurrentPalette = next;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
