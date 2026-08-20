using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.DatabaseMigrator;

/// <summary>Runs the finite SQL-administrator operations through GitHub OIDC and Azure CLI.</summary>
internal static class SqlAdministratorOperationRunner
{
    private const string SqlScope = "https://database.windows.net/.default";
    private const string DbUpJournalPrefix = "AdventuresSuite.DatabaseMigrator.Database.Migrations.";
    internal const string CompanionPolicyRuntimeRoleName = "AdventuresSuiteCompanionPolicyRuntime";
    internal const string InitialOwnerCreatorId = "creator_tsa_01";
    internal const string InitialOwnerMembershipId = "membership_tsa_initial_owner";
    internal const string InitialOwnerAuditEventId = "audit_tsa_initial_owner";
    private const string CompanionReadRuntimeRoleName = "AdventuresSuiteCompanionReadRuntime";
    private const string CompanionPolicyRoleOperationVersion = "companion-policy-runtime-role-bootstrap-v1";
    private const string InitialOwnerOperationVersion = "development-initial-owner-bootstrap-v1";

    private static readonly string[] ApprovedScripts =
    [
        "0001_create_planning_schema.sql", "0002_create_adventure_plans.sql",
        "0003_create_planning_children.sql", "0004_create_authentication_persistence.sql",
        "0005_bind_sessions_to_external_identities.sql", "0006_create_creator_memberships.sql",
        "0007_create_traveler_participations.sql", "0008_create_companion_read_role.sql",
        "0009_create_adventure_plan_create_results.sql",
        "0010_create_companion_policy_assignments.sql",
        "0011_create_adventure_plan_template_origins.sql",
        "0012_create_planner_footstep_applications.sql"
    ];

    private static readonly string[] At0006PermissionSignatures =
    [
        "AdventuresSuiteAuthenticationRuntime|DENY|ALTER|SCHEMA|[auth]",
        "AdventuresSuiteAuthenticationRuntime|GRANT|INSERT|SCHEMA|[auth]",
        "AdventuresSuiteAuthenticationRuntime|GRANT|SELECT|SCHEMA|[auth]",
        "AdventuresSuiteAuthenticationRuntime|GRANT|UPDATE|SCHEMA|[auth]",
        "AdventuresSuiteMembershipRuntime|DENY|DELETE|OBJECT_OR_COLUMN|[audit].[AuditEvents]",
        "AdventuresSuiteMembershipRuntime|GRANT|INSERT|OBJECT_OR_COLUMN|[audit].[AuditEvents]",
        "AdventuresSuiteMembershipRuntime|GRANT|SELECT|OBJECT_OR_COLUMN|[audit].[AuditEvents]",
        "AdventuresSuiteMembershipRuntime|DENY|UPDATE|OBJECT_OR_COLUMN|[audit].[AuditEvents]",
        "AdventuresSuiteMembershipRuntime|GRANT|DELETE|OBJECT_OR_COLUMN|[auth].[CreatorMembershipPermissionGrants]",
        "AdventuresSuiteMembershipRuntime|GRANT|INSERT|OBJECT_OR_COLUMN|[auth].[CreatorMembershipPermissionGrants]",
        "AdventuresSuiteMembershipRuntime|GRANT|SELECT|OBJECT_OR_COLUMN|[auth].[CreatorMembershipPermissionGrants]",
        "AdventuresSuiteMembershipRuntime|GRANT|DELETE|OBJECT_OR_COLUMN|[auth].[CreatorMembershipRoles]",
        "AdventuresSuiteMembershipRuntime|GRANT|INSERT|OBJECT_OR_COLUMN|[auth].[CreatorMembershipRoles]",
        "AdventuresSuiteMembershipRuntime|GRANT|SELECT|OBJECT_OR_COLUMN|[auth].[CreatorMembershipRoles]",
        "AdventuresSuiteMembershipRuntime|DENY|DELETE|OBJECT_OR_COLUMN|[auth].[CreatorMemberships]",
        "AdventuresSuiteMembershipRuntime|GRANT|INSERT|OBJECT_OR_COLUMN|[auth].[CreatorMemberships]",
        "AdventuresSuiteMembershipRuntime|GRANT|SELECT|OBJECT_OR_COLUMN|[auth].[CreatorMemberships]",
        "AdventuresSuiteMembershipRuntime|GRANT|UPDATE|OBJECT_OR_COLUMN|[auth].[CreatorMemberships]",
        "AdventuresSuiteMembershipRuntime|DENY|ALTER|SCHEMA|[audit]",
        "AdventuresSuiteMembershipRuntime|DENY|ALTER|SCHEMA|[auth]"
    ];

    private static readonly string[] At0006ObjectCountSignatures =
    [
        "audit|USER_TABLE|1",
        "auth|USER_TABLE|6",
        "dbo|USER_TABLE|1",
        "planning|USER_TABLE|13"
    ];

    private static readonly string[] At0009PermissionSignatures =
    [
        "AdventuresSuiteAuthenticationRuntime|DENY|ALTER|SCHEMA|[auth]",
        "AdventuresSuiteAuthenticationRuntime|GRANT|INSERT|SCHEMA|[auth]",
        "AdventuresSuiteAuthenticationRuntime|GRANT|SELECT|SCHEMA|[auth]",
        "AdventuresSuiteAuthenticationRuntime|GRANT|UPDATE|SCHEMA|[auth]",
        "AdventuresSuiteCompanionReadRuntime|DENY|DELETE|OBJECT_OR_COLUMN|[auth].[CreatorMembershipPermissionGrants]",
        "AdventuresSuiteCompanionReadRuntime|DENY|INSERT|OBJECT_OR_COLUMN|[auth].[CreatorMembershipPermissionGrants]",
        "AdventuresSuiteCompanionReadRuntime|GRANT|SELECT|OBJECT_OR_COLUMN|[auth].[CreatorMembershipPermissionGrants]",
        "AdventuresSuiteCompanionReadRuntime|DENY|UPDATE|OBJECT_OR_COLUMN|[auth].[CreatorMembershipPermissionGrants]",
        "AdventuresSuiteCompanionReadRuntime|DENY|DELETE|OBJECT_OR_COLUMN|[auth].[CreatorMembershipRoles]",
        "AdventuresSuiteCompanionReadRuntime|DENY|INSERT|OBJECT_OR_COLUMN|[auth].[CreatorMembershipRoles]",
        "AdventuresSuiteCompanionReadRuntime|GRANT|SELECT|OBJECT_OR_COLUMN|[auth].[CreatorMembershipRoles]",
        "AdventuresSuiteCompanionReadRuntime|DENY|UPDATE|OBJECT_OR_COLUMN|[auth].[CreatorMembershipRoles]",
        "AdventuresSuiteCompanionReadRuntime|DENY|DELETE|OBJECT_OR_COLUMN|[auth].[CreatorMemberships]",
        "AdventuresSuiteCompanionReadRuntime|DENY|INSERT|OBJECT_OR_COLUMN|[auth].[CreatorMemberships]",
        "AdventuresSuiteCompanionReadRuntime|GRANT|SELECT|OBJECT_OR_COLUMN|[auth].[CreatorMemberships]",
        "AdventuresSuiteCompanionReadRuntime|DENY|UPDATE|OBJECT_OR_COLUMN|[auth].[CreatorMemberships]",
        "AdventuresSuiteCompanionReadRuntime|DENY|DELETE|OBJECT_OR_COLUMN|[planning].[AdventurePlans]",
        "AdventuresSuiteCompanionReadRuntime|DENY|INSERT|OBJECT_OR_COLUMN|[planning].[AdventurePlans]",
        "AdventuresSuiteCompanionReadRuntime|GRANT|SELECT|OBJECT_OR_COLUMN|[planning].[AdventurePlans]",
        "AdventuresSuiteCompanionReadRuntime|DENY|UPDATE|OBJECT_OR_COLUMN|[planning].[AdventurePlans]",
        "AdventuresSuiteCompanionReadRuntime|DENY|DELETE|OBJECT_OR_COLUMN|[planning].[DestinationVisits]",
        "AdventuresSuiteCompanionReadRuntime|DENY|INSERT|OBJECT_OR_COLUMN|[planning].[DestinationVisits]",
        "AdventuresSuiteCompanionReadRuntime|GRANT|SELECT|OBJECT_OR_COLUMN|[planning].[DestinationVisits]",
        "AdventuresSuiteCompanionReadRuntime|DENY|UPDATE|OBJECT_OR_COLUMN|[planning].[DestinationVisits]",
        "AdventuresSuiteCompanionReadRuntime|DENY|DELETE|OBJECT_OR_COLUMN|[planning].[TravelerParticipations]",
        "AdventuresSuiteCompanionReadRuntime|DENY|INSERT|OBJECT_OR_COLUMN|[planning].[TravelerParticipations]",
        "AdventuresSuiteCompanionReadRuntime|GRANT|SELECT|OBJECT_OR_COLUMN|[planning].[TravelerParticipations]",
        "AdventuresSuiteCompanionReadRuntime|DENY|UPDATE|OBJECT_OR_COLUMN|[planning].[TravelerParticipations]",
        "AdventuresSuiteCompanionReadRuntime|DENY|ALTER|SCHEMA|[auth]",
        "AdventuresSuiteCompanionReadRuntime|DENY|ALTER|SCHEMA|[planning]",
        "AdventuresSuiteMembershipRuntime|DENY|DELETE|OBJECT_OR_COLUMN|[audit].[AuditEvents]",
        "AdventuresSuiteMembershipRuntime|GRANT|INSERT|OBJECT_OR_COLUMN|[audit].[AuditEvents]",
        "AdventuresSuiteMembershipRuntime|GRANT|SELECT|OBJECT_OR_COLUMN|[audit].[AuditEvents]",
        "AdventuresSuiteMembershipRuntime|DENY|UPDATE|OBJECT_OR_COLUMN|[audit].[AuditEvents]",
        "AdventuresSuiteMembershipRuntime|GRANT|DELETE|OBJECT_OR_COLUMN|[auth].[CreatorMembershipPermissionGrants]",
        "AdventuresSuiteMembershipRuntime|GRANT|INSERT|OBJECT_OR_COLUMN|[auth].[CreatorMembershipPermissionGrants]",
        "AdventuresSuiteMembershipRuntime|GRANT|SELECT|OBJECT_OR_COLUMN|[auth].[CreatorMembershipPermissionGrants]",
        "AdventuresSuiteMembershipRuntime|GRANT|DELETE|OBJECT_OR_COLUMN|[auth].[CreatorMembershipRoles]",
        "AdventuresSuiteMembershipRuntime|GRANT|INSERT|OBJECT_OR_COLUMN|[auth].[CreatorMembershipRoles]",
        "AdventuresSuiteMembershipRuntime|GRANT|SELECT|OBJECT_OR_COLUMN|[auth].[CreatorMembershipRoles]",
        "AdventuresSuiteMembershipRuntime|DENY|DELETE|OBJECT_OR_COLUMN|[auth].[CreatorMemberships]",
        "AdventuresSuiteMembershipRuntime|GRANT|INSERT|OBJECT_OR_COLUMN|[auth].[CreatorMemberships]",
        "AdventuresSuiteMembershipRuntime|GRANT|SELECT|OBJECT_OR_COLUMN|[auth].[CreatorMemberships]",
        "AdventuresSuiteMembershipRuntime|GRANT|UPDATE|OBJECT_OR_COLUMN|[auth].[CreatorMemberships]",
        "AdventuresSuiteMembershipRuntime|DENY|ALTER|SCHEMA|[audit]",
        "AdventuresSuiteMembershipRuntime|DENY|ALTER|SCHEMA|[auth]",
        "AdventuresSuitePlanningRuntime|DENY|DELETE|OBJECT_OR_COLUMN|[planning].[AdventurePlanCreateResults]",
        "AdventuresSuitePlanningRuntime|GRANT|INSERT|OBJECT_OR_COLUMN|[planning].[AdventurePlanCreateResults]",
        "AdventuresSuitePlanningRuntime|GRANT|SELECT|OBJECT_OR_COLUMN|[planning].[AdventurePlanCreateResults]",
        "AdventuresSuitePlanningRuntime|DENY|UPDATE|OBJECT_OR_COLUMN|[planning].[AdventurePlanCreateResults]",
        "AdventuresSuitePlanningRuntime|DENY|ALTER|SCHEMA|[planning]"
    ];

    private static readonly string[] At0009ObjectCountSignatures =
    [
        "audit|USER_TABLE|1",
        "auth|USER_TABLE|6",
        "dbo|USER_TABLE|1",
        "planning|USER_TABLE|15"
    ];

    internal static async Task<int> RunAsync(string operation)
    {
        var context = ReadContext();
        var credential = new AzureCliCredential(new AzureCliCredentialOptions
        {
            TenantId = context.TenantId.ToString()
        });
        AccessToken token = await credential.GetTokenAsync(
            new TokenRequestContext([SqlScope]), CancellationToken.None);
        _ = MigrationIdentityValidator.ValidateSqlWorkloadToken(
            token, context.TenantId, context.AdministratorPrincipalId,
            context.AdministratorClientId, MigrationCredentialMode.GitHubOidcAzureCli);

        if (operation == "denial-proof")
            return await ProveDeniedAsync(context, token);

        await using var connection = new SqlConnection(ConnectionString(context))
        {
            AccessToken = token.Token
        };
        await connection.OpenAsync();
        return operation switch
        {
            "baseline" => await CaptureBaselineAsync(connection, context),
            "bootstrap" => await BootstrapAsync(connection, context),
            "bootstrap-policy-role" => await BootstrapCompanionPolicyRuntimeRoleAsync(
                connection,
                context,
                RequireBoundedIdentifier("ADVENTURESSUITE_ADMIN_SUPPORT_ID"),
                RequireBoundedIdentifier("ADVENTURESSUITE_ADMIN_CORRELATION_ID")),
            "bootstrap-initial-owner" => await BootstrapInitialOwnerAsync(
                connection,
                context,
                RequireAuthorizationIdentity("ADVENTURESSUITE_ADMIN_TARGET_USER_ID"),
                RequireBoundedIdentifier("ADVENTURESSUITE_ADMIN_SUPPORT_ID"),
                RequireBoundedIdentifier("ADVENTURESSUITE_ADMIN_CORRELATION_ID")),
            "cleanup" => await CleanupAsync(connection, context),
            _ => throw new InvalidOperationException("The SQL administrator operation is not approved.")
        };
    }

    /// <summary>
    /// Creates the single initial Owner membership for the fixed development
    /// Creator after proving that no Creator membership already exists.
    /// </summary>
    internal static async Task<int> BootstrapInitialOwnerAsync(
        SqlConnection connection,
        Context context,
        string targetUserId,
        string supportId,
        string correlationId,
        TextWriter? evidenceWriter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);
        targetUserId = RequireAuthorizationIdentity(targetUserId, nameof(targetUserId));
        supportId = RequireBoundedIdentifier(supportId, nameof(supportId));
        correlationId = RequireBoundedIdentifier(correlationId, nameof(correlationId));
        cancellationToken.ThrowIfCancellationRequested();

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var committed = false;
        try
        {
            await using (var lockCommand = new SqlCommand("""
                DECLARE @Result int;
                EXEC @Result = sys.sp_getapplock
                    @Resource = N'AdventuresSuite.SqlAdministrator.InitialOwner.creator_tsa_01.v1',
                    @LockMode = N'Exclusive',
                    @LockOwner = N'Transaction',
                    @LockTimeout = 0;
                IF @Result < 0 THROW 51000, 'The initial-owner administrator lock is unavailable.', 1;
                """, connection, transaction))
            {
                await lockCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var command = new SqlCommand("""
                SET XACT_ABORT ON;
                IF NOT EXISTS (
                    SELECT 1 FROM auth.Users WITH (UPDLOCK,HOLDLOCK)
                    WHERE UserId=@UserId AND Status='Active' AND DisabledAtUtc IS NULL)
                    THROW 51000, 'The target platform user is not active.', 1;
                IF NOT EXISTS (
                    SELECT 1 FROM auth.ExternalIdentities WITH (UPDLOCK,HOLDLOCK)
                    WHERE UserId=@UserId AND DisabledAtUtc IS NULL)
                    THROW 51000, 'The target platform user has no active external identity.', 1;
                IF EXISTS (
                    SELECT 1 FROM auth.CreatorMemberships WITH (UPDLOCK,HOLDLOCK)
                    WHERE CreatorId=@CreatorId)
                    THROW 51000, 'The development Creator already has membership state.', 1;
                IF EXISTS (
                    SELECT 1 FROM audit.AuditEvents WITH (UPDLOCK,HOLDLOCK)
                    WHERE AuditEventId=@AuditEventId)
                    THROW 51000, 'The initial-owner audit identity is already present.', 1;

                DECLARE @Now datetimeoffset(7)=SYSUTCDATETIME();
                INSERT auth.CreatorMemberships
                    (CreatorId,CreatorMembershipId,UserId,Status,Version,EffectiveFromUtc,ExpiresAtUtc,
                     CreatedAtUtc,UpdatedAtUtc,CreatedByUserId,UpdatedByUserId)
                VALUES
                    (@CreatorId,@MembershipId,@UserId,'Active',1,@Now,NULL,
                     @Now,@Now,@UserId,@UserId);
                INSERT auth.CreatorMembershipRoles (CreatorId,CreatorMembershipId,Role)
                    VALUES (@CreatorId,@MembershipId,'Owner');
                INSERT audit.AuditEvents
                    (AuditEventId,CreatorId,ActorType,ActorUserId,Permission,ResourceType,ResourceId,
                     Outcome,ReasonCategory,OccurredAtUtc,CorrelationId,PreviousVersion,ResultingVersion)
                VALUES
                    (@AuditEventId,@CreatorId,'System',NULL,'Creator.ManageMembers','CreatorMembership',
                     @MembershipId,'Succeeded','Completed',@Now,@CorrelationId,NULL,1);
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("@CreatorId", InitialOwnerCreatorId);
                command.Parameters.AddWithValue("@MembershipId", InitialOwnerMembershipId);
                command.Parameters.AddWithValue("@AuditEventId", InitialOwnerAuditEventId);
                command.Parameters.AddWithValue("@UserId", targetUserId);
                command.Parameters.AddWithValue("@CorrelationId", correlationId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            var occurredAtUtc = DateTimeOffset.UtcNow;
            var payload = JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                operation = InitialOwnerOperationVersion,
                outcome = "created",
                creatorId = InitialOwnerCreatorId,
                membershipId = InitialOwnerMembershipId,
                role = "Owner",
                membershipVersion = 1,
                targetUserIdSha256 = Hash(targetUserId),
                auditEventId = InitialOwnerAuditEventId,
                auditActorType = "System",
                supportId,
                correlationId,
                operationId = context.OperationId,
                occurredAtUtc
            });
            if (Encoding.UTF8.GetByteCount(payload) > 4096)
                throw new InvalidOperationException("The initial-owner bootstrap evidence exceeded its size bound.");

            await transaction.CommitAsync(cancellationToken);
            committed = true;
            await (evidenceWriter ?? Console.Out).WriteLineAsync(payload);
            return 0;
        }
        catch
        {
            if (committed)
                throw;
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                throw new InvalidOperationException(
                    "The initial-owner bootstrap failed and transaction rollback was not conclusive.");
            }
            throw;
        }
    }

    /// <summary>
    /// Creates only the fixed, authority-free Companion policy runtime role or
    /// verifies that an exact pre-existing role is already conforming.
    /// </summary>
    internal static async Task<int> BootstrapCompanionPolicyRuntimeRoleAsync(
        SqlConnection connection,
        Context context,
        string supportId,
        string correlationId,
        TextWriter? evidenceWriter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);
        supportId = RequireBoundedIdentifier(supportId, nameof(supportId));
        correlationId = RequireBoundedIdentifier(correlationId, nameof(correlationId));
        cancellationToken.ThrowIfCancellationRequested();

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var committed = false;
        try
        {
            await AcquirePolicyRoleLockAsync(connection, transaction, cancellationToken);
            var readBefore = await ReadRoleStateAsync(
                connection, transaction, CompanionReadRuntimeRoleName, cancellationToken);
            RequireConformingReadRole(readBefore);

            var policyRole = await ReadRoleStateAsync(
                connection, transaction, CompanionPolicyRuntimeRoleName, cancellationToken);
            var created = false;
            if (policyRole is null)
            {
                await using var create = new SqlCommand(
                    $"CREATE ROLE [{CompanionPolicyRuntimeRoleName}] AUTHORIZATION [dbo];",
                    connection,
                    transaction);
                await create.ExecuteNonQueryAsync(cancellationToken);
                created = true;
                policyRole = await ReadRoleStateAsync(
                    connection, transaction, CompanionPolicyRuntimeRoleName, cancellationToken);
            }

            RequireAuthorityFreePolicyRole(policyRole);
            var readAfter = await ReadRoleStateAsync(
                connection, transaction, CompanionReadRuntimeRoleName, cancellationToken);
            if (readBefore != readAfter)
                throw new InvalidOperationException("The Companion read runtime role changed during the operation.");

            var occurredAtUtc = DateTimeOffset.UtcNow;
            var payload = JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                operation = CompanionPolicyRoleOperationVersion,
                roleName = CompanionPolicyRuntimeRoleName,
                outcome = created ? "created" : "preexisting",
                owner = "dbo",
                databaseRole = true,
                memberCount = 0,
                parentRoleCount = 0,
                explicitPermissionCount = 0,
                ownedSecurableCount = 0,
                inheritedApplicationAuthorityCount = 0,
                readRuntimeRoleUnchanged = true,
                supportId,
                correlationId,
                operationId = context.OperationId,
                occurredAtUtc
            });
            if (Encoding.UTF8.GetByteCount(payload) > 4096)
                throw new InvalidOperationException("The policy-role bootstrap evidence exceeded its size bound.");

            await transaction.CommitAsync(cancellationToken);
            committed = true;
            await (evidenceWriter ?? Console.Out).WriteLineAsync(payload);
            return 0;
        }
        catch
        {
            if (committed)
                throw;
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                throw new InvalidOperationException(
                    "The policy-role bootstrap failed and transaction rollback was not conclusive.");
            }
            throw;
        }
    }

    private static async Task AcquirePolicyRoleLockAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("""
            DECLARE @Result int;
            EXEC @Result = sys.sp_getapplock
                @Resource = N'AdventuresSuite.SqlAdministrator.CompanionPolicyRuntimeRole.v1',
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 0;
            IF @Result < 0 THROW 51000, 'The policy-role administrator lock is unavailable.', 1;
            """, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<RoleState?> ReadRoleStateAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string exactRoleName,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("""
            SELECT principal.name, principal.type, principal.is_fixed_role,
                   COALESCE(owner.name, N''),
                   (SELECT COUNT_BIG(*) FROM sys.database_role_members AS membership
                    WHERE membership.role_principal_id = principal.principal_id),
                   (SELECT COUNT_BIG(*) FROM sys.database_role_members AS membership
                    WHERE membership.member_principal_id = principal.principal_id),
                   (SELECT COUNT_BIG(*) FROM sys.database_permissions AS permission
                    WHERE permission.grantee_principal_id = principal.principal_id),
                   ((SELECT COUNT_BIG(*) FROM sys.schemas AS schemaValue
                     WHERE schemaValue.principal_id = principal.principal_id)
                    + (SELECT COUNT_BIG(*) FROM sys.objects AS objectValue
                       WHERE objectValue.principal_id = principal.principal_id))
            FROM sys.database_principals AS principal
            LEFT JOIN sys.database_principals AS owner
              ON owner.principal_id = principal.owning_principal_id
            WHERE principal.name COLLATE Latin1_General_100_CI_AS = @RoleName
            ORDER BY principal.principal_id;
            """, connection, transaction);
        command.Parameters.Add("@RoleName", System.Data.SqlDbType.NVarChar, 128).Value = exactRoleName;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var states = new List<RoleState>(2);
        while (await reader.ReadAsync(cancellationToken))
        {
            states.Add(new(
                reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), reader.GetString(3),
                reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6), reader.GetInt64(7)));
        }

        if (states.Count > 1)
            throw new InvalidOperationException("The administrator role identity is ambiguous.");
        if (states.Count == 0)
            return null;
        if (!string.Equals(states[0].Name, exactRoleName, StringComparison.Ordinal))
            throw new InvalidOperationException("A case-altered administrator role identity already exists.");
        return states[0];
    }

    private static void RequireAuthorityFreePolicyRole(RoleState? role)
    {
        if (role is null
            || role.Type != "R"
            || role.IsFixedRole
            || !string.Equals(role.Owner, "dbo", StringComparison.Ordinal)
            || role.MemberCount != 0
            || role.ParentRoleCount != 0
            || role.ExplicitPermissionCount != 0
            || role.OwnedSecurableCount != 0)
        {
            throw new InvalidOperationException(
                "The existing Companion policy runtime role is not the exact authority-free prerequisite.");
        }
    }

    private static void RequireConformingReadRole(RoleState? role)
    {
        if (role is null
            || role.Type != "R"
            || role.IsFixedRole
            || !string.Equals(role.Owner, "dbo", StringComparison.Ordinal)
            || role.MemberCount != 0
            || role.ParentRoleCount != 0
            || role.OwnedSecurableCount != 0)
        {
            throw new InvalidOperationException("The Companion read runtime role is not conforming.");
        }
    }

    private static async Task<int> BootstrapAsync(SqlConnection connection, Context context)
    {
        await AzureDevelopmentBootstrapper.BootstrapMigrationIdentityAsync(
            connection, context.MigrationPrincipalId.ToString(),
            context.MigrationClientId.ToString(), context.MigrationPrincipalName);
        await WriteBoundedAsync("sql_administrator_bootstrap_complete", context, new
        {
            migrationPrincipalVerified = true,
            temporaryPermissionCatalogVerified = true,
            directoryLookupUsed = false
        });
        return 0;
    }

    private static async Task<int> CleanupAsync(SqlConnection connection, Context context)
    {
        await AzureDevelopmentBootstrapper.CleanupMigrationIdentityAsync(
            connection, context.MigrationPrincipalId.ToString(),
            context.MigrationClientId.ToString(), context.MigrationPrincipalName);
        await WriteBoundedAsync("sql_administrator_cleanup_complete", context, new
        {
            migrationPrincipalAbsent = true,
            administratorPrerequisitesRetainedOrConclusiveAbsent = true
        });
        return 0;
    }

    private static async Task<int> ProveDeniedAsync(Context context, AccessToken token)
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString(context))
            {
                AccessToken = token.Token
            };
            await connection.OpenAsync();
        }
        catch (SqlException exception) when (exception.Number is 18456 or 40607 or 40615)
        {
            await WriteBoundedAsync("sql_administrator_authorization_denied", context, new
            {
                freshTokenAcquired = true,
                sqlConnectionAuthorized = false,
                azureSqlErrorNumber = exception.Number
            });
            return 0;
        }
        throw new InvalidOperationException("Fresh administrator credentials unexpectedly retained SQL access.");
    }

    internal static async Task<int> CaptureBaselineAsync(
        SqlConnection connection,
        Context context,
        TextWriter? evidenceWriter = null)
    {
        var path = Path.GetFullPath(Require("ADVENTURESSUITE_ADMIN_BASELINE_SQL_PATH"));
        if (!path.EndsWith("/infrastructure/private-sql-admin-operation/baseline.sql", StringComparison.Ordinal)
            || !File.Exists(path))
            throw new InvalidOperationException("The reviewed administrator baseline SQL is unavailable.");
        var sql = await File.ReadAllTextAsync(path);
        if (!Hash(sql).Equals(RequireHex("ADVENTURESSUITE_ADMIN_BASELINE_SQL_SHA256", 64), StringComparison.Ordinal))
            throw new InvalidOperationException("The administrator baseline SQL checksum is not approved.");

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 60;
        await using var reader = await command.ExecuteReaderAsync();
        var schemas = new List<object>();
        while (await reader.ReadAsync()) schemas.Add(new { name = reader.GetString(0), owner = reader.GetString(2) });
        await reader.NextResultAsync();
        var roleMap = new SortedDictionary<string, (string Owner, List<string> Members)>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0); var owner = reader.GetString(1);
            if (!roleMap.TryGetValue(name, out var role)) role = (owner, []);
            if (!reader.IsDBNull(2)) role.Members.Add(reader.GetString(2));
            roleMap[name] = role;
        }
        var roles = roleMap.Select(pair => new { name = pair.Key, owner = pair.Value.Owner, memberSidSha256 = pair.Value.Members }).ToArray();
        await reader.NextResultAsync();
        var principals = new List<object>();
        while (await reader.ReadAsync()) principals.Add(new
        {
            name = reader.GetString(0),
            type = reader.GetString(1),
            authenticationType = reader.GetString(2),
            sidSha256 = reader.GetString(4)
        });
        await reader.NextResultAsync();
        var permissions = new List<object>();
        var permissionSignatures = new List<string>();
        while (await reader.ReadAsync())
        {
            var grantee = reader.GetString(0);
            var state = reader.GetString(1);
            var permission = reader.GetString(2);
            var permissionClass = reader.GetString(3);
            var securable = reader.IsDBNull(4) ? null : reader.GetString(4);
            permissions.Add(new { grantee, state, permission, @class = permissionClass, securable });
            permissionSignatures.Add(
                $"{grantee}|{state}|{permission}|{permissionClass}|{securable ?? string.Empty}");
        }
        await reader.NextResultAsync();
        await reader.ReadAsync();
        var journalExists = reader.GetBoolean(0);
        var rawScripts = new List<string>();
        await reader.NextResultAsync();
        if (journalExists)
        {
            while (await reader.ReadAsync()) rawScripts.Add(reader.GetString(0));
            await reader.NextResultAsync();
        }
        var objectCounts = new List<object>();
        var objectCountSignatures = new List<string>();
        while (await reader.ReadAsync())
        {
            var schema = reader.GetString(0);
            var type = reader.GetString(1);
            var count = reader.GetInt64(2);
            objectCounts.Add(new { schema, type, count });
            objectCountSignatures.Add($"{schema}|{type}|{count}");
        }
        if (schemas.Count > 3 || roles.Length > 5 || principals.Count > 2
            || permissions.Count > 168 || rawScripts.Count > 12 || objectCounts.Count > 24)
            throw new InvalidOperationException("The SQL administrator baseline evidence exceeded its bounds.");

        var journalIsValid = TryNormalizeJournal(rawScripts, out var scripts);
        var schemaJson = JsonSerializer.Serialize(schemas);
        var roleNames = roleMap.Keys.ToArray();
        var principalJson = JsonSerializer.Serialize(principals);
        var permissionJson = JsonSerializer.Serialize(permissions);
        var absent = schemas.Count == 0 && roles.Length == 0 && principals.Count == 0
            && permissions.Count == 0 && !journalExists && rawScripts.Count == 0 && objectCounts.Count == 0;
        var at0006 = journalIsValid
            && journalExists
            && scripts.SequenceEqual(ApprovedScripts.Take(6), StringComparer.Ordinal)
            && schemaJson == "[{\"name\":\"audit\",\"owner\":\"db_ddladmin\"},{\"name\":\"auth\",\"owner\":\"db_ddladmin\"},{\"name\":\"planning\",\"owner\":\"db_ddladmin\"}]"
            && roleNames.SequenceEqual(new[]
            {
                "AdventuresSuiteAuthenticationRuntime", "AdventuresSuiteMembershipRuntime"
            }, StringComparer.Ordinal)
            && roles.All(role => !JsonSerializer.Serialize(role).Contains("unexpected-redacted", StringComparison.Ordinal))
            && principals.Count == 0
            && permissionSignatures.SequenceEqual(At0006PermissionSignatures, StringComparer.Ordinal)
            && objectCountSignatures.SequenceEqual(At0006ObjectCountSignatures, StringComparer.Ordinal);
        var at0009 = journalIsValid
            && journalExists
            && scripts.SequenceEqual(ApprovedScripts.Take(9), StringComparer.Ordinal)
            && schemaJson == "[{\"name\":\"audit\",\"owner\":\"db_ddladmin\"},{\"name\":\"auth\",\"owner\":\"db_ddladmin\"},{\"name\":\"planning\",\"owner\":\"db_ddladmin\"}]"
            && roleNames.SequenceEqual(new[]
            {
                "AdventuresSuiteAuthenticationRuntime", "AdventuresSuiteCompanionReadRuntime",
                "AdventuresSuiteMembershipRuntime", "AdventuresSuitePlanningRuntime"
            }, StringComparer.Ordinal)
            && roleMap["AdventuresSuiteAuthenticationRuntime"].Owner == "dbo"
            && roleMap["AdventuresSuiteCompanionReadRuntime"].Owner == "dbo"
            && roleMap["AdventuresSuiteMembershipRuntime"].Owner == "dbo"
            && roleMap["AdventuresSuitePlanningRuntime"].Owner == "dbo"
            && roleMap["AdventuresSuiteAuthenticationRuntime"].Members.Count == 1
            && roleMap["AdventuresSuiteMembershipRuntime"].Members.Count == 1
            && roleMap["AdventuresSuiteAuthenticationRuntime"].Members[0]
                == roleMap["AdventuresSuiteMembershipRuntime"].Members[0]
            && roleMap["AdventuresSuiteCompanionReadRuntime"].Members.Count == 0
            && roleMap["AdventuresSuitePlanningRuntime"].Members.Count == 0
            && principals.Count == 0
            && permissionSignatures.SequenceEqual(At0009PermissionSignatures, StringComparer.Ordinal)
            && objectCountSignatures.SequenceEqual(At0009ObjectCountSignatures, StringComparer.Ordinal);
        var complete = schemas.Count == 3
            && schemaJson.Contains("\"planning\",\"owner\":\"db_ddladmin\"", StringComparison.Ordinal)
            && schemaJson.Contains("\"auth\",\"owner\":\"db_ddladmin\"", StringComparison.Ordinal)
            && schemaJson.Contains("\"audit\",\"owner\":\"db_ddladmin\"", StringComparison.Ordinal)
            && roleNames.SequenceEqual(new[]
            {
                "AdventuresSuiteAuthenticationRuntime", "AdventuresSuiteCompanionPolicyRuntime",
                "AdventuresSuiteCompanionReadRuntime", "AdventuresSuiteMembershipRuntime",
                "AdventuresSuitePlanningRuntime"
            }, StringComparer.Ordinal)
            && roles.All(role => !JsonSerializer.Serialize(role).Contains("unexpected-redacted", StringComparison.Ordinal))
            && principals.Count == 1
            && principalJson.Contains("\"name\":\"AdventuresSuiteMigrationDev-ffc9a\"", StringComparison.Ordinal)
            && !principalJson.Contains("unexpected-redacted", StringComparison.Ordinal)
            && !permissionJson.Contains("unexpected-redacted", StringComparison.Ordinal)
            && journalExists && journalIsValid
            && scripts.SequenceEqual(ApprovedScripts, StringComparer.Ordinal);
        var outcome = absent ? "absent" : at0006 ? nameof(MigrationJournalOutcome.At0006)
            : at0009 ? nameof(MigrationJournalOutcome.At0009)
            : complete ? "complete" : "unexpected";
        var baseline = new
        {
            schemaVersion = 1,
            binding = new
            {
                repositoryId = 1317655952,
                organizationId = 316268438,
                sourceSha = context.SourceSha,
                workflowSha256 = context.WorkflowSha256,
                operationId = context.OperationId,
                packageRunId = context.PackageRunId,
                packageArtifactId = context.PackageArtifactId,
                packageSha256 = context.PackageSha256,
                catalogSha256 = context.CatalogSha256,
                administratorIdentityResourceIdSha256 = Hash(context.AdministratorIdentityResourceId),
                serverResourceIdSha256 = Hash(context.SqlServerResourceId),
                databaseName = context.SqlDatabase,
                privateEndpointResourceIdSha256 = Hash(context.PrivateEndpointResourceId)
            },
            outcome,
            journal = new { exists = journalExists, scripts },
            schemas,
            roles,
            principals,
            permissions,
            objectCounts,
            residue = new { resourceCount = 0, registrationCount = 0, temporaryAssignmentCount = 0, guestFileCount = 0 }
        };
        var payload = JsonSerializer.Serialize(baseline);
        if (Encoding.UTF8.GetByteCount(payload) > 65536)
            throw new InvalidOperationException("The SQL administrator evidence exceeded its size bound.");
        await (evidenceWriter ?? Console.Out).WriteLineAsync(payload);
        return outcome == "unexpected" ? 1 : 0;
    }

    private static bool TryNormalizeJournal(
        IReadOnlyList<string> rawScripts,
        out IReadOnlyList<string> normalizedScripts)
    {
        var normalized = new List<string>(rawScripts.Count);
        var valid = true;
        foreach (var rawScript in rawScripts)
        {
            if (!rawScript.StartsWith(DbUpJournalPrefix, StringComparison.Ordinal))
            {
                valid = false;
                continue;
            }

            var script = rawScript[DbUpJournalPrefix.Length..];
            if (!ApprovedScripts.Contains(script, StringComparer.Ordinal))
            {
                valid = false;
                continue;
            }

            normalized.Add(script);
        }

        normalizedScripts = normalized.AsReadOnly();
        return valid && normalized.Count == rawScripts.Count;
    }

    private static string ConnectionString(Context context) =>
        new SqlConnectionStringBuilder
        {
            DataSource = $"tcp:{context.SqlServer}.database.windows.net,1433",
            InitialCatalog = context.SqlDatabase,
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 30
        }.ConnectionString;

    private static Task WriteBoundedAsync(string classification, Context context, object evidence)
    {
        var payload = JsonSerializer.Serialize(new
        {
            classification,
            sourceSha = context.SourceSha,
            operationId = context.OperationId,
            sqlTokenScope = SqlScope,
            evidence
        });
        if (Encoding.UTF8.GetByteCount(payload) > 65536)
            throw new InvalidOperationException("The SQL administrator evidence exceeded its size bound.");
        Console.WriteLine(payload);
        return Task.CompletedTask;
    }

    private static Context ReadContext() => new(
        Guid.Parse(Require("ADVENTURESSUITE_ADMIN_TENANT_ID")),
        Guid.Parse(Require("ADVENTURESSUITE_ADMIN_PRINCIPAL_ID")),
        Guid.Parse(Require("ADVENTURESSUITE_ADMIN_CLIENT_ID")),
        Guid.Parse(Require("ADVENTURESSUITE_MIGRATION_PRINCIPAL_ID")),
        Guid.Parse(Require("ADVENTURESSUITE_MIGRATION_PRINCIPAL_CLIENT_ID")),
        Require("ADVENTURESSUITE_MIGRATION_PRINCIPAL_NAME"),
        Require("ADVENTURESSUITE_SQL_SERVER"), Require("ADVENTURESSUITE_SQL_DATABASE"),
        RequireHex("ADVENTURESSUITE_RELEASE_SHA", 40), Require("ADVENTURESSUITE_ADMIN_OPERATION_ID"),
        RequireHex("ADVENTURESSUITE_ADMIN_WORKFLOW_SHA256", 64),
        long.Parse(Require("ADVENTURESSUITE_PACKAGE_RUN_ID")),
        long.Parse(Require("ADVENTURESSUITE_PACKAGE_ARTIFACT_ID")),
        RequireHex("ADVENTURESSUITE_PACKAGE_SHA256", 64),
        RequireHex("ADVENTURESSUITE_CATALOG_SHA256", 64),
        Require("ADVENTURESSUITE_ADMIN_IDENTITY_RESOURCE_ID"),
        Require("ADVENTURESSUITE_SQL_SERVER_RESOURCE_ID"),
        Require("ADVENTURESSUITE_SQL_PRIVATE_ENDPOINT_RESOURCE_ID"));

    private static string Require(string name) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))
            ? Environment.GetEnvironmentVariable(name)!.Trim()
            : throw new InvalidOperationException($"Set {name} for the reviewed administrator operation.");

    private static string RequireBoundedIdentifier(string name) =>
        RequireBoundedIdentifier(Require(name), name);

    private static string RequireAuthorizationIdentity(string name) =>
        RequireAuthorizationIdentity(Require(name), name);

    private static string RequireAuthorizationIdentity(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length is < 3 or > 64
            || value[0] is < 'a' or > 'z'
            || value.Any(character => character is not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9') and not '_'))
            throw new ArgumentException("A canonical authorization identity is required.", parameterName);
        return value;
    }

    private static string RequireBoundedIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length is < 8 or > 64
            || value != value.Trim()
            || value.Any(character => character is not (>= 'a' and <= 'z')
                and not (>= 'A' and <= 'Z')
                and not (>= '0' and <= '9')
                and not '-' and not '_' and not '.'))
            throw new ArgumentException("A bounded operation identifier is required.", parameterName);
        return value;
    }

    private static string RequireHex(string name, int length)
    {
        var value = Require(name);
        return value.Length == length && value.All(Uri.IsHexDigit)
            ? value.ToLowerInvariant()
            : throw new InvalidOperationException($"Set a valid {name} value.");
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    internal sealed record Context(
        Guid TenantId, Guid AdministratorPrincipalId, Guid AdministratorClientId,
        Guid MigrationPrincipalId, Guid MigrationClientId, string MigrationPrincipalName,
        string SqlServer, string SqlDatabase, string SourceSha, string OperationId,
        string WorkflowSha256, long PackageRunId, long PackageArtifactId,
        string PackageSha256, string CatalogSha256, string AdministratorIdentityResourceId,
        string SqlServerResourceId, string PrivateEndpointResourceId);

    private sealed record RoleState(
        string Name,
        string Type,
        bool IsFixedRole,
        string Owner,
        long MemberCount,
        long ParentRoleCount,
        long ExplicitPermissionCount,
        long OwnedSecurableCount);
}
