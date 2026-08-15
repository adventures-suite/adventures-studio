using AdventuresSuite.Companion.Application;
using AdventuresSuite.Identity;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace AdventuresSuite.Companion.SqlServer;

/// <summary>Resolves authoritative Companion access facts without activating an API projection.</summary>
public sealed class SqlCompanionAuthoritativeAccessContextResolver
    : ICompanionAuthoritativeAccessContextResolver, ICompanionProjectionAuthorizationRecheck
{
    private const string ViewPermission = "AdventurePlan.View";
    private readonly string connectionString;
    private readonly ICompanionInformationPolicy informationPolicy;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes an inert resolver with authoritative time and policy providers.</summary>
    public SqlCompanionAuthoritativeAccessContextResolver(
        string connectionString,
        ICompanionInformationPolicy informationPolicy,
        TimeProvider timeProvider)
    {
        this.connectionString = string.IsNullOrWhiteSpace(connectionString)
            ? throw new ArgumentException("A SQL Server connection string is required.", nameof(connectionString))
            : connectionString;
        this.informationPolicy = informationPolicy ?? throw new ArgumentNullException(nameof(informationPolicy));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async Task<CompanionAccessContextResolution> ResolveAdventureAsync(
        CompanionExternalIdentity identity,
        string adventureId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ValidateAdventureId(adventureId);
        var evaluatedAtUtc = timeProvider.GetUtcNow();
        RequireUtc(evaluatedAtUtc);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var identities = (await connection.QueryAsync<IdentityRow>(new CommandDefinition(
                IdentitySql,
                new
                {
                    Provider = identity.ProviderId.Value,
                    Issuer = identity.Issuer.Value,
                    Subject = identity.Subject.Value,
                    IdentityKeyHash = ComputeIdentityKeyHash(identity)
                },
                cancellationToken: cancellationToken))).AsList();

            if (identities.Count == 0)
                return Closed(CompanionAccessContextOutcome.Unmapped);
            if (identities.Count > 1)
                return Closed(CompanionAccessContextOutcome.Ambiguous);

            var identityOutcome = ValidateIdentity(identities[0], identity);
            if (identityOutcome.HasValue)
                return Closed(identityOutcome.Value);

            var candidates = (await connection.QueryAsync<AccessRow>(new CommandDefinition(
                AccessSql,
                new
                {
                    identities[0].UserId,
                    identities[0].ExternalIdentityId,
                    AdventureId = adventureId,
                    EvaluatedAtUtc = evaluatedAtUtc,
                    Permission = ViewPermission
                },
                cancellationToken: cancellationToken))).AsList();

            if (candidates.Count == 0)
                return Closed(CompanionAccessContextOutcome.Unauthorized);
            if (candidates.Count > 1)
                return Closed(CompanionAccessContextOutcome.Ambiguous);

            var mapped = MapCandidate(candidates[0], identities[0].ExternalIdentityId, evaluatedAtUtc);
            if (mapped.Outcome.HasValue)
                return Closed(mapped.Outcome.Value);

            var candidate = mapped.Context!;
            var policy = await informationPolicy.EvaluateAsync(
                new CompanionInformationPolicyRequest(
                    candidate.UserId,
                    candidate.CreatorId,
                    candidate.AdventureId,
                    candidate.TravelerId,
                    candidate.MembershipVersion,
                    candidate.ParticipationVersion,
                    candidate.RequiredPermission,
                    candidate.EvaluatedAtUtc),
                cancellationToken);

            if (!policy.IsAllowed)
                return Closed(CompanionAccessContextOutcome.InformationPolicyClosed);
            if (!IsSafePolicyVersion(policy.Version))
                return Closed(CompanionAccessContextOutcome.Malformed);

            return CompanionAccessContextResolution.Resolved(candidate with
            {
                InformationPolicyVersion = policy.Version!
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SqlException)
        {
            return Closed(CompanionAccessContextOutcome.OperationallyUnavailable);
        }
        catch (ArgumentException)
        {
            return Closed(CompanionAccessContextOutcome.OperationallyUnavailable);
        }
        catch (InvalidOperationException)
        {
            return Closed(CompanionAccessContextOutcome.OperationallyUnavailable);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsCurrentAsync(
        CompanionAuthoritativeAccessContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ExternalIdentityId == default
            || context.RequiredPermission != Permissions.AdventurePlanView
            || context.UserSecurityVersion < 1
            || context.MembershipVersion < 1
            || context.ParticipationVersion < 1
            || !IsSafePolicyVersion(context.InformationPolicyVersion)
            || context.EvaluatedAtUtc.Offset != TimeSpan.Zero)
            return false;

        try
        {
            var evaluatedAtUtc = timeProvider.GetUtcNow();
            RequireUtc(evaluatedAtUtc);
            await using var connection = new SqlConnection(connectionString);
            var isCurrent = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                RecheckSql,
                new
                {
                    UserId = context.UserId.Value,
                    ExternalIdentityId = context.ExternalIdentityId.Value,
                    UserSecurityVersion = context.UserSecurityVersion,
                    CreatorId = context.CreatorId.Value,
                    context.AdventureId,
                    context.TravelerId,
                    context.MembershipVersion,
                    context.ParticipationVersion,
                    EvaluatedAtUtc = evaluatedAtUtc,
                    Permission = ViewPermission
                },
                cancellationToken: cancellationToken)) == 1;
            if (!isCurrent)
                return false;

            var policy = await informationPolicy.EvaluateAsync(
                new CompanionInformationPolicyRequest(
                    context.UserId,
                    context.CreatorId,
                    context.AdventureId,
                    context.TravelerId,
                    context.MembershipVersion,
                    context.ParticipationVersion,
                    context.RequiredPermission,
                    evaluatedAtUtc),
                cancellationToken);
            return policy.IsAllowed
                && string.Equals(policy.Version, context.InformationPolicyVersion, StringComparison.Ordinal);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SqlException)
        {
            return false;
        }
    }

    private static CompanionAccessContextOutcome? ValidateIdentity(
        IdentityRow row,
        CompanionExternalIdentity expected)
    {
        if (!string.Equals(row.Provider, expected.ProviderId.Value, StringComparison.Ordinal)
            || !string.Equals(row.Issuer, expected.Issuer.Value, StringComparison.Ordinal)
            || !string.Equals(row.Subject, expected.Subject.Value, StringComparison.Ordinal))
            return CompanionAccessContextOutcome.Malformed;
        if (row.IdentityDisabledAtUtc.HasValue || string.Equals(row.UserStatus, "Disabled", StringComparison.Ordinal))
            return CompanionAccessContextOutcome.Disabled;
        if (string.Equals(row.UserStatus, "Onboarding", StringComparison.Ordinal))
            return CompanionAccessContextOutcome.Inactive;
        if (!string.Equals(row.UserStatus, "Active", StringComparison.Ordinal)
            || row.UserSecurityVersion < 1
            || row.UserDisabledAtUtc.HasValue)
            return CompanionAccessContextOutcome.Malformed;

        try
        {
            _ = new UserId(row.UserId);
        }
        catch (ArgumentException)
        {
            return CompanionAccessContextOutcome.Malformed;
        }

        return null;
    }

    private static (CompanionAuthoritativeAccessContext? Context, CompanionAccessContextOutcome? Outcome)
        MapCandidate(AccessRow row, string externalIdentityId, DateTimeOffset evaluatedAtUtc)
    {
        if (string.Equals(row.MembershipStatus, "Revoked", StringComparison.Ordinal)
            || string.Equals(row.ParticipationStatus, "Revoked", StringComparison.Ordinal))
            return (null, CompanionAccessContextOutcome.Revoked);

        if (!string.Equals(row.MembershipStatus, "Active", StringComparison.Ordinal)
            || evaluatedAtUtc < row.MembershipEffectiveFromUtc
            || (row.MembershipExpiresAtUtc.HasValue && evaluatedAtUtc >= row.MembershipExpiresAtUtc.Value)
            || !string.Equals(row.ParticipationStatus, "Accepted", StringComparison.Ordinal)
            || evaluatedAtUtc < row.ParticipationEffectiveFromUtc
            || (row.ParticipationExpiresAtUtc.HasValue && evaluatedAtUtc >= row.ParticipationExpiresAtUtc.Value))
            return (null, CompanionAccessContextOutcome.Inactive);

        if (row.HasUnknownRole || row.HasUnknownPermission)
            return (null, CompanionAccessContextOutcome.Malformed);

        if (!row.HasPermission)
            return (null, CompanionAccessContextOutcome.Unauthorized);

        if (row.UserSecurityVersion < 1 || row.MembershipVersion < 1 || row.ParticipationVersion < 1)
            return (null, CompanionAccessContextOutcome.Malformed);

        try
        {
            var context = new CompanionAuthoritativeAccessContext(
                new ExternalIdentityId(externalIdentityId),
                new UserId(row.UserId),
                row.UserSecurityVersion,
                new CreatorId(row.CreatorId),
                RequireBoundedIdentity(row.AdventurePlanId),
                RequireBoundedIdentity(row.TravelerId),
                row.MembershipVersion,
                row.ParticipationVersion,
                Permissions.AdventurePlanView,
                "closed",
                evaluatedAtUtc);
            return (context, null);
        }
        catch (ArgumentException)
        {
            return (null, CompanionAccessContextOutcome.Malformed);
        }
    }

    private static string RequireBoundedIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > CompanionAccessContextLimits.MaximumAdventureIdLength
            || value != value.Trim()
            || value.Any(char.IsControl))
            throw new ArgumentException("A bounded opaque identity is required.");
        return value;
    }

    private static void ValidateAdventureId(string adventureId) => _ = RequireBoundedIdentity(adventureId);

    private static void RequireUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException("Authorization evaluation time must be UTC.", nameof(value));
    }

    private static bool IsSafePolicyVersion(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 64
        && value == value.Trim()
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');

    private static byte[] ComputeIdentityKeyHash(CompanionExternalIdentity identity)
    {
        // This exactly mirrors migration 0004's persisted SQL expression. Provider is
        // varchar/ASCII while issuer, subject, and CONCAT's result are nvarchar/UTF-16LE.
        var provider = identity.ProviderId.Value;
        var issuer = identity.Issuer.Value;
        var subject = identity.Subject.Value;
        var exactKey = $"{Encoding.ASCII.GetByteCount(provider)}:{provider}|" +
            $"{Encoding.Unicode.GetByteCount(issuer)}:{issuer}|" +
            $"{Encoding.Unicode.GetByteCount(subject)}:{subject}";
        return SHA256.HashData(Encoding.Unicode.GetBytes(exactKey));
    }

    private static CompanionAccessContextResolution Closed(CompanionAccessContextOutcome outcome) =>
        CompanionAccessContextResolution.Closed(outcome);

    private const string IdentitySql = """
        SELECT TOP (2)
            identityMap.ExternalIdentityId,
            identityMap.UserId,
            identityMap.Provider,
            identityMap.Issuer,
            identityMap.Subject,
            identityMap.DisabledAtUtc AS IdentityDisabledAtUtc,
            platformUser.Status AS UserStatus,
            platformUser.SecurityVersion AS UserSecurityVersion,
            platformUser.DisabledAtUtc AS UserDisabledAtUtc
        FROM auth.ExternalIdentities AS identityMap
        INNER JOIN auth.Users AS platformUser
            ON platformUser.UserId = identityMap.UserId COLLATE Latin1_General_100_BIN2
        WHERE identityMap.IdentityKeyHash = @IdentityKeyHash
          AND identityMap.Provider = @Provider COLLATE Latin1_General_100_BIN2
          AND identityMap.Issuer = @Issuer COLLATE Latin1_General_100_BIN2
          AND identityMap.Subject = @Subject COLLATE Latin1_General_100_BIN2;
        """;

    private const string AccessSql = """
        SELECT TOP (2)
            platformUser.UserId,
            platformUser.SecurityVersion AS UserSecurityVersion,
            adventure.CreatorId,
            adventure.AdventurePlanId,
            participation.TravelerId,
            membership.Status AS MembershipStatus,
            membership.Version AS MembershipVersion,
            membership.EffectiveFromUtc AS MembershipEffectiveFromUtc,
            membership.ExpiresAtUtc AS MembershipExpiresAtUtc,
            participation.Status AS ParticipationStatus,
            participation.Version AS ParticipationVersion,
            participation.EffectiveFromUtc AS ParticipationEffectiveFromUtc,
            participation.ExpiresAtUtc AS ParticipationExpiresAtUtc,
            CONVERT(bit, CASE WHEN
                EXISTS (
                    SELECT 1
                    FROM auth.CreatorMembershipRoles AS membershipRole
                    WHERE membershipRole.CreatorId = membership.CreatorId
                      AND membershipRole.CreatorMembershipId = membership.CreatorMembershipId
                      AND membershipRole.Role IN ('Owner', 'Administrator', 'Planner', 'Contributor', 'Viewer'))
                OR EXISTS (
                    SELECT 1
                    FROM auth.CreatorMembershipPermissionGrants AS permissionGrant
                    WHERE permissionGrant.CreatorId = membership.CreatorId
                      AND permissionGrant.CreatorMembershipId = membership.CreatorMembershipId
                      AND permissionGrant.Permission = @Permission COLLATE Latin1_General_100_BIN2)
                THEN 1 ELSE 0 END) AS HasPermission,
            CONVERT(bit, CASE WHEN EXISTS (
                SELECT 1
                FROM auth.CreatorMembershipRoles AS membershipRole
                WHERE membershipRole.CreatorId = membership.CreatorId
                  AND membershipRole.CreatorMembershipId = membership.CreatorMembershipId
                  AND membershipRole.Role NOT IN ('Owner', 'Administrator', 'Planner', 'Contributor', 'Viewer'))
                THEN 1 ELSE 0 END) AS HasUnknownRole,
            CONVERT(bit, CASE WHEN EXISTS (
                SELECT 1
                FROM auth.CreatorMembershipPermissionGrants AS permissionGrant
                WHERE permissionGrant.CreatorId = membership.CreatorId
                  AND permissionGrant.CreatorMembershipId = membership.CreatorMembershipId
                  AND permissionGrant.Permission NOT IN (
                    'Creator.View', 'Creator.ManageMembers', 'AdventurePlan.View',
                    'AdventurePlan.Create', 'AdventurePlan.Edit', 'AdventurePlan.ViewArchived',
                    'AdventurePlan.Archive', 'AdventurePlan.Restore',
                    'AdventurePlan.ViewSensitiveReservations', 'PlanningProposal.Submit',
                    'PlanningProposal.Review', 'PlanningProposal.ApplyApproved',
                    'PlanningEngagement.Invite', 'PlanningEngagement.Manage',
                    'PlanningEngagement.DirectEdit', 'Audit.View', 'Support.Impersonate'))
                THEN 1 ELSE 0 END) AS HasUnknownPermission
        FROM auth.Users AS platformUser
        INNER JOIN auth.ExternalIdentities AS identityMap
            ON identityMap.UserId = platformUser.UserId COLLATE Latin1_General_100_BIN2
           AND identityMap.ExternalIdentityId = @ExternalIdentityId COLLATE Latin1_General_100_BIN2
           AND identityMap.DisabledAtUtc IS NULL
        INNER JOIN planning.TravelerParticipations AS participation
            ON participation.UserId = platformUser.UserId COLLATE Latin1_General_100_BIN2
        INNER JOIN planning.AdventurePlans AS adventure
            ON adventure.CreatorId = participation.CreatorId
           AND adventure.AdventurePlanId = participation.AdventurePlanId
        INNER JOIN auth.CreatorMemberships AS membership
            ON membership.CreatorId = adventure.CreatorId COLLATE Latin1_General_100_BIN2
           AND membership.UserId = platformUser.UserId
        WHERE platformUser.UserId = @UserId COLLATE Latin1_General_100_BIN2
          AND platformUser.Status = 'Active'
          AND platformUser.DisabledAtUtc IS NULL
          AND adventure.AdventurePlanId = @AdventureId COLLATE Latin1_General_100_BIN2;
        """;

    private const string RecheckSql = """
        SELECT CASE WHEN COUNT_BIG(*) = 1 THEN 1 ELSE 0 END
        FROM auth.Users AS platformUser
        INNER JOIN auth.ExternalIdentities AS identityMap
            ON identityMap.UserId = platformUser.UserId COLLATE Latin1_General_100_BIN2
           AND identityMap.ExternalIdentityId = @ExternalIdentityId COLLATE Latin1_General_100_BIN2
           AND identityMap.DisabledAtUtc IS NULL
        INNER JOIN planning.TravelerParticipations AS participation
            ON participation.UserId = platformUser.UserId COLLATE Latin1_General_100_BIN2
        INNER JOIN planning.AdventurePlans AS adventure
            ON adventure.CreatorId = participation.CreatorId
           AND adventure.AdventurePlanId = participation.AdventurePlanId
        INNER JOIN auth.CreatorMemberships AS membership
            ON membership.CreatorId = adventure.CreatorId COLLATE Latin1_General_100_BIN2
           AND membership.UserId = platformUser.UserId
        WHERE platformUser.UserId = @UserId COLLATE Latin1_General_100_BIN2
          AND platformUser.Status = 'Active'
          AND platformUser.DisabledAtUtc IS NULL
          AND platformUser.SecurityVersion = @UserSecurityVersion
          AND adventure.CreatorId = @CreatorId
          AND adventure.AdventurePlanId = @AdventureId
          AND participation.TravelerId = @TravelerId
          AND participation.Status = 'Accepted'
          AND participation.Version = @ParticipationVersion
          AND participation.EffectiveFromUtc <= @EvaluatedAtUtc
          AND (participation.ExpiresAtUtc IS NULL OR participation.ExpiresAtUtc > @EvaluatedAtUtc)
          AND membership.Status = 'Active'
          AND membership.Version = @MembershipVersion
          AND membership.EffectiveFromUtc <= @EvaluatedAtUtc
          AND (membership.ExpiresAtUtc IS NULL OR membership.ExpiresAtUtc > @EvaluatedAtUtc)
          AND (
              EXISTS (
                  SELECT 1 FROM auth.CreatorMembershipRoles AS membershipRole
                  WHERE membershipRole.CreatorId = membership.CreatorId
                    AND membershipRole.CreatorMembershipId = membership.CreatorMembershipId
                    AND membershipRole.Role IN ('Owner', 'Administrator', 'Planner', 'Contributor', 'Viewer'))
              OR EXISTS (
                  SELECT 1 FROM auth.CreatorMembershipPermissionGrants AS permissionGrant
                  WHERE permissionGrant.CreatorId = membership.CreatorId
                    AND permissionGrant.CreatorMembershipId = membership.CreatorMembershipId
                    AND permissionGrant.Permission = @Permission COLLATE Latin1_General_100_BIN2));
        """;

    private sealed record IdentityRow(
        string ExternalIdentityId,
        string UserId,
        string Provider,
        string Issuer,
        string Subject,
        DateTimeOffset? IdentityDisabledAtUtc,
        string UserStatus,
        long UserSecurityVersion,
        DateTimeOffset? UserDisabledAtUtc);

    private sealed record AccessRow(
        string UserId,
        long UserSecurityVersion,
        string CreatorId,
        string AdventurePlanId,
        string TravelerId,
        string MembershipStatus,
        long MembershipVersion,
        DateTimeOffset MembershipEffectiveFromUtc,
        DateTimeOffset? MembershipExpiresAtUtc,
        string ParticipationStatus,
        long ParticipationVersion,
        DateTimeOffset ParticipationEffectiveFromUtc,
        DateTimeOffset? ParticipationExpiresAtUtc,
        bool HasPermission,
        bool HasUnknownRole,
        bool HasUnknownPermission);
}
