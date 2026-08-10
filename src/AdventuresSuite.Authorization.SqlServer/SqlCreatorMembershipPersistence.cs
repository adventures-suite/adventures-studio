using Dapper;
using AdventuresSuite.Identity;
using Microsoft.Data.SqlClient;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace AdventuresSuite.Authorization.SqlServer;

/// <summary>Creates Creator-scoped SQL transactions for membership persistence.</summary>
public sealed class SqlCreatorMembershipTransactionFactory : ICreatorMembershipTransactionFactory
{
    private readonly string connectionString;

    /// <summary>Initializes the factory with an explicit Azure SQL connection string.</summary>
    public SqlCreatorMembershipTransactionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A membership database connection string is required.", nameof(connectionString));
        }

        this.connectionString = connectionString;
    }

    /// <inheritdoc />
    public async Task<ICreatorMembershipTransaction> BeginAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default)
    {
        if (creatorId == default)
        {
            throw new ArgumentException("A Creator identity is required.", nameof(creatorId));
        }

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        try
        {
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            return new SqlCreatorMembershipTransaction(creatorId, connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}

internal sealed class SqlCreatorMembershipTransaction : ICreatorMembershipTransaction
{
    private readonly SqlConnection connection;
    private readonly SqlTransaction transaction;
    private bool completed;

    public SqlCreatorMembershipTransaction(
        CreatorId creatorId,
        SqlConnection connection,
        SqlTransaction transaction)
    {
        CreatorId = creatorId;
        this.connection = connection;
        this.transaction = transaction;
        Memberships = new DapperCreatorMembershipRepository(creatorId, connection, transaction);
    }

    /// <inheritdoc />
    public CreatorId CreatorId { get; }

    /// <inheritdoc />
    public ICreatorMembershipRepository Memberships { get; }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(completed, this);
        await transaction.CommitAsync(cancellationToken);
        completed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!completed)
            {
                await transaction.RollbackAsync();
            }
        }
        finally
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                await connection.DisposeAsync();
                completed = true;
            }
        }
    }
}

internal sealed class DapperCreatorMembershipRepository(
    CreatorId transactionCreatorId,
    SqlConnection connection,
    SqlTransaction transaction) : ICreatorMembershipRepository
{
    /// <inheritdoc />
    public Task<CreatorMembershipSnapshot?> GetMembershipAsync(
        UserId userId,
        CreatorId creatorId,
        CancellationToken cancellationToken = default)
    {
        RequireCreator(creatorId);
        if (userId == default)
        {
            throw new ArgumentException("A user identity is required.", nameof(userId));
        }

        return LoadAsync("memberships.UserId=@UserId", new { UserId = userId.Value }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CreatorMembershipSnapshot?> GetByIdAsync(
        CreatorMembershipId membershipId,
        CancellationToken cancellationToken = default)
    {
        if (membershipId == default)
        {
            throw new ArgumentException("A membership identity is required.", nameof(membershipId));
        }

        return LoadAsync(
            "memberships.CreatorMembershipId=@MembershipId",
            new { MembershipId = membershipId.Value },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(
        CreatorMembershipSnapshot membership,
        AuditEventIntent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ValidateMutation(membership, auditEvent, expectedVersion: null);
        await AcquireCreatorLockAsync(cancellationToken);
        await RequireOwnerContinuityAsync(membership, auditEvent.OccurredAtUtc, isNew: true, cancellationToken);

        var actorUserId = auditEvent.Actor.UserId!.Value.Value;
        await connection.ExecuteAsync(Command("""
            INSERT auth.CreatorMemberships
              (CreatorId,CreatorMembershipId,UserId,Status,Version,EffectiveFromUtc,ExpiresAtUtc,
               CreatedAtUtc,UpdatedAtUtc,CreatedByUserId,UpdatedByUserId)
            VALUES
              (@CreatorId,@MembershipId,@UserId,@Status,@Version,@EffectiveFromUtc,@ExpiresAtUtc,
               @OccurredAtUtc,@OccurredAtUtc,@ActorUserId,@ActorUserId);
            """, new
        {
            CreatorId = transactionCreatorId.Value,
            MembershipId = membership.Id.Value,
            UserId = membership.UserId.Value,
            Status = membership.Status.ToString(),
            membership.Version,
            membership.EffectiveFromUtc,
            membership.ExpiresAtUtc,
            auditEvent.OccurredAtUtc,
            ActorUserId = actorUserId
        }, cancellationToken));
        await ReplaceAssignmentsAsync(membership, cancellationToken);
        await AppendAuditAsync(auditEvent, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(
        CreatorMembershipSnapshot membership,
        long expectedVersion,
        AuditEventIntent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ValidateMutation(membership, auditEvent, expectedVersion);
        await AcquireCreatorLockAsync(cancellationToken);
        await RequireOwnerContinuityAsync(membership, auditEvent.OccurredAtUtc, isNew: false, cancellationToken);

        var count = await connection.ExecuteAsync(Command("""
            UPDATE auth.CreatorMemberships
            SET Status=@Status,Version=@Version,EffectiveFromUtc=@EffectiveFromUtc,ExpiresAtUtc=@ExpiresAtUtc,
                UpdatedAtUtc=@OccurredAtUtc,UpdatedByUserId=@ActorUserId
            WHERE CreatorId=@CreatorId AND CreatorMembershipId=@MembershipId
              AND UserId=@UserId AND Version=@ExpectedVersion;
            """, new
        {
            CreatorId = transactionCreatorId.Value,
            MembershipId = membership.Id.Value,
            UserId = membership.UserId.Value,
            Status = membership.Status.ToString(),
            membership.Version,
            membership.EffectiveFromUtc,
            membership.ExpiresAtUtc,
            auditEvent.OccurredAtUtc,
            ActorUserId = auditEvent.Actor.UserId!.Value.Value,
            ExpectedVersion = expectedVersion
        }, cancellationToken));
        if (count != 1)
        {
            throw new CreatorMembershipConcurrencyException();
        }

        await ReplaceAssignmentsAsync(membership, cancellationToken);
        await AppendAuditAsync(auditEvent, cancellationToken);
    }

    private async Task<CreatorMembershipSnapshot?> LoadAsync(
        string predicate,
        object parameters,
        CancellationToken cancellationToken)
    {
        var values = new DynamicParameters(parameters);
        values.Add("CreatorId", transactionCreatorId.Value);
        using var results = await connection.QueryMultipleAsync(Command($"""
            SELECT memberships.CreatorMembershipId,memberships.UserId,memberships.CreatorId,
                   memberships.Status,memberships.Version,memberships.EffectiveFromUtc,memberships.ExpiresAtUtc
            FROM auth.CreatorMemberships AS memberships
            WHERE memberships.CreatorId=@CreatorId AND {predicate};
            SELECT roles.Role
            FROM auth.CreatorMembershipRoles AS roles
            JOIN auth.CreatorMemberships AS memberships
              ON memberships.CreatorId=roles.CreatorId
             AND memberships.CreatorMembershipId=roles.CreatorMembershipId
            WHERE memberships.CreatorId=@CreatorId AND {predicate}
            ORDER BY roles.Role;
            SELECT grants.Permission
            FROM auth.CreatorMembershipPermissionGrants AS grants
            JOIN auth.CreatorMemberships AS memberships
              ON memberships.CreatorId=grants.CreatorId
             AND memberships.CreatorMembershipId=grants.CreatorMembershipId
            WHERE memberships.CreatorId=@CreatorId AND {predicate}
            ORDER BY grants.Permission;
            """, values, cancellationToken));
        var row = await results.ReadSingleOrDefaultAsync<MembershipRow>();
        var roles = (await results.ReadAsync<string>()).ToArray();
        var grants = (await results.ReadAsync<string>()).ToArray();
        return row?.Map(roles, grants);
    }

    private async Task ReplaceAssignmentsAsync(
        CreatorMembershipSnapshot membership,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(Command("""
            DELETE auth.CreatorMembershipRoles
            WHERE CreatorId=@CreatorId AND CreatorMembershipId=@MembershipId;
            DELETE auth.CreatorMembershipPermissionGrants
            WHERE CreatorId=@CreatorId AND CreatorMembershipId=@MembershipId;
            """, new
        {
            CreatorId = transactionCreatorId.Value,
            MembershipId = membership.Id.Value
        }, cancellationToken));

        foreach (var role in membership.Roles.OrderBy(value => value.ToString(), StringComparer.Ordinal))
        {
            await connection.ExecuteAsync(Command("""
                INSERT auth.CreatorMembershipRoles (CreatorId,CreatorMembershipId,Role)
                VALUES (@CreatorId,@MembershipId,@Role);
                """, new
            {
                CreatorId = transactionCreatorId.Value,
                MembershipId = membership.Id.Value,
                Role = role.ToString()
            }, cancellationToken));
        }

        foreach (var permission in membership.PermissionGrants.OrderBy(value => value.Value, StringComparer.Ordinal))
        {
            await connection.ExecuteAsync(Command("""
                INSERT auth.CreatorMembershipPermissionGrants (CreatorId,CreatorMembershipId,Permission)
                VALUES (@CreatorId,@MembershipId,@Permission);
                """, new
            {
                CreatorId = transactionCreatorId.Value,
                MembershipId = membership.Id.Value,
                Permission = permission.Value
            }, cancellationToken));
        }
    }

    private async Task AppendAuditAsync(AuditEventIntent auditEvent, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(Command("""
            INSERT audit.AuditEvents
              (AuditEventId,CreatorId,ActorType,ActorUserId,Permission,ResourceType,ResourceId,
               Outcome,ReasonCategory,OccurredAtUtc,CorrelationId,PreviousVersion,ResultingVersion)
            VALUES
              (@AuditEventId,@CreatorId,@ActorType,@ActorUserId,@Permission,@ResourceType,@ResourceId,
               @Outcome,@ReasonCategory,@OccurredAtUtc,@CorrelationId,@PreviousVersion,@ResultingVersion);
            """, new
        {
            AuditEventId = auditEvent.Id.Value,
            CreatorId = auditEvent.CreatorId.Value,
            ActorType = auditEvent.Actor.Type.ToString(),
            ActorUserId = auditEvent.Actor.UserId?.Value,
            Permission = auditEvent.Permission.Value,
            ResourceType = auditEvent.Resource.ResourceType.Value,
            auditEvent.Resource.ResourceId,
            Outcome = auditEvent.Outcome.ToString(),
            ReasonCategory = auditEvent.ReasonCategory.ToString(),
            auditEvent.OccurredAtUtc,
            CorrelationId = auditEvent.CorrelationId.Value,
            auditEvent.PreviousVersion,
            auditEvent.ResultingVersion
        }, cancellationToken));
    }

    private async Task AcquireCreatorLockAsync(CancellationToken cancellationToken)
    {
        var result = await connection.ExecuteScalarAsync<int>(Command("""
            DECLARE @Result int;
            EXEC @Result=sys.sp_getapplock @Resource=@Resource,@LockMode='Exclusive',
                @LockOwner='Transaction',@LockTimeout=15000;
            SELECT @Result;
            """, new { Resource = $"membership:{transactionCreatorId.Value}" }, cancellationToken));
        if (result < 0)
        {
            throw new InvalidOperationException("The Creator membership lock was unavailable.");
        }
    }

    private async Task RequireOwnerContinuityAsync(
        CreatorMembershipSnapshot proposed,
        DateTimeOffset utcNow,
        bool isNew,
        CancellationToken cancellationToken)
    {
        var proposedIsOwner = proposed.Status == CreatorMembershipStatus.Active
            && proposed.EffectiveFromUtc <= utcNow
            && (!proposed.ExpiresAtUtc.HasValue || utcNow < proposed.ExpiresAtUtc.Value)
            && proposed.Roles.Contains(CreatorRole.Owner);
        if (proposed.Roles.Contains(CreatorRole.Owner) && proposed.ExpiresAtUtc.HasValue)
        {
            throw new LastCreatorOwnerException();
        }

        var existingOwnerCount = await connection.ExecuteScalarAsync<int>(Command("""
            SELECT COUNT(DISTINCT memberships.CreatorMembershipId)
            FROM auth.CreatorMemberships AS memberships WITH (UPDLOCK,HOLDLOCK)
            JOIN auth.CreatorMembershipRoles AS roles
              ON roles.CreatorId=memberships.CreatorId
             AND roles.CreatorMembershipId=memberships.CreatorMembershipId
             AND roles.Role='Owner'
            WHERE memberships.CreatorId=@CreatorId
              AND memberships.Status='Active'
              AND memberships.EffectiveFromUtc<=@UtcNow
              AND (memberships.ExpiresAtUtc IS NULL OR @UtcNow<memberships.ExpiresAtUtc)
              AND (@IsNew=1 OR memberships.CreatorMembershipId<>@MembershipId);
            """, new
        {
            CreatorId = transactionCreatorId.Value,
            UtcNow = utcNow,
            IsNew = isNew,
            MembershipId = proposed.Id.Value
        }, cancellationToken));
        if (!proposedIsOwner && existingOwnerCount == 0)
        {
            throw new LastCreatorOwnerException();
        }
    }

    private void ValidateMutation(
        CreatorMembershipSnapshot membership,
        AuditEventIntent auditEvent,
        long? expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(membership);
        ArgumentNullException.ThrowIfNull(auditEvent);
        RequireCreator(membership.CreatorId);
        if (!auditEvent.Actor.IsHuman || !auditEvent.Actor.UserId.HasValue
            || auditEvent.CreatorId != transactionCreatorId
            || auditEvent.Permission != Permissions.CreatorManageMembers
            || auditEvent.Resource.ResourceType != AuthorizationResourceTypes.Creator
            || auditEvent.Resource.ResourceId != membership.Id.Value
            || auditEvent.Outcome != AuditOutcome.Succeeded
            || auditEvent.ReasonCategory != AuditReasonCategory.Completed
            || auditEvent.ResultingVersion != membership.Version
            || auditEvent.PreviousVersion != expectedVersion
            || (expectedVersion.HasValue && membership.Version != expectedVersion.Value + 1)
            || (!expectedVersion.HasValue && membership.Version != 1))
        {
            throw new ArgumentException("Membership mutation audit intent does not match the protected change.", nameof(auditEvent));
        }
    }

    private void RequireCreator(CreatorId creatorId)
    {
        if (creatorId == default || creatorId != transactionCreatorId)
        {
            throw new ArgumentException("The membership Creator must match the transaction scope.", nameof(creatorId));
        }
    }

    private CommandDefinition Command(
        string sql,
        object? parameters,
        CancellationToken cancellationToken) =>
        new(sql, parameters, transaction, cancellationToken: cancellationToken);

    private sealed record MembershipRow(
        string CreatorMembershipId,
        string UserId,
        string CreatorId,
        string Status,
        long Version,
        DateTimeOffset EffectiveFromUtc,
        DateTimeOffset? ExpiresAtUtc)
    {
        public CreatorMembershipSnapshot Map(string[] roles, string[] grants) => new(
            new(CreatorMembershipId),
            new(UserId),
            new(CreatorId),
            Enum.Parse<CreatorMembershipStatus>(Status),
            roles.Select(Enum.Parse<CreatorRole>),
            grants.Select(value => new Permission(value)),
            Version,
            EffectiveFromUtc.ToUniversalTime(),
            ExpiresAtUtc?.ToUniversalTime());
    }
}
