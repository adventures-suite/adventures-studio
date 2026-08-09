using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Web;

namespace AdventuresSuite.Identity.ExternalId;

/// <summary>
/// Supplies short-lived client assertions for both MSAL and the initial OIDC authorization-code exchange.
/// </summary>
public interface IExternalIdClientAssertionProvider : ICustomSignedAssertionProvider
{
    /// <summary>Creates an assertion scoped to one exact confidential client and token endpoint.</summary>
    Task<string> CreateClientAssertionAsync(
        string clientId,
        Uri tokenEndpoint,
        CancellationToken cancellationToken = default);
}
