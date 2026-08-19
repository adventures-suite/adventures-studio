using Microsoft.Data.SqlClient;

return await LocalAlphaBootstrap.RunAsync(args, Console.Out, Console.Error);

/// <summary>Runs the bounded, idempotent local Alpha identity and membership bootstrap.</summary>
public static class LocalAlphaBootstrap
{
    /// <summary>The only development Creator installed by this operation.</summary>
    public const string CreatorId = "creator_local_alpha";
    /// <summary>The fixed synthetic platform user.</summary>
    public const string UserId = "user_local_alpha_planner";
    /// <summary>The fixed synthetic external-identity row.</summary>
    public const string ExternalIdentityId = "identity_local_alpha_planner";
    /// <summary>The fixed synthetic membership row.</summary>
    public const string MembershipId = "membership_local_alpha_planner";
    /// <summary>The fixed local provider identifier.</summary>
    public const string ProviderId = "local_alpha_development";
    /// <summary>The fixed synthetic issuer.</summary>
    public const string Issuer = "https://identity.localhost/adventures-suite";
    /// <summary>The fixed synthetic subject.</summary>
    public const string Subject = "local-alpha-planner";
    /// <summary>The only Creator role granted by bootstrap.</summary>
    public const string CreatorRole = "Planner";

    /// <summary>Validates the explicit local target and installs only the approved rows.</summary>
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        if (args is not ["--bootstrap"])
        {
            await error.WriteLineAsync("Specify --bootstrap.");
            return 2;
        }

        try
        {
            var connectionString = Require("ADVENTURESSUITE_LOCAL_ALPHA_APP_CONNECTION_STRING");
            var enabled = string.Equals(Require("ADVENTURESSUITE_LOCAL_ALPHA_ENABLED"), "true", StringComparison.Ordinal);
            var environment = Require("ASPNETCORE_ENVIRONMENT");
            ValidateTarget(connectionString, environment, enabled);
            await BootstrapApprovedTargetAsync(connectionString);
            await output.WriteLineAsync("Local Alpha identity and Planner membership are ready.");
            return 0;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    /// <summary>Rejects non-local, shared, or insufficiently explicit bootstrap targets.</summary>
    public static void ValidateTarget(string connectionString, string environmentName, bool enabled)
    {
        var value = new SqlConnectionStringBuilder(connectionString);
        var host = value.DataSource.Split(',', 2)[0].Replace("tcp:", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (!string.Equals(environmentName, "Development", StringComparison.Ordinal)
            || !enabled
            || host is not ("localhost" or "127.0.0.1" or "::1")
            || !string.Equals(value.InitialCatalog, "AdventuresSuiteLocalAlpha", StringComparison.Ordinal)
            || !string.Equals(value.UserID, "adventures_alpha_app", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(value.Password)
            || value.IntegratedSecurity
            || value.Authentication != SqlAuthenticationMethod.NotSpecified
            || value.Encrypt != SqlConnectionEncryptOption.Mandatory
            || !value.TrustServerCertificate)
        {
            throw new InvalidOperationException(
                "Bootstrap requires explicit Development enablement and the approved localhost application identity and database.");
        }
    }

    /// <summary>Installs or verifies the fixed bootstrap state after the caller validates the target.</summary>
    public static async Task BootstrapApprovedTargetAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SET XACT_ABORT ON;
            DECLARE @Now datetimeoffset(7)=SYSUTCDATETIME();
            IF NOT EXISTS (SELECT 1 FROM auth.Users WHERE UserId=@UserId)
                INSERT auth.Users (UserId,Status,SecurityVersion,CreatedAtUtc,UpdatedAtUtc,DisabledAtUtc)
                VALUES (@UserId,'Active',1,@Now,@Now,NULL);
            IF EXISTS (SELECT 1 FROM auth.Users WHERE UserId=@UserId AND (Status<>'Active' OR SecurityVersion<>1))
                THROW 51000, 'The synthetic user exists with unapproved state.', 1;

            IF NOT EXISTS (SELECT 1 FROM auth.ExternalIdentities WHERE Provider=@Provider AND Issuer=@Issuer AND Subject=@Subject)
                INSERT auth.ExternalIdentities
                    (ExternalIdentityId,UserId,Provider,Issuer,Subject,CreatedAtUtc,LastAuthenticatedAtUtc,DisabledAtUtc)
                VALUES (@ExternalIdentityId,@UserId,@Provider,@Issuer,@Subject,@Now,NULL,NULL);
            IF EXISTS (SELECT 1 FROM auth.ExternalIdentities WHERE Provider=@Provider AND Issuer=@Issuer AND Subject=@Subject
                AND (ExternalIdentityId<>@ExternalIdentityId OR UserId<>@UserId OR DisabledAtUtc IS NOT NULL))
                THROW 51000, 'The synthetic external identity exists with unapproved state.', 1;

            IF NOT EXISTS (SELECT 1 FROM auth.CreatorMemberships WHERE CreatorId=@CreatorId AND UserId=@UserId)
            BEGIN
                INSERT auth.CreatorMemberships
                    (CreatorId,CreatorMembershipId,UserId,Status,Version,EffectiveFromUtc,ExpiresAtUtc,
                     CreatedAtUtc,UpdatedAtUtc,CreatedByUserId,UpdatedByUserId)
                VALUES (@CreatorId,@MembershipId,@UserId,'Active',1,@Now,NULL,@Now,@Now,@UserId,@UserId);
                INSERT auth.CreatorMembershipRoles (CreatorId,CreatorMembershipId,Role)
                    VALUES (@CreatorId,@MembershipId,@CreatorRole);
                INSERT audit.AuditEvents
                    (AuditEventId,CreatorId,ActorType,ActorUserId,Permission,ResourceType,ResourceId,
                     Outcome,ReasonCategory,OccurredAtUtc,CorrelationId,PreviousVersion,ResultingVersion)
                VALUES ('audit_local_alpha_bootstrap',@CreatorId,'System',NULL,'Creator.ManageMembers',
                    'CreatorMembership',@MembershipId,'Succeeded','Completed',@Now,
                    'local_alpha_bootstrap',NULL,1);
            END;
            IF EXISTS (SELECT 1 FROM auth.CreatorMemberships WHERE CreatorId=@CreatorId AND UserId=@UserId
                AND (CreatorMembershipId<>@MembershipId OR Status<>'Active' OR Version<>1 OR ExpiresAtUtc IS NOT NULL))
                THROW 51000, 'The local Alpha membership exists with unapproved state.', 1;
            IF (SELECT COUNT(*) FROM auth.CreatorMembershipRoles WHERE CreatorId=@CreatorId AND CreatorMembershipId=@MembershipId)<>1
                OR NOT EXISTS (SELECT 1 FROM auth.CreatorMembershipRoles WHERE CreatorId=@CreatorId AND CreatorMembershipId=@MembershipId AND Role=@CreatorRole)
                OR EXISTS (SELECT 1 FROM auth.CreatorMembershipPermissionGrants WHERE CreatorId=@CreatorId AND CreatorMembershipId=@MembershipId)
                THROW 51000, 'The local Alpha membership is not minimum-permission Planner access.', 1;
            """;
        command.Parameters.AddWithValue("@CreatorId", CreatorId);
        command.Parameters.AddWithValue("@UserId", UserId);
        command.Parameters.AddWithValue("@ExternalIdentityId", ExternalIdentityId);
        command.Parameters.AddWithValue("@MembershipId", MembershipId);
        command.Parameters.AddWithValue("@Provider", ProviderId);
        command.Parameters.AddWithValue("@Issuer", Issuer);
        command.Parameters.AddWithValue("@Subject", Subject);
        command.Parameters.AddWithValue("@CreatorRole", CreatorRole);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    private static string Require(string name) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))
            ? Environment.GetEnvironmentVariable(name)!
            : throw new InvalidOperationException($"Set {name}.");
}
