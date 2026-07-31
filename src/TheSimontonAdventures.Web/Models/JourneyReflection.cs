namespace TheSimontonAdventures.Web.Models;

public sealed class JourneyReflection
{
    public int AfterSection { get; init; }

    public string Author { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string Signature { get; init; } = string.Empty;
}