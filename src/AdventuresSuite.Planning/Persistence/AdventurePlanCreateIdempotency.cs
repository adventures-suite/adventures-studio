using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning.Persistence;

/// <summary>Defines the only Planning operation currently eligible for durable creation idempotency.</summary>
public static class PlanningIdempotencyOperations
{
    /// <summary>Identifies version one of manual Adventure Plan creation.</summary>
    public const string AdventurePlanCreateV1 = "AdventurePlan.Create.v1";
}

/// <summary>Identifies one retryable Planning request within a Creator and operation.</summary>
public readonly record struct PlanningIdempotencyKey
{
    /// <summary>Initializes an opaque, case-sensitive idempotency key.</summary>
    public PlanningIdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value != value.Trim()
            || value.Length is < 16 or > 128)
        {
            throw new ArgumentException(
                "An idempotency key must contain 16-128 non-whitespace characters.",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>Gets the opaque, case-sensitive key value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Contains a versioned SHA-256 fingerprint without retaining request content.</summary>
public sealed class PlanningRequestFingerprint
{
    private readonly byte[] value;

    /// <summary>Initializes a versioned SHA-256 fingerprint.</summary>
    public PlanningRequestFingerprint(int version, ReadOnlySpan<byte> value)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Fingerprint version must be positive.");
        }

        if (value.Length != 32)
        {
            throw new ArgumentException("A SHA-256 fingerprint must contain exactly 32 bytes.", nameof(value));
        }

        Version = version;
        this.value = value.ToArray();
    }

    /// <summary>Gets the canonical fingerprint schema version.</summary>
    public int Version { get; }

    /// <summary>Gets a defensive copy of the SHA-256 fingerprint.</summary>
    public byte[] ToArray() => value.ToArray();
}

/// <summary>Requests one durable reservation for a version-one Adventure Plan result.</summary>
public sealed record AdventurePlanCreateReservation
{
    /// <summary>Initializes and validates the allowlisted creation result.</summary>
    public AdventurePlanCreateReservation(
        string operation,
        PlanningIdempotencyKey idempotencyKey,
        PlanningRequestFingerprint fingerprint,
        AdventurePlanId adventurePlanId,
        long resultingVersion,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (!string.Equals(
                operation,
                PlanningIdempotencyOperations.AdventurePlanCreateV1,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("The Planning idempotency operation is not allowlisted.", nameof(operation));
        }

        if (idempotencyKey == default)
        {
            throw new ArgumentException("A valid idempotency key is required.", nameof(idempotencyKey));
        }

        ArgumentNullException.ThrowIfNull(fingerprint);
        if (adventurePlanId == default)
        {
            throw new ArgumentException("A valid Adventure Plan identity is required.", nameof(adventurePlanId));
        }

        if (resultingVersion != 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resultingVersion),
                "Adventure Plan creation must produce version one.");
        }

        if (createdAtUtc.Offset != TimeSpan.Zero
            || expiresAtUtc.Offset != TimeSpan.Zero
            || expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentException("Idempotency timestamps must be increasing UTC values.");
        }

        Operation = operation;
        IdempotencyKey = idempotencyKey;
        Fingerprint = fingerprint;
        AdventurePlanId = adventurePlanId;
        ResultingVersion = resultingVersion;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Gets the allowlisted operation identity.</summary>
    public string Operation { get; }
    /// <summary>Gets the opaque retry key.</summary>
    public PlanningIdempotencyKey IdempotencyKey { get; }
    /// <summary>Gets the versioned request fingerprint.</summary>
    public PlanningRequestFingerprint Fingerprint { get; }
    /// <summary>Gets the pre-generated Adventure Plan result identity.</summary>
    public AdventurePlanId AdventurePlanId { get; }
    /// <summary>Gets the required initial plan version.</summary>
    public long ResultingVersion { get; }
    /// <summary>Gets when the durable result was created.</summary>
    public DateTimeOffset CreatedAtUtc { get; }
    /// <summary>Gets when retention cleanup may remove the result.</summary>
    public DateTimeOffset ExpiresAtUtc { get; }
}

/// <summary>Describes how a transaction resolved an Adventure Plan creation key.</summary>
public enum AdventurePlanCreateIdempotencyOutcome
{
    /// <summary>The transaction reserved a new result and must create and audit it.</summary>
    Reserved,
    /// <summary>The same request already committed and its original result is returned.</summary>
    Replay,
    /// <summary>The key already belongs to a request with a different fingerprint.</summary>
    Conflict
}

/// <summary>Returns the durable result associated with an Adventure Plan creation key.</summary>
public sealed record AdventurePlanCreateIdempotencyResult(
    AdventurePlanCreateIdempotencyOutcome Outcome,
    AdventurePlanId? AdventurePlanId,
    long? ResultingVersion);

/// <summary>Reserves and resolves Creator-scoped Adventure Plan creation results.</summary>
public interface IAdventurePlanCreateIdempotencyStore
{
    /// <summary>
    /// Resolves a key only within the explicit transaction Creator boundary.
    /// Authorization must already have succeeded at the application boundary.
    /// </summary>
    Task<AdventurePlanCreateIdempotencyResult> ReserveAsync(
        CreatorId creatorId,
        AdventurePlanCreateReservation reservation,
        CancellationToken cancellationToken = default);
}
