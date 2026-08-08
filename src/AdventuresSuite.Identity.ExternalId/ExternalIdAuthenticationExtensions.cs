using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using TheSimontonAdventures.Web.Authorization;

namespace AdventuresSuite.Identity.ExternalId;

/// <summary>Registers the production External ID protocol adapter without enabling endpoints or UI.</summary>
public static class ExternalIdAuthenticationExtensions
{
    /// <summary>The private production OIDC scheme.</summary>
    public const string Scheme = "AdventuresSuite.ExternalId";

    /// <summary>The internal sign-in state scheme; browser cookie behavior is completed in Slice 5E.</summary>
    public const string SessionScheme = "AdventuresSuite.ExternalId.Session";

    /// <summary>Adds hardened confidential-client OIDC protocol services.</summary>
    public static AuthenticationBuilder AddAdventuresSuiteExternalId(
        this AuthenticationBuilder builder,
        AuthenticationConfiguration configuration,
        IExternalIdClientCertificateSource certificateSource,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(certificateSource);
        if (configuration.Mode != AuthenticationMode.ExternalProvider)
        {
            throw new InvalidOperationException("External ID requires external-provider mode.");
        }

        X509Certificate2 certificate;
        try
        {
            certificate = certificateSource.Resolve(configuration.ClientCertificateReference!);
        }
        catch
        {
            throw new InvalidOperationException("The external identity client certificate is unavailable.");
        }

        ExternalIdClientCertificateValidator.Validate(certificate, utcNow);
        builder.Services.AddSingleton(configuration);
        builder.Services.AddScoped<ExternalIdSessionIssuer>();

        _ = builder.AddMicrosoftIdentityWebApp(
            options =>
            {
                options.Authority = configuration.Authority;
                options.ClientId = configuration.ClientId;
                options.CallbackPath = configuration.CallbackPath;
                options.SignedOutCallbackPath = configuration.SignedOutCallbackPath;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;
                options.SaveTokens = false;
                options.GetClaimsFromUserInfoEndpoint = false;
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = true;
                options.BackchannelTimeout = TimeSpan.FromSeconds(30);
                options.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(5);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    NameClaimType = "sub"
                };
                options.ProtocolValidator.RequireNonce = true;
                options.ProtocolValidator.RequireStateValidation = true;
                options.ClientCredentials =
                [
                    new CredentialDescription
                    {
                        SourceType = CredentialSource.Certificate,
                        Certificate = certificate
                    }
                ];
                options.Events = CreateEvents(configuration);
            },
            _ => { },
            Scheme,
            SessionScheme,
            subscribeToOpenIdConnectMiddlewareDiagnosticsEvents: false,
            displayName: null);
        builder.Services.PostConfigure<OpenIdConnectOptions>(Scheme, options =>
        {
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;
            options.SaveTokens = false;
            options.GetClaimsFromUserInfoEndpoint = false;
            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = true;
            options.BackchannelTimeout = TimeSpan.FromSeconds(30);
            options.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(5);
            options.ProtocolValidator.RequireNonce = true;
            options.ProtocolValidator.RequireStateValidation = true;
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidateAudience = true;
            options.TokenValidationParameters.ValidateIssuerSigningKey = true;
            options.TokenValidationParameters.ValidateLifetime = true;
            options.TokenValidationParameters.RequireSignedTokens = true;
            options.TokenValidationParameters.RequireExpirationTime = true;
            options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(2);
        });
        return builder;
    }

    private static OpenIdConnectEvents CreateEvents(AuthenticationConfiguration configuration) => new()
    {
        OnRedirectToIdentityProvider = context =>
        {
            if (!IsWorkspaceRequest(context.Request, configuration))
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status404NotFound;
            }

            return Task.CompletedTask;
        },
        OnMessageReceived = context =>
        {
            EnforceWorkspace(context.Request, context.Fail, configuration);
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            if (!IsWorkspaceRequest(context.Request, configuration))
            {
                context.Fail("Authentication failed.");
                return;
            }

            try
            {
                var issuer = context.HttpContext.RequestServices
                    .GetRequiredService<ExternalIdSessionIssuer>();
                var ticket = await issuer.EstablishSessionAsync(
                    context.Principal!,
                    context.HttpContext.RequestAborted);
                context.HttpContext.Features.Set(new ExternalIdSessionFeature(ticket));
            }
            catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                context.Fail("Authentication failed.");
            }
        },
        OnRemoteFailure = context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
    };

    private static void EnforceWorkspace(
        HttpRequest request,
        Action<string> fail,
        AuthenticationConfiguration configuration)
    {
        if (!IsWorkspaceRequest(request, configuration))
        {
            fail("Authentication failed.");
        }
    }

    /// <summary>Determines whether a request uses the exact configured private workspace origin.</summary>
    public static bool IsWorkspaceRequest(
        HttpRequest request,
        AuthenticationConfiguration configuration)
    {
        if (!Uri.TryCreate(configuration.WorkspaceOrigin, UriKind.Absolute, out var workspace))
        {
            return false;
        }

        return string.Equals(request.Scheme, workspace.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(request.Host.Host, workspace.IdnHost, StringComparison.OrdinalIgnoreCase)
            && (request.Host.Port ?? (request.IsHttps ? 443 : 80)) == workspace.Port;
    }
}

/// <summary>
/// Carries a newly established server session only within the validated callback request.
/// Slice 5E will consume this feature without persisting provider tokens or claims.
/// </summary>
public sealed record ExternalIdSessionFeature
{
    /// <summary>Initializes the transient successful-authentication feature.</summary>
    public ExternalIdSessionFeature(AuthenticationSessionTicket ticket) =>
        Ticket = ticket ?? throw new ArgumentNullException(nameof(ticket));

    /// <summary>Gets the application-controlled session ticket.</summary>
    public AuthenticationSessionTicket Ticket { get; }
}
