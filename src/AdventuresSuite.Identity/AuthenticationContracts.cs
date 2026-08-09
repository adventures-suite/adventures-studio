namespace AdventuresSuite.Identity;

internal static class ExternalIdentityValue
{
    public static string RequireProvider(string? value, string parameterName) =>
        AuthorizationIdentity.Require(value, parameterName);

    public static string RequireIssuer(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 2048
            || value != value.Trim()
            || !Uri.TryCreate(value, UriKind.Absolute, out var issuer)
            || issuer.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(issuer.UserInfo)
            || !string.IsNullOrEmpty(issuer.Query)
            || !string.IsNullOrEmpty(issuer.Fragment))
        {
            throw new ArgumentException(
                "External identity issuers must be exact absolute HTTPS URIs without credentials, query, fragment, or surrounding whitespace.",
                parameterName);
        }

        return value;
    }

    public static string RequireSubject(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 255
            || value != value.Trim()
            || value.Any(char.IsControl))
        {
            throw new ArgumentException(
                "External identity subjects must contain 1-255 exact characters without controls or surrounding whitespace.",
                parameterName);
        }

        return value;
    }
}

internal static class AuthenticationTimestamp
{
    public static void RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Authentication timestamps must use UTC.", parameterName);
        }
    }
}

/// <summary>Identifies one external authentication provider adapter.</summary>
public readonly record struct ExternalIdentityProviderId
{
    /// <summary>Initializes a provider-neutral adapter identity.</summary>
    public ExternalIdentityProviderId(string value) =>
        Value = ExternalIdentityValue.RequireProvider(value, nameof(value));

    /// <summary>Gets the canonical provider adapter identity.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Preserves one validated external issuer using exact ordinal semantics.</summary>
public readonly record struct ExternalIdentityIssuer
{
    /// <summary>Initializes an exact external issuer value without normalizing it.</summary>
    public ExternalIdentityIssuer(string value) =>
        Value = ExternalIdentityValue.RequireIssuer(value, nameof(value));

    /// <summary>Gets the exact validated issuer value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Preserves one validated external subject using exact ordinal semantics.</summary>
public readonly record struct ExternalIdentitySubject
{
    /// <summary>Initializes an exact external subject value without normalizing it.</summary>
    public ExternalIdentitySubject(string value) =>
        Value = ExternalIdentityValue.RequireSubject(value, nameof(value));

    /// <summary>Gets the exact validated subject value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifies an external account without depending on provider claims.</summary>
public sealed record ExternalIdentityKey
{
    /// <summary>Initializes a validated immutable external identity key.</summary>
    public ExternalIdentityKey(
        ExternalIdentityProviderId providerId,
        ExternalIdentityIssuer issuer,
        ExternalIdentitySubject subject)
    {
        if (providerId == default || issuer == default || subject == default)
        {
            throw new ArgumentException("A provider, issuer, and subject are required.");
        }

        ProviderId = providerId;
        Issuer = issuer;
        Subject = subject;
    }

    /// <summary>Gets the provider adapter identity.</summary>
    public ExternalIdentityProviderId ProviderId { get; }

    /// <summary>Gets the exact case-sensitive issuer.</summary>
    public ExternalIdentityIssuer Issuer { get; }

    /// <summary>Gets the exact case-sensitive subject.</summary>
    public ExternalIdentitySubject Subject { get; }
}

/// <summary>Identifies one external-to-platform identity mapping.</summary>
public readonly record struct ExternalIdentityId
{
    /// <summary>Initializes a stable external identity mapping identifier.</summary>
    public ExternalIdentityId(string value) =>
        Value = AuthorizationIdentity.Require(value, nameof(value));

    /// <summary>Gets the stable mapping identifier.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Represents one immutable external identity mapping snapshot.</summary>
public sealed record ExternalIdentityMapping
{
    /// <summary>Initializes and validates an external identity mapping snapshot.</summary>
    public ExternalIdentityMapping(
        ExternalIdentityId id,
        ExternalIdentityKey key,
        UserId userId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? lastAuthenticatedAtUtc = null,
        DateTimeOffset? disabledAtUtc = null)
    {
        if (id == default || userId == default)
        {
            throw new ArgumentException("External identity and user identities are required.");
        }

        Id = id;
        Key = key ?? throw new ArgumentNullException(nameof(key));
        AuthenticationTimestamp.RequireUtc(createdAtUtc, nameof(createdAtUtc));
        if (lastAuthenticatedAtUtc.HasValue)
        {
            AuthenticationTimestamp.RequireUtc(
                lastAuthenticatedAtUtc.Value,
                nameof(lastAuthenticatedAtUtc));
        }

        if (disabledAtUtc.HasValue)
        {
            AuthenticationTimestamp.RequireUtc(disabledAtUtc.Value, nameof(disabledAtUtc));
        }

        if ((lastAuthenticatedAtUtc.HasValue && lastAuthenticatedAtUtc.Value < createdAtUtc)
            || (disabledAtUtc.HasValue && disabledAtUtc.Value < createdAtUtc)
            || (lastAuthenticatedAtUtc.HasValue
                && disabledAtUtc.HasValue
                && lastAuthenticatedAtUtc.Value > disabledAtUtc.Value))
        {
            throw new ArgumentException(
                "External identity lifecycle timestamps cannot precede creation.");
        }

        UserId = userId;
        CreatedAtUtc = createdAtUtc;
        LastAuthenticatedAtUtc = lastAuthenticatedAtUtc;
        DisabledAtUtc = disabledAtUtc;
    }

    /// <summary>Gets the stable mapping identifier.</summary>
    public ExternalIdentityId Id { get; }

    /// <summary>Gets the immutable provider identity key.</summary>
    public ExternalIdentityKey Key { get; }

    /// <summary>Gets the stable platform user identity.</summary>
    public UserId UserId { get; }

    /// <summary>Gets when the mapping was created in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>Gets the last successful provider authentication time in UTC.</summary>
    public DateTimeOffset? LastAuthenticatedAtUtc { get; }

    /// <summary>Gets when new sessions were disabled for this mapping.</summary>
    public DateTimeOffset? DisabledAtUtc { get; }

    /// <summary>Gets whether this mapping may establish a new session.</summary>
    public bool CanEstablishSession => !DisabledAtUtc.HasValue;
}

/// <summary>Describes the platform lifecycle of a human user.</summary>
public enum PlatformUserStatus
{
    /// <summary>The user exists but has not completed platform onboarding.</summary>
    Onboarding,
    /// <summary>The user may establish sessions and seek Creator authorization.</summary>
    Active,
    /// <summary>The user is disabled and cannot use an application session.</summary>
    Disabled
}

/// <summary>
/// Classifies authentication mutations that require durable audit when their
/// application operations are implemented.
/// </summary>
public enum AuthenticationAuditAction
{
    /// <summary>An external identity was linked to a platform user.</summary>
    ExternalIdentityLinked,
    /// <summary>An external identity was unlinked from a platform user.</summary>
    ExternalIdentityUnlinked,
    /// <summary>A platform user was disabled.</summary>
    UserDisabled,
    /// <summary>A disabled platform user was reenabled.</summary>
    UserReenabled,
    /// <summary>A user security version advanced to revoke older sessions.</summary>
    SecurityVersionAdvanced,
    /// <summary>An administrator explicitly revoked an application session.</summary>
    SessionAdministrativelyRevoked
}

/// <summary>Represents one immutable platform-user lifecycle snapshot.</summary>
public sealed record PlatformUser
{
    /// <summary>Initializes and validates a platform-user snapshot.</summary>
    public PlatformUser(
        UserId id,
        PlatformUserStatus status,
        SecurityVersion securityVersion,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? disabledAtUtc = null)
    {
        if (id == default || securityVersion == default)
        {
            throw new ArgumentException("User identity and security version are required.");
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        AuthenticationTimestamp.RequireUtc(createdAtUtc, nameof(createdAtUtc));
        AuthenticationTimestamp.RequireUtc(updatedAtUtc, nameof(updatedAtUtc));
        if (disabledAtUtc.HasValue)
        {
            AuthenticationTimestamp.RequireUtc(disabledAtUtc.Value, nameof(disabledAtUtc));
        }

        if (updatedAtUtc < createdAtUtc
            || (disabledAtUtc.HasValue && disabledAtUtc.Value < createdAtUtc)
            || (disabledAtUtc.HasValue && disabledAtUtc.Value > updatedAtUtc)
            || (status == PlatformUserStatus.Disabled) != disabledAtUtc.HasValue)
        {
            throw new ArgumentException(
                "User lifecycle timestamps and disabled status must be consistent.");
        }

        Id = id;
        Status = status;
        SecurityVersion = securityVersion;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        DisabledAtUtc = disabledAtUtc;
    }

    /// <summary>Gets the stable platform user identity.</summary>
    public UserId Id { get; }

    /// <summary>Gets the current platform-user lifecycle status.</summary>
    public PlatformUserStatus Status { get; }

    /// <summary>Gets the version that invalidates older sessions.</summary>
    public SecurityVersion SecurityVersion { get; }

    /// <summary>Gets when the platform user was created in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>Gets when the platform-user snapshot last changed in UTC.</summary>
    public DateTimeOffset UpdatedAtUtc { get; }

    /// <summary>Gets when the platform user was disabled in UTC.</summary>
    public DateTimeOffset? DisabledAtUtc { get; }

    /// <summary>Gets whether this user may use an otherwise valid application session.</summary>
    public bool CanUseSession => Status == PlatformUserStatus.Active;

    /// <summary>Returns a new immutable snapshot after an allowed lifecycle transition.</summary>
    public PlatformUser TransitionTo(
        PlatformUserStatus targetStatus,
        DateTimeOffset transitionedAtUtc)
    {
        if (!Enum.IsDefined(Status) || !Enum.IsDefined(targetStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(targetStatus));
        }

        AuthenticationTimestamp.RequireUtc(transitionedAtUtc, nameof(transitionedAtUtc));
        if (transitionedAtUtc <= UpdatedAtUtc)
        {
            throw new ArgumentException(
                "A user lifecycle transition must occur after the current snapshot update.",
                nameof(transitionedAtUtc));
        }

        var isAllowed = (Status, targetStatus) switch
        {
            (PlatformUserStatus.Onboarding, PlatformUserStatus.Active) => true,
            (PlatformUserStatus.Onboarding, PlatformUserStatus.Disabled) => true,
            (PlatformUserStatus.Active, PlatformUserStatus.Disabled) => true,
            (PlatformUserStatus.Disabled, PlatformUserStatus.Active) => true,
            _ => false
        };
        if (!isAllowed)
        {
            throw new InvalidOperationException(
                $"The {Status} to {targetStatus} user lifecycle transition is not allowed.");
        }

        return new PlatformUser(
            Id,
            targetStatus,
            SecurityVersion.Next(),
            CreatedAtUtc,
            transitionedAtUtc,
            targetStatus == PlatformUserStatus.Disabled ? transitionedAtUtc : null);
    }
}

/// <summary>Represents a positive user security version.</summary>
public readonly record struct SecurityVersion
{
    /// <summary>Initializes a positive security version.</summary>
    public SecurityVersion(long value)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Value = value;
    }

    /// <summary>Gets the positive version value.</summary>
    public long Value { get; }

    /// <summary>Returns the next security version.</summary>
    public SecurityVersion Next()
    {
        if (Value == long.MaxValue)
        {
            throw new InvalidOperationException("The security version cannot advance further.");
        }

        return new(Value + 1);
    }
}

/// <summary>Identifies one revocable application-controlled session.</summary>
public readonly record struct UserSessionId
{
    /// <summary>Initializes a stable opaque application-session identifier.</summary>
    public UserSessionId(string value) =>
        Value = AuthorizationIdentity.Require(value, nameof(value));

    /// <summary>Gets the opaque session identifier.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Classifies why an application session was revoked.</summary>
public enum SessionRevocationReason
{
    /// <summary>The user signed out this session.</summary>
    SignedOut,
    /// <summary>The user or administrator revoked every session.</summary>
    SignedOutEverywhere,
    /// <summary>The owning platform user was disabled.</summary>
    UserDisabled,
    /// <summary>The authoritative user security version changed.</summary>
    SecurityVersionChanged,
    /// <summary>Identity compromise or recovery required revocation.</summary>
    IdentityCompromised
}

/// <summary>Classifies authoritative application-session validity.</summary>
public enum ApplicationSessionState
{
    /// <summary>The session is currently usable.</summary>
    Active,
    /// <summary>The platform user is not active.</summary>
    UserInactive,
    /// <summary>The session was explicitly revoked.</summary>
    Revoked,
    /// <summary>The idle activity window elapsed.</summary>
    IdleExpired,
    /// <summary>The absolute session lifetime elapsed.</summary>
    AbsoluteExpired,
    /// <summary>The session carries an obsolete user security version.</summary>
    SecurityVersionMismatch
}

/// <summary>Represents one immutable revocable application-session snapshot.</summary>
public sealed record ApplicationSession
{
    /// <summary>Initializes and validates an application-session snapshot.</summary>
    public ApplicationSession(
        UserSessionId id,
        UserId userId,
        SecurityVersion securityVersion,
        DateTimeOffset createdAtUtc,
        DateTimeOffset lastSeenAtUtc,
        DateTimeOffset absoluteExpiresAtUtc,
        DateTimeOffset? revokedAtUtc = null,
        SessionRevocationReason? revocationReason = null)
    {
        if (id == default || userId == default || securityVersion == default)
        {
            throw new ArgumentException(
                "Session, user, and security-version identities are required.");
        }

        AuthenticationTimestamp.RequireUtc(createdAtUtc, nameof(createdAtUtc));
        AuthenticationTimestamp.RequireUtc(lastSeenAtUtc, nameof(lastSeenAtUtc));
        AuthenticationTimestamp.RequireUtc(absoluteExpiresAtUtc, nameof(absoluteExpiresAtUtc));
        if (revokedAtUtc.HasValue)
        {
            AuthenticationTimestamp.RequireUtc(revokedAtUtc.Value, nameof(revokedAtUtc));
        }

        if (lastSeenAtUtc < createdAtUtc
            || absoluteExpiresAtUtc <= createdAtUtc
            || lastSeenAtUtc >= absoluteExpiresAtUtc
            || (revokedAtUtc.HasValue && revokedAtUtc.Value < lastSeenAtUtc))
        {
            throw new ArgumentException("Application-session timestamps are inconsistent.");
        }

        if (revocationReason.HasValue && !Enum.IsDefined(revocationReason.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(revocationReason));
        }

        if (revokedAtUtc.HasValue != revocationReason.HasValue)
        {
            throw new ArgumentException(
                "A revoked session requires both a UTC timestamp and safe reason.");
        }

        Id = id;
        UserId = userId;
        SecurityVersion = securityVersion;
        CreatedAtUtc = createdAtUtc;
        LastSeenAtUtc = lastSeenAtUtc;
        AbsoluteExpiresAtUtc = absoluteExpiresAtUtc;
        RevokedAtUtc = revokedAtUtc;
        RevocationReason = revocationReason;
    }

    /// <summary>Gets the opaque session identity.</summary>
    public UserSessionId Id { get; }

    /// <summary>Gets the stable platform user identity.</summary>
    public UserId UserId { get; }

    /// <summary>Gets the security version captured when the session was created.</summary>
    public SecurityVersion SecurityVersion { get; }

    /// <summary>Gets when the session was created in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>Gets the latest coalesced activity timestamp in UTC.</summary>
    public DateTimeOffset LastSeenAtUtc { get; }

    /// <summary>Gets the non-extendable absolute expiration in UTC.</summary>
    public DateTimeOffset AbsoluteExpiresAtUtc { get; }

    /// <summary>Gets when the session was revoked in UTC.</summary>
    public DateTimeOffset? RevokedAtUtc { get; }

    /// <summary>Gets the safe reason for revocation.</summary>
    public SessionRevocationReason? RevocationReason { get; }

    /// <summary>Evaluates the snapshot against current user and time state.</summary>
    public ApplicationSessionState EvaluateAt(
        DateTimeOffset utcNow,
        TimeSpan idleTimeout,
        PlatformUserStatus userStatus,
        SecurityVersion authoritativeSecurityVersion)
    {
        AuthenticationTimestamp.RequireUtc(utcNow, nameof(utcNow));
        if (idleTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(idleTimeout));
        }

        if (!Enum.IsDefined(userStatus) || authoritativeSecurityVersion == default)
        {
            throw new ArgumentException("Current user status and security version are required.");
        }

        if (utcNow < LastSeenAtUtc)
        {
            throw new ArgumentException(
                "The session cannot be evaluated before its latest recorded activity.",
                nameof(utcNow));
        }

        if (userStatus != PlatformUserStatus.Active)
        {
            return ApplicationSessionState.UserInactive;
        }

        if (RevokedAtUtc.HasValue)
        {
            return ApplicationSessionState.Revoked;
        }

        if (SecurityVersion != authoritativeSecurityVersion)
        {
            return ApplicationSessionState.SecurityVersionMismatch;
        }

        if (utcNow >= AbsoluteExpiresAtUtc)
        {
            return ApplicationSessionState.AbsoluteExpired;
        }

        return utcNow >= LastSeenAtUtc + idleTimeout
            ? ApplicationSessionState.IdleExpired
            : ApplicationSessionState.Active;
    }
}
