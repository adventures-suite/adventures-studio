namespace TheSimontonAdventures.Web.Models;

/// <summary>Describes the editorial and publication lifecycle of a volume.</summary>
public enum VolumeStatus
{
    /// <summary>The volume is private and still being authored.</summary>
    Draft,
    /// <summary>The adventure is planned but travel has not started.</summary>
    Planned,
    /// <summary>The adventure is approaching and may be publicly previewed.</summary>
    Upcoming,
    /// <summary>The volume represents the currently featured adventure.</summary>
    Current,
    /// <summary>The completed volume is available as published content.</summary>
    Published
}
