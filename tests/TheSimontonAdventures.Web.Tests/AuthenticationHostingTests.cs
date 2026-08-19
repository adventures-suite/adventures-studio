using AdventuresSuite.Identity.ExternalId;
using AdventuresSuite.Identity.SqlServer;
using Azure.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Abstractions;
using TheSimontonAdventures.Web.Authorization;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies fail-closed Slice 5F host composition.</summary>
public sealed class AuthenticationHostingTests
{
    /// <summary>Ensures absent authentication configuration fails startup.</summary>
    [Fact]
    public void AddAuthentication_AbsentConfiguration_Throws()
    {
        var builder = WebApplication.CreateBuilder();

        Assert.Throws<InvalidOperationException>(() =>
            builder.AddAdventuresSuiteAuthentication());
    }

    /// <summary>Ensures public-only operation must be selected explicitly.</summary>
    [Fact]
    public void AddAuthentication_ExplicitDisabledMode_RegistersDisabledMode()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Authentication:Mode"] = nameof(AuthenticationMode.Disabled);

        var configuration = builder.AddAdventuresSuiteAuthentication();
        using var services = builder.Services.BuildServiceProvider();

        Assert.Equal(AuthenticationMode.Disabled, configuration.Mode);
        Assert.Same(configuration, services.GetRequiredService<AuthenticationConfiguration>());
    }

    /// <summary>Ensures unknown or partial hosted modes fail rather than falling back.</summary>
    [Theory]
    [InlineData("ExternalProvider")]
    [InlineData("unexpected")]
    public void AddAuthentication_UnsupportedOrPartialConfiguration_Throws(string mode)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Authentication:Mode"] = mode;

        Assert.Throws<InvalidOperationException>(() =>
            builder.AddAdventuresSuiteAuthentication());
    }

    /// <summary>Ensures Development mode is rejected outside the exact Development environment.</summary>
    [Fact]
    public void AddAuthentication_DevelopmentModeOutsideDevelopment_Throws()
    {
        var builder = DevelopmentBuilder("Production", enabled: true);

        Assert.Throws<InvalidOperationException>(() => builder.AddAdventuresSuiteAuthentication());
    }

    /// <summary>Ensures Development mode requires a second explicit enablement control.</summary>
    [Fact]
    public void AddAuthentication_DevelopmentModeWithoutEnablement_Throws()
    {
        var builder = DevelopmentBuilder("Development", enabled: false);

        Assert.Throws<InvalidOperationException>(() => builder.AddAdventuresSuiteAuthentication());
    }

    /// <summary>Ensures the approved local mode composes the fixed adapter and normal session services.</summary>
    [Fact]
    public void AddAuthentication_ApprovedDevelopmentMode_RegistersFixedIdentity()
    {
        var builder = DevelopmentBuilder("Development", enabled: true);

        var configuration = builder.AddAdventuresSuiteAuthentication();
        using var services = builder.Services.BuildServiceProvider();
        var identity = services.GetRequiredService<DevelopmentAuthenticationIdentity>();
        var generator = services.GetRequiredService<IAuthenticationIdentityGenerator>();

        Assert.Equal(AuthenticationMode.Development, configuration.Mode);
        Assert.Equal("local-alpha-planner", identity.ExternalIdentity.Subject.Value);
        Assert.Equal("user_local_alpha_planner", generator.CreateUserId().Value);
        Assert.Equal("identity_local_alpha_planner", generator.CreateExternalIdentityId().Value);
        Assert.NotEqual(generator.CreateSessionId(), generator.CreateSessionId());
    }

    /// <summary>Ensures local SQL cannot point at Azure, a shared database, or relaxed credentials.</summary>
    [Theory]
    [InlineData("Server=tcp:shared.database.windows.net,1433;Database=AdventuresSuiteLocalAlpha;User ID=adventures_alpha_app;Password=x;Encrypt=True;TrustServerCertificate=True")]
    [InlineData("Server=localhost,1433;Database=AdventuresSuiteDev;User ID=adventures_alpha_app;Password=x;Encrypt=True;TrustServerCertificate=True")]
    [InlineData("Server=localhost,1433;Database=AdventuresSuiteLocalAlpha;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")]
    public void LocalSqlConfiguration_UnapprovedTarget_Throws(string value)
    {
        Assert.Throws<InvalidOperationException>(() => LocalDevelopmentSqlConfiguration.Validate(
            value, "AdventuresSuiteLocalAlpha", "Development", explicitlyEnabled: true));
    }

    /// <summary>Ensures ExternalProvider readiness can resolve the concrete remote signer.</summary>
    [Fact]
    public void AddAuthentication_ExternalProvider_RegistersConcreteSignedAssertionProvider()
    {
        var builder = WebApplication.CreateBuilder();
        var settings = new Dictionary<string, string?>
        {
            ["Authentication:Mode"] = nameof(AuthenticationMode.ExternalProvider),
            ["Authentication:WorkspaceOrigin"] = "https://workspace.example.com",
            ["Authentication:ProviderId"] = "entra_external_id_dev",
            ["Authentication:Authority"] = "https://tenant.example.com/tenant/v2.0",
            ["Authentication:ClientId"] = "client-id",
            ["Authentication:ClientCertificateName"] = "external-id-certificate",
            ["Authentication:CallbackPath"] = "/signin-oidc",
            ["Authentication:SignedOutCallbackPath"] = "/signout-callback-oidc",
            ["Authentication:AbsoluteSessionLifetime"] = "08:00:00",
            ["Authentication:IdleSessionTimeout"] = "00:30:00",
            ["Authentication:ActivityTouchInterval"] = "00:05:00",
            ["Authentication:CircuitRevalidationInterval"] = "00:05:00",
            ["Authentication:KeyVaultUri"] = "https://vault.example.com/",
            ["Authentication:DataProtectionBlobUri"] =
                "https://storage.example.com/dataprotection/keys.xml",
            ["Authentication:DataProtectionKeyUri"] =
                "https://vault.example.com/keys/data-protection",
            ["Authentication:SqlConnectionString"] =
                "Server=tcp:sql.example.com,1433;Database=AdventuresSuiteDev;" +
                "Authentication=Active Directory Managed Identity;Encrypt=Strict;" +
                "TrustServerCertificate=False",
            ["Authentication:SqlServerName"] = "sql.example.com",
            ["Authentication:SqlDatabaseName"] = "AdventuresSuiteDev",
            ["Authentication:DataProtectionApplicationName"] =
                "AdventuresSuite.Development.Authentication",
            ["Authentication:TrustedProxyAddresses:0"] = "169.254.129.1"
        };
        builder.Configuration.AddInMemoryCollection(settings);

        builder.AddAdventuresSuiteAuthentication();
        using var services = builder.Services.BuildServiceProvider();

        var concrete = services.GetRequiredService<KeyVaultExternalIdSignedAssertionProvider>();
        Assert.Same(concrete, services.GetRequiredService<ICustomSignedAssertionProvider>());
    }

    /// <summary>Ensures generated platform identities are typed, bounded, and unpredictable.</summary>
    [Fact]
    public void CryptographicIdentityGenerator_CreatesDistinctTypedValues()
    {
        var generator = new CryptographicAuthenticationIdentityGenerator();

        var users = Enumerable.Range(0, 32).Select(_ => generator.CreateUserId()).ToArray();
        var identities = Enumerable.Range(0, 32).Select(_ => generator.CreateExternalIdentityId()).ToArray();
        var sessions = Enumerable.Range(0, 32).Select(_ => generator.CreateSessionId()).ToArray();

        Assert.Equal(users.Length, users.Distinct().Count());
        Assert.Equal(identities.Length, identities.Distinct().Count());
        Assert.Equal(sessions.Length, sessions.Distinct().Count());
        Assert.All(users, value => Assert.StartsWith("user_", value.Value, StringComparison.Ordinal));
        Assert.All(identities, value => Assert.StartsWith("identity_", value.Value, StringComparison.Ordinal));
        Assert.All(sessions, value => Assert.StartsWith("session_", value.Value, StringComparison.Ordinal));
    }

    /// <summary>Ensures unsafe Key Vault references fail before any remote call.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" certificate")]
    [InlineData("certificate/version")]
    [InlineData("certificate_name")]
    public void KeyVaultSignedAssertionProvider_InvalidName_Throws(string reference)
    {
        Assert.Throws<ArgumentException>(() => new KeyVaultExternalIdSignedAssertionProvider(
            new Uri("https://vault.example.com/"),
            reference,
            new UnusedCredential()));
    }

    /// <summary>Ensures only the exact encrypted Managed Identity SQL target is accepted.</summary>
    [Fact]
    public void AzureSqlConfiguration_ExactManagedIdentityTarget_IsAccepted()
    {
        var value = AzureSqlAuthenticationConfiguration.Validate(
            "Server=tcp:adventures-suite-dev-sql.database.windows.net,1433;Database=AdventuresSuiteDev;Authentication=Active Directory Managed Identity;Encrypt=Strict;TrustServerCertificate=False",
            "adventures-suite-dev-sql.database.windows.net",
            "AdventuresSuiteDev");

        Assert.NotEmpty(value);
    }

    /// <summary>Ensures credentials and relaxed or redirected SQL connections fail closed.</summary>
    [Theory]
    [InlineData("Server=tcp:other.database.windows.net,1433;Database=AdventuresSuiteDev;Authentication=Active Directory Managed Identity;Encrypt=Strict;TrustServerCertificate=False")]
    [InlineData("Server=tcp:adventures-suite-dev-sql.database.windows.net,1433;Database=Other;Authentication=Active Directory Managed Identity;Encrypt=Strict;TrustServerCertificate=False")]
    [InlineData("Server=tcp:adventures-suite-dev-sql.database.windows.net,1433;Database=AdventuresSuiteDev;User ID=user;Password=secret;Encrypt=True;TrustServerCertificate=False")]
    [InlineData("Server=tcp:adventures-suite-dev-sql.database.windows.net,1433;Database=AdventuresSuiteDev;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=True")]
    [InlineData("Server=tcp:adventures-suite-dev-sql.database.windows.net,1433;Database=AdventuresSuiteDev;Authentication=Active Directory Default;Encrypt=Strict;TrustServerCertificate=False")]
    public void AzureSqlConfiguration_UnapprovedConnection_Throws(string connectionString)
    {
        Assert.Throws<InvalidOperationException>(() => AzureSqlAuthenticationConfiguration.Validate(
            connectionString,
            "adventures-suite-dev-sql.database.windows.net",
            "AdventuresSuiteDev"));
    }

    private sealed class UnusedCredential : TokenCredential
    {
        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static WebApplicationBuilder DevelopmentBuilder(string environment, bool enabled)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Mode"] = "Development",
            ["Authentication:Development:Enabled"] = enabled.ToString(),
            ["Authentication:WorkspaceOrigin"] = "https://localhost:7041",
            ["Authentication:ProviderId"] = "local_alpha_development",
            ["Authentication:AbsoluteSessionLifetime"] = "08:00:00",
            ["Authentication:IdleSessionTimeout"] = "00:30:00",
            ["Authentication:ActivityTouchInterval"] = "00:05:00",
            ["Authentication:CircuitRevalidationInterval"] = "00:05:00",
            ["Authentication:SqlConnectionString"] =
                "Server=localhost,1433;Database=AdventuresSuiteLocalAlpha;User ID=adventures_alpha_app;Password=local-test-only;Encrypt=True;TrustServerCertificate=True",
            ["Authentication:SqlDatabaseName"] = "AdventuresSuiteLocalAlpha",
            ["Authentication:Development:Issuer"] = "https://identity.localhost/adventures-suite",
            ["Authentication:Development:Subject"] = "local-alpha-planner",
            ["Authentication:Development:UserId"] = "user_local_alpha_planner",
            ["Authentication:Development:ExternalIdentityId"] = "identity_local_alpha_planner"
        });
        return builder;
    }
}
