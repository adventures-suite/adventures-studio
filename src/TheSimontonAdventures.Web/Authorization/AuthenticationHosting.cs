using AdventuresSuite.Identity.ExternalId;
using AdventuresSuite.Identity.Persistence;
using AdventuresSuite.Identity.SqlServer;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace TheSimontonAdventures.Web.Authorization;

/// <summary>Composes environment-backed Slice 5F authentication services.</summary>
public static class AuthenticationHosting
{
    private const string SectionName = "Authentication";

    /// <summary>
    /// Adds disabled public-only authentication or the fully configured Azure
    /// External ID boundary. Partial external configuration fails startup.
    /// </summary>
    public static AuthenticationConfiguration AddAdventuresSuiteAuthentication(
        this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var section = builder.Configuration.GetSection(SectionName);
        var modeValue = section["Mode"];
        if (string.IsNullOrWhiteSpace(modeValue))
        {
            throw new InvalidOperationException("Authentication configuration 'Mode' is required.");
        }

        if (string.Equals(modeValue, nameof(AuthenticationMode.Disabled), StringComparison.OrdinalIgnoreCase))
        {
            var disabled = AuthenticationConfiguration.Disabled();
            builder.Services.AddSingleton(disabled);
            return disabled;
        }

        if (!string.Equals(
                modeValue,
                nameof(AuthenticationMode.ExternalProvider),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The configured authentication mode is not supported by this host.");
        }

        var configuration = new AuthenticationConfiguration(
            AuthenticationMode.ExternalProvider,
            Require(section, "WorkspaceOrigin"),
            new ExternalIdentityProviderId(Require(section, "ProviderId")),
            Require(section, "Authority"),
            Require(section, "ClientId"),
            Require(section, "ClientCertificateName"),
            Require(section, "CallbackPath"),
            Require(section, "SignedOutCallbackPath"),
            RequireDuration(section, "AbsoluteSessionLifetime"),
            RequireDuration(section, "IdleSessionTimeout"),
            RequireDuration(section, "ActivityTouchInterval"),
            RequireDuration(section, "CircuitRevalidationInterval"));

        var vaultUri = RequireHttpsRootUri(section, "KeyVaultUri");
        var dataProtectionBlobUri = RequireHttpsUri(section, "DataProtectionBlobUri");
        var dataProtectionKeyUri = RequireHttpsUri(section, "DataProtectionKeyUri");
        var sqlConnectionString = AzureSqlAuthenticationConfiguration.Validate(
            Require(section, "SqlConnectionString"),
            Require(section, "SqlServerName"),
            Require(section, "SqlDatabaseName"));
        var applicationName = Require(section, "DataProtectionApplicationName");
        if (!string.Equals(applicationName, "AdventuresSuite.Development.Authentication", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The Data Protection application name is not approved for this environment.");
        }

        var trustedProxyAddresses = section.GetSection("TrustedProxyAddresses").Get<string[]>() ?? [];
        if (trustedProxyAddresses.Length == 0)
        {
            throw new InvalidOperationException("At least one exact trusted proxy address is required.");
        }

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var value in trustedProxyAddresses)
            {
                if (!IPAddress.TryParse(value, out var address))
                {
                    throw new InvalidOperationException("Every trusted proxy must be an exact IP address.");
                }

                options.KnownProxies.Add(address);
            }
        });

        var credential = new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
        var signedAssertionProvider = new KeyVaultExternalIdSignedAssertionProvider(
            vaultUri,
            configuration.ClientCertificateReference!,
            credential);

        builder.Services.AddSingleton(configuration);
        builder.Services.AddSingleton(signedAssertionProvider);
        builder.Services.AddSingleton<IAuthenticationClock, SystemAuthenticationClock>();
        builder.Services.AddSingleton<IAuthenticationIdentityGenerator, CryptographicAuthenticationIdentityGenerator>();
        builder.Services.AddSingleton<IAuthenticationPersistenceTransactionFactory>(
            new SqlAuthenticationTransactionFactory(sqlConnectionString));
        builder.Services.AddSingleton(new SqlAuthenticationReadinessProbe(sqlConnectionString));
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = ExternalIdAuthenticationExtensions.SessionScheme;
                options.DefaultChallengeScheme = ExternalIdAuthenticationExtensions.Scheme;
                options.DefaultSignInScheme = ExternalIdAuthenticationExtensions.SessionScheme;
            })
            .AddAdventuresSuiteExternalId(configuration, signedAssertionProvider);

        builder.Services
            .AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToAzureBlobStorage(dataProtectionBlobUri, credential)
            .ProtectKeysWithAzureKeyVault(dataProtectionKeyUri, credential);
        builder.Services.AddSingleton<AuthenticationReadinessState>();
        builder.Services.AddHostedService<AuthenticationReadinessHostedService>();
        return configuration;
    }

    private static string Require(IConfiguration section, string key) =>
        !string.IsNullOrWhiteSpace(section[key]) && section[key] == section[key]!.Trim()
            ? section[key]!
            : throw new InvalidOperationException($"Authentication configuration '{key}' is required.");

    private static TimeSpan RequireDuration(IConfiguration section, string key) =>
        TimeSpan.TryParse(Require(section, key), out var value) && value > TimeSpan.Zero
            ? value
            : throw new InvalidOperationException($"Authentication configuration '{key}' must be a positive duration.");

    private static Uri RequireHttpsUri(IConfiguration section, string key)
    {
        var value = Require(section, key);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException($"Authentication configuration '{key}' must be an exact HTTPS URI.");
        }

        return uri;
    }

    private static Uri RequireHttpsRootUri(IConfiguration section, string key)
    {
        var uri = RequireHttpsUri(section, key);
        return uri.AbsolutePath == "/"
            ? uri
            : throw new InvalidOperationException($"Authentication configuration '{key}' must be an HTTPS origin.");
    }
}

/// <summary>Returns the current system time as an exact UTC instant.</summary>
public sealed class SystemAuthenticationClock : IAuthenticationClock
{
    /// <inheritdoc />
    public DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
}

/// <summary>Creates cryptographically unpredictable opaque authentication identities.</summary>
public sealed class CryptographicAuthenticationIdentityGenerator : IAuthenticationIdentityGenerator
{
    /// <inheritdoc />
    public UserId CreateUserId() => new($"user_{Guid.NewGuid():N}");

    /// <inheritdoc />
    public ExternalIdentityId CreateExternalIdentityId() => new($"identity_{Guid.NewGuid():N}");

    /// <inheritdoc />
    public UserSessionId CreateSessionId() => new($"session_{Guid.NewGuid():N}");
}

/// <summary>Tracks safe authentication dependency readiness.</summary>
public sealed class AuthenticationReadinessState
{
    /// <summary>Gets whether all authentication dependencies passed their startup probe.</summary>
    public bool IsReady { get; private set; }

    internal void MarkReady() => IsReady = true;
}

internal sealed class AuthenticationReadinessHostedService(
    IDataProtectionProvider dataProtectionProvider,
    SqlAuthenticationReadinessProbe persistenceProbe,
    KeyVaultExternalIdSignedAssertionProvider signedAssertionProvider,
    AuthenticationReadinessState readinessState) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var protector = dataProtectionProvider.CreateProtector("Slice5F.Readiness.v1");
        var protectedValue = protector.Protect("ready");
        if (!string.Equals(protector.Unprotect(protectedValue), "ready", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Authentication dependencies are unavailable.");
        }

        await persistenceProbe.VerifyAsync(cancellationToken);
        await signedAssertionProvider.VerifyAsync(cancellationToken);
        readinessState.MarkReady();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
