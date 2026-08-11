namespace AdventuresSuite.Companion.Poc.Services;

/// <summary>Identifies the traveler's persisted Companion appearance preference.</summary>
public enum CompanionAppearancePreference
{
    /// <summary>Follows the current operating-system appearance.</summary>
    System,

    /// <summary>Always uses the light palette.</summary>
    Light,

    /// <summary>Always uses the dark palette.</summary>
    Dark
}

/// <summary>Identifies the palette currently rendered by Companion.</summary>
public enum CompanionPalette
{
    /// <summary>The light semantic-token palette.</summary>
    Light,

    /// <summary>The dark semantic-token palette.</summary>
    Dark
}

/// <summary>Owns the local appearance preference and effective Companion palette.</summary>
public interface ICompanionAppearanceService
{
    /// <summary>Raised after the preference or effective palette changes.</summary>
    event EventHandler? Changed;

    /// <summary>Gets the traveler's persisted preference.</summary>
    CompanionAppearancePreference Preference { get; }

    /// <summary>Gets the currently effective light or dark palette.</summary>
    CompanionPalette EffectivePalette { get; }

    /// <summary>Changes and persists the traveler's preference.</summary>
    /// <param name="preference">The new appearance preference.</param>
    void SetPreference(CompanionAppearancePreference preference);
}

/// <summary>Persists ordinary, non-sensitive presentation preferences.</summary>
public interface IAppearancePreferenceStore
{
    /// <summary>Reads the stored appearance value, if present.</summary>
    string? Get();

    /// <summary>Stores the appearance value.</summary>
    /// <param name="value">The validated preference name.</param>
    void Set(string value);
}

/// <summary>Reports live operating-system appearance changes.</summary>
public interface ISystemAppearanceSource
{
    /// <summary>Raised when the operating-system palette changes.</summary>
    event EventHandler? Changed;

    /// <summary>Gets the current operating-system palette.</summary>
    CompanionPalette CurrentPalette { get; }
}

/// <summary>Provides the Companion appearance state used by shared and native surfaces.</summary>
public sealed class CompanionAppearanceService : ICompanionAppearanceService, IDisposable
{
    private readonly IAppearancePreferenceStore _store;
    private readonly ISystemAppearanceSource _systemAppearance;

    /// <summary>Initializes the service from the validated local preference.</summary>
    public CompanionAppearanceService(IAppearancePreferenceStore store, ISystemAppearanceSource systemAppearance)
    {
        _store = store;
        _systemAppearance = systemAppearance;
        Preference = ParsePreference(store.Get());
        EffectivePalette = ResolvePalette(Preference);
        _systemAppearance.Changed += OnSystemAppearanceChanged;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public CompanionAppearancePreference Preference { get; private set; }

    /// <inheritdoc />
    public CompanionPalette EffectivePalette { get; private set; }

    /// <inheritdoc />
    public void SetPreference(CompanionAppearancePreference preference)
    {
        if (!Enum.IsDefined(preference))
        {
            preference = CompanionAppearancePreference.System;
        }

        var palette = ResolvePalette(preference);
        if (Preference == preference && EffectivePalette == palette)
        {
            return;
        }

        Preference = preference;
        EffectivePalette = palette;
        _store.Set(preference.ToString());
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Dispose() => _systemAppearance.Changed -= OnSystemAppearanceChanged;

    private static CompanionAppearancePreference ParsePreference(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out CompanionAppearancePreference preference) && Enum.IsDefined(preference)
            ? preference
            : CompanionAppearancePreference.System;

    private CompanionPalette ResolvePalette(CompanionAppearancePreference preference) => preference switch
    {
        CompanionAppearancePreference.Light => CompanionPalette.Light,
        CompanionAppearancePreference.Dark => CompanionPalette.Dark,
        _ => _systemAppearance.CurrentPalette
    };

    private void OnSystemAppearanceChanged(object? sender, EventArgs args)
    {
        if (Preference != CompanionAppearancePreference.System)
        {
            return;
        }

        var palette = _systemAppearance.CurrentPalette;
        if (palette == EffectivePalette)
        {
            return;
        }

        EffectivePalette = palette;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
