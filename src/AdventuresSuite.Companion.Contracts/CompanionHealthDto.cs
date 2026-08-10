namespace AdventuresSuite.Companion.Contracts;

/// <summary>Provides the minimal public health and release identity for the Companion API host.</summary>
public sealed record CompanionHealthDto
{
    /// <summary>Gets the bounded health status.</summary>
    public required string Status { get; init; }

    /// <summary>Gets the stable service identity.</summary>
    public required string Service { get; init; }

    /// <summary>Gets the exact immutable release commit SHA.</summary>
    public required string ReleaseSha { get; init; }

    /// <summary>Gets the bounded product activation state.</summary>
    public required string ActivationState { get; init; }
}
