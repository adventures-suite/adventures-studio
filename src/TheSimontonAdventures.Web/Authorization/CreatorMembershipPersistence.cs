using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Authorization;

/// <summary>Creates Creator-scoped transactions for membership state and required audit evidence.</summary>
public interface ICreatorMembershipTransactionFactory
{
    /// <summary>Begins one transaction whose every operation is bound to one Creator.</summary>
    Task<ICreatorMembershipTransaction> BeginAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default);
}

/// <summary>Coordinates membership changes and their required audit records atomically.</summary>
public interface ICreatorMembershipTransaction : IAsyncDisposable
{
    /// <summary>Gets the immutable Creator scope of this transaction.</summary>
    CreatorId CreatorId { get; }

    /// <summary>Gets membership operations constrained to <see cref="CreatorId"/>.</summary>
    ICreatorMembershipRepository Memberships { get; }

    /// <summary>Commits all membership and audit changes.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);
}

/// <summary>Persists Creator memberships without accepting ambient or optional Creator scope.</summary>
public interface ICreatorMembershipRepository : ICreatorMembershipProvider
{
    /// <summary>Gets one membership by its Creator-scoped stable identity.</summary>
    Task<CreatorMembershipSnapshot?> GetByIdAsync(
        CreatorMembershipId membershipId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a membership and its required successful audit event atomically.</summary>
    Task AddAsync(
        CreatorMembershipSnapshot membership,
        AuditEventIntent auditEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a membership only when its stored version matches
    /// <paramref name="expectedVersion"/>, while preserving last-owner safety.
    /// </summary>
    Task UpdateAsync(
        CreatorMembershipSnapshot membership,
        long expectedVersion,
        AuditEventIntent auditEvent,
        CancellationToken cancellationToken = default);
}

/// <summary>Signals that a membership changed after the caller observed it.</summary>
public sealed class CreatorMembershipConcurrencyException : Exception
{
    /// <summary>Initializes a safe concurrency failure without protected membership details.</summary>
    public CreatorMembershipConcurrencyException() : base("The Creator membership changed before the operation completed.")
    {
    }
}

/// <summary>Signals that a mutation would leave a Creator without an active owner.</summary>
public sealed class LastCreatorOwnerException : Exception
{
    /// <summary>Initializes a safe last-owner failure.</summary>
    public LastCreatorOwnerException() : base("The Creator must retain at least one active owner.")
    {
    }
}
