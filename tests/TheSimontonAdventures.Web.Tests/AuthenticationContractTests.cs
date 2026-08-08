using System.Reflection;
using TheSimontonAdventures.Web.Authorization;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies provider-neutral authentication contract boundaries.</summary>
public sealed class AuthenticationContractTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 7, 20, 0, 0, TimeSpan.Zero);

    /// <summary>Ensures issuer and subject values preserve exact ordinal identity.</summary>
    [Fact]
    public void ExternalIdentityKey_PreservesCaseSensitiveIssuerAndSubject()
    {
        var first = Key("https://login.example.com/tenant", "Customer-A");
        var second = Key("https://login.example.com/tenant", "customer-A");
        var third = Key("https://LOGIN.example.com/tenant", "Customer-A");

        Assert.Equal("Customer-A", first.Subject.Value);
        Assert.NotEqual(first, second);
        Assert.NotEqual(first, third);
    }

    /// <summary>Ensures identity values reject normalization and unsafe boundaries.</summary>
    [Theory]
    [InlineData(null, "subject")]
    [InlineData("", "subject")]
    [InlineData(" subject", "subject")]
    [InlineData("subject ", "subject")]
    [InlineData("sub\nject", "subject")]
    [InlineData("http://login.example.com/tenant", "issuer")]
    [InlineData(" https://login.example.com/tenant", "issuer")]
    [InlineData("https://user@login.example.com/tenant", "issuer")]
    [InlineData("https://login.example.com/tenant?x=1", "issuer")]
    [InlineData("https://login.example.com/tenant#fragment", "issuer")]
    public void ExternalIdentityValues_InvalidValue_Throws(string? value, string kind)
    {
        Assert.Throws<ArgumentException>(() =>
            _ = kind == "issuer"
                ? new ExternalIdentityIssuer(value!).Value
                : new ExternalIdentitySubject(value!).Value);
    }

    /// <summary>Ensures Unicode subjects remain exact and are never normalized.</summary>
    [Fact]
    public void ExternalIdentitySubject_UnicodeVariants_RemainDistinct()
    {
        var composed = new ExternalIdentitySubject("café");
        var decomposed = new ExternalIdentitySubject("café");

        Assert.Equal("café", composed.Value);
        Assert.NotEqual(composed, decomposed);
    }

    /// <summary>Ensures complete keys reject default component values.</summary>
    [Fact]
    public void ExternalIdentityKey_DefaultComponent_Throws()
    {
        var issuer = new ExternalIdentityIssuer("https://login.example.com/tenant");
        var subject = new ExternalIdentitySubject("subject-01");

        Assert.Throws<ArgumentException>(() => new ExternalIdentityKey(default, issuer, subject));
        Assert.Throws<ArgumentException>(() => new ExternalIdentityKey(
            new("external_id"), default, subject));
        Assert.Throws<ArgumentException>(() => new ExternalIdentityKey(
            new("external_id"), issuer, default));
    }

    /// <summary>Ensures mappings retain exact identity and enforce lifecycle timestamps.</summary>
    [Fact]
    public void ExternalIdentityMapping_ValidatesLifecycle()
    {
        var active = Mapping();
        var disabled = Mapping(disabledAtUtc: CreatedAt.AddHours(1));

        Assert.True(active.CanEstablishSession);
        Assert.False(disabled.CanEstablishSession);
        Assert.Throws<ArgumentException>(() => Mapping(lastAuthenticatedAtUtc: CreatedAt.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => Mapping(disabledAtUtc: CreatedAt.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => Mapping(
            lastAuthenticatedAtUtc: CreatedAt.AddHours(2),
            disabledAtUtc: CreatedAt.AddHours(1)));
        Assert.Throws<ArgumentException>(() => Mapping(
            lastAuthenticatedAtUtc: CreatedAt.ToOffset(TimeSpan.FromHours(-7))));
    }

    /// <summary>Ensures user lifecycle, security version, and disabled state agree.</summary>
    [Fact]
    public void PlatformUser_ValidatesLifecycleAndSecurityVersion()
    {
        var active = User(PlatformUserStatus.Active);
        var disabled = User(PlatformUserStatus.Disabled, CreatedAt.AddHours(1));

        Assert.True(active.CanUseSession);
        Assert.False(disabled.CanUseSession);
        Assert.Throws<ArgumentException>(() => User(PlatformUserStatus.Disabled));
        Assert.Throws<ArgumentException>(() => User(
            PlatformUserStatus.Active,
            CreatedAt.AddHours(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => User((PlatformUserStatus)999));
        Assert.Throws<ArgumentException>(() => new PlatformUser(
            new("user_steve"), PlatformUserStatus.Active, new(1),
            CreatedAt, CreatedAt.AddTicks(-1)));
    }

    /// <summary>Ensures implemented authentication audit actions remain explicit.</summary>
    [Fact]
    public void AuthenticationAuditActions_ClassifyRequiredMutations()
    {
        var actions = Enum.GetValues<AuthenticationAuditAction>();

        Assert.Equal(6, actions.Length);
        Assert.Contains(AuthenticationAuditAction.ExternalIdentityLinked, actions);
        Assert.Contains(AuthenticationAuditAction.ExternalIdentityUnlinked, actions);
        Assert.Contains(AuthenticationAuditAction.UserDisabled, actions);
        Assert.Contains(AuthenticationAuditAction.UserReenabled, actions);
        Assert.Contains(AuthenticationAuditAction.SecurityVersionAdvanced, actions);
        Assert.Contains(AuthenticationAuditAction.SessionAdministrativelyRevoked, actions);
    }

    /// <summary>Ensures security versions are positive and advance safely.</summary>
    [Fact]
    public void SecurityVersion_RequiresPositiveAndAdvances()
    {
        Assert.Equal(2, new SecurityVersion(1).Next().Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SecurityVersion(0));
        Assert.Throws<InvalidOperationException>(() => new SecurityVersion(long.MaxValue).Next());
    }

    /// <summary>Ensures a session cannot carry inconsistent lifecycle state.</summary>
    [Fact]
    public void ApplicationSession_InvalidState_Throws()
    {
        Assert.Throws<ArgumentException>(() => Session(lastSeenAtUtc: CreatedAt.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => Session(absoluteExpiresAtUtc: CreatedAt));
        Assert.Throws<ArgumentException>(() => Session(
            lastSeenAtUtc: CreatedAt.AddHours(8),
            absoluteExpiresAtUtc: CreatedAt.AddHours(8)));
        Assert.Throws<ArgumentException>(() => Session(revokedAtUtc: CreatedAt.AddMinutes(1)));
        Assert.Throws<ArgumentException>(() => Session(
            revocationReason: SessionRevocationReason.SignedOut));
        Assert.Throws<ArgumentOutOfRangeException>(() => Session(
            revokedAtUtc: CreatedAt.AddMinutes(1),
            revocationReason: (SessionRevocationReason)999));
        Assert.Throws<ArgumentException>(() => Session(
            lastSeenAtUtc: CreatedAt.ToOffset(TimeSpan.FromHours(-7))));
        Assert.Throws<ArgumentException>(() => Session(
            lastSeenAtUtc: CreatedAt.AddMinutes(2),
            revokedAtUtc: CreatedAt.AddMinutes(1),
            revocationReason: SessionRevocationReason.SignedOut));
    }

    /// <summary>Ensures session evaluation fails closed for every invalidating state.</summary>
    [Theory]
    [InlineData("active", ApplicationSessionState.Active)]
    [InlineData("idle", ApplicationSessionState.IdleExpired)]
    [InlineData("absolute", ApplicationSessionState.AbsoluteExpired)]
    [InlineData("revoked", ApplicationSessionState.Revoked)]
    [InlineData("version", ApplicationSessionState.SecurityVersionMismatch)]
    [InlineData("inactive", ApplicationSessionState.UserInactive)]
    public void ApplicationSession_EvaluatesAuthoritativeState(
        string scenario,
        ApplicationSessionState expected)
    {
        var session = scenario == "revoked"
            ? Session(
                revokedAtUtc: CreatedAt.AddMinutes(5),
                revocationReason: SessionRevocationReason.SignedOut)
            : Session();
        var now = scenario switch
        {
            "idle" => CreatedAt.AddMinutes(30),
            "absolute" => CreatedAt.AddHours(8),
            _ => CreatedAt.AddMinutes(10)
        };
        var status = scenario == "inactive"
            ? PlatformUserStatus.Disabled
            : PlatformUserStatus.Active;
        var version = scenario == "version" ? new SecurityVersion(2) : new SecurityVersion(1);

        Assert.Equal(expected, session.EvaluateAt(now, TimeSpan.FromMinutes(30), status, version));
    }

    /// <summary>Ensures session evaluation validates current authoritative inputs.</summary>
    [Fact]
    public void ApplicationSession_InvalidEvaluationInput_Throws()
    {
        var session = Session();

        Assert.Throws<ArgumentException>(() => session.EvaluateAt(
            CreatedAt.ToOffset(TimeSpan.FromHours(-7)),
            TimeSpan.FromMinutes(30), PlatformUserStatus.Active, new(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.EvaluateAt(
            CreatedAt, TimeSpan.Zero, PlatformUserStatus.Active, new(1)));
        Assert.Throws<ArgumentException>(() => session.EvaluateAt(
            CreatedAt, TimeSpan.FromMinutes(30), (PlatformUserStatus)999, new(1)));
        Assert.Throws<ArgumentException>(() => session.EvaluateAt(
            CreatedAt, TimeSpan.FromMinutes(30), PlatformUserStatus.Active, default));
        Assert.Throws<ArgumentException>(() => session.EvaluateAt(
            CreatedAt.AddTicks(-1), TimeSpan.FromMinutes(30),
            PlatformUserStatus.Active, new(1)));
    }

    /// <summary>Ensures external-provider configuration is complete and immutable.</summary>
    [Fact]
    public void AuthenticationConfiguration_ExternalProvider_Validates()
    {
        var configuration = ExternalConfiguration();

        Assert.Equal(AuthenticationMode.ExternalProvider, configuration.Mode);
        Assert.Equal("https://workspace.example.com", configuration.WorkspaceOrigin);
        Assert.Equal("certificate-key-vault-reference", configuration.ClientCertificateReference);
    }

    /// <summary>Ensures disabled and development modes cannot retain provider secrets.</summary>
    [Fact]
    public void AuthenticationConfiguration_ModeBoundaries_AreFailFast()
    {
        Assert.Equal(AuthenticationMode.Disabled, AuthenticationConfiguration.Disabled().Mode);
        var development = new AuthenticationConfiguration(
            AuthenticationMode.Development,
            "https://workspace.localhost:7041",
            new("development"),
            null, null, null, null, null,
            TimeSpan.FromHours(8),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
        Assert.Null(development.Authority);

        Assert.Throws<ArgumentException>(() => new AuthenticationConfiguration(
            AuthenticationMode.Development,
            "https://workspace.localhost:7041",
            new("development"),
            "https://login.example.com", null, null, null, null,
            TimeSpan.FromHours(8), TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)));
        Assert.Throws<ArgumentException>(() => new AuthenticationConfiguration(
            AuthenticationMode.Disabled,
            "https://workspace.example.com", default,
            null, null, null, null, null,
            TimeSpan.FromHours(8), TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuthenticationConfiguration(
            (AuthenticationMode)999,
            null, default, null, null, null, null, null,
            TimeSpan.FromHours(8), TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)));
    }

    /// <summary>Ensures external-provider protocol configuration is complete.</summary>
    [Fact]
    public void AuthenticationConfiguration_MissingProviderValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => new AuthenticationConfiguration(
            AuthenticationMode.ExternalProvider,
            "https://workspace.example.com",
            new("external_id"),
            "https://login.example.com/tenant",
            "client-identity",
            null,
            "/signin-oidc",
            "/signout-callback-oidc",
            TimeSpan.FromHours(8), TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)));
        Assert.Throws<ArgumentException>(() => new AuthenticationConfiguration(
            AuthenticationMode.ExternalProvider,
            "https://workspace.example.com",
            new("external_id"),
            "https://login.example.com/tenant",
            "client-identity",
            "certificate-reference",
            "/signin-oidc",
            "/signin-oidc",
            TimeSpan.FromHours(8), TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)));
    }

    /// <summary>Ensures invalid origins, callbacks, credentials, and durations are rejected.</summary>
    [Theory]
    [InlineData("http://workspace.example.com", "/signin-oidc", 30, 5)]
    [InlineData("https://workspace.example.com/", "/signin-oidc", 30, 5)]
    [InlineData("https://workspace.example.com/path", "/signin-oidc", 30, 5)]
    [InlineData("https://workspace.example.com", "//signin-oidc", 30, 5)]
    [InlineData("https://workspace.example.com", "/signin-oidc?next=/", 30, 5)]
    [InlineData("https://workspace.example.com", "/signin-oidc", 0, 5)]
    [InlineData("https://workspace.example.com", "/signin-oidc", 30, 30)]
    public void AuthenticationConfiguration_InvalidBoundary_Throws(
        string origin,
        string callback,
        int idleMinutes,
        int touchMinutes)
    {
        Assert.Throws<ArgumentException>(() => ExternalConfiguration(
            origin,
            callback,
            TimeSpan.FromMinutes(idleMinutes),
            TimeSpan.FromMinutes(touchMinutes)));
    }

    /// <summary>Ensures authentication contracts remain provider and framework independent.</summary>
    [Fact]
    public void AuthenticationContracts_DoNotExposeInfrastructureDependencies()
    {
        var contractTypes = typeof(AuthenticationConfiguration).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(AuthenticationConfiguration).Namespace)
            .Where(type => type.Name.Contains("Authentication", StringComparison.Ordinal)
                || type.Name.Contains("ExternalIdentity", StringComparison.Ordinal)
                || type.Name.Contains("Session", StringComparison.Ordinal)
                || type.Name.Contains("PlatformUser", StringComparison.Ordinal)
                || type.Name.Contains("SecurityVersion", StringComparison.Ordinal));

        var exposedTypes = contractTypes.SelectMany(GetExposedTypes).Select(type => type.FullName ?? string.Empty);

        Assert.DoesNotContain(exposedTypes, name =>
            name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.Identity", StringComparison.Ordinal)
            || name.StartsWith("Dapper", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.Data.SqlClient", StringComparison.Ordinal));
    }

    private static ExternalIdentityKey Key(string issuer, string subject) => new(
        new("external_id"),
        new(issuer),
        new(subject));

    private static ExternalIdentityMapping Mapping(
        DateTimeOffset? lastAuthenticatedAtUtc = null,
        DateTimeOffset? disabledAtUtc = null) => new(
        new("external_identity_01"),
        Key("https://login.example.com/tenant", "subject-01"),
        new("user_steve"),
        CreatedAt,
        lastAuthenticatedAtUtc,
        disabledAtUtc);

    private static PlatformUser User(
        PlatformUserStatus status,
        DateTimeOffset? disabledAtUtc = null) => new(
        new("user_steve"),
        status,
        new(1),
        CreatedAt,
        disabledAtUtc ?? CreatedAt,
        disabledAtUtc);

    private static ApplicationSession Session(
        DateTimeOffset? lastSeenAtUtc = null,
        DateTimeOffset? absoluteExpiresAtUtc = null,
        DateTimeOffset? revokedAtUtc = null,
        SessionRevocationReason? revocationReason = null) => new(
        new("session_01"),
        new("user_steve"),
        new(1),
        CreatedAt,
        lastSeenAtUtc ?? CreatedAt,
        absoluteExpiresAtUtc ?? CreatedAt.AddHours(8),
        revokedAtUtc,
        revocationReason);

    private static AuthenticationConfiguration ExternalConfiguration(
        string workspaceOrigin = "https://workspace.example.com",
        string callbackPath = "/signin-oidc",
        TimeSpan? idleTimeout = null,
        TimeSpan? touchInterval = null) => new(
        AuthenticationMode.ExternalProvider,
        workspaceOrigin,
        new("external_id"),
        "https://login.example.com/tenant",
        "client-identity",
        "certificate-key-vault-reference",
        callbackPath,
        "/signout-callback-oidc",
        TimeSpan.FromHours(8),
        idleTimeout ?? TimeSpan.FromMinutes(30),
        touchInterval ?? TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(5));

    private static IEnumerable<Type> GetExposedTypes(Type type)
    {
        yield return type;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            yield return property.PropertyType;
        }

        foreach (var constructor in type.GetConstructors())
        {
            foreach (var parameter in constructor.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            yield return method.ReturnType;
            foreach (var parameter in method.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }
    }
}
