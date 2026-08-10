using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AdventuresSuite.Api;

/// <summary>Adds the stable Companion identity and closed OAuth contract to OpenAPI.</summary>
public sealed class CompanionOpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info.Title = "AdventuresCompanion API";
        document.Info.Version = "v1";
        document.Info.Description = "Deterministic fictional read contract. Production OAuth, data, protected Resource delivery, and infrastructure activation remain closed.";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes["companionOAuth"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "Browser-delegated authorization code with PKCE. Environment-specific provider activation remains closed.",
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri("https://identity.adventuressuite.invalid/oauth2/authorize"),
                    TokenUrl = new Uri("https://identity.adventuressuite.invalid/oauth2/token"),
                    Scopes = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["Companion.Access"] = "Read the current traveler's authorized Companion projections."
                    }
                }
            }
        };
        document.Security ??= new List<OpenApiSecurityRequirement>();
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("companionOAuth", document, null)] =
                new List<string> { "Companion.Access" }
        });
        return Task.CompletedTask;
    }
}
