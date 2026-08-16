using System.Text.Json;
using AdventuresSuite.Companion.Mobile.Models;

namespace AdventuresSuite.Companion.Mobile.Services;

/// <summary>
/// Loads the privacy-minimized Italy Playbook fixture used for customer discovery.
/// </summary>
public sealed class PlaybookContentService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Loads the fictional Italy Playbook projection used by Demo mode.
    /// </summary>
    public async Task<CompanionPlaybook> LoadItalyAsync()
    {
        await using var stream = typeof(PlaybookContentService).Assembly.GetManifestResourceStream(
            "AdventuresSuite.Companion.Mobile.Data.playbook-italy.json")
            ?? throw new InvalidOperationException("The Italy Playbook demo data is missing.");
        return await JsonSerializer.DeserializeAsync<CompanionPlaybook>(stream, JsonOptions)
            ?? throw new InvalidOperationException("The Italy Playbook demo data is invalid.");
    }
}
