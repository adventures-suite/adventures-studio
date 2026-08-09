using System.Text.Json;
using AdventuresSuite.Companion.Poc.Models;

namespace AdventuresSuite.Companion.Poc.Services;

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
    /// Loads the POC Italy Playbook projection.
    /// </summary>
    public async Task<CompanionPlaybook> LoadItalyAsync()
    {
        await using var stream = typeof(PlaybookContentService).Assembly.GetManifestResourceStream(
            "AdventuresSuite.Companion.Poc.Data.playbook-italy.json")
            ?? throw new InvalidOperationException("The Italy Playbook POC data is missing.");
        return await JsonSerializer.DeserializeAsync<CompanionPlaybook>(stream, JsonOptions)
            ?? throw new InvalidOperationException("The Italy Playbook POC data is invalid.");
    }
}
