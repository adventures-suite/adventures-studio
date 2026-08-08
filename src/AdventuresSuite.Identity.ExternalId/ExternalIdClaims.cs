using System.Security.Claims;
using TheSimontonAdventures.Web.Authorization;

namespace AdventuresSuite.Identity.ExternalId;

/// <summary>Maps only immutable OIDC identity claims after protocol validation succeeds.</summary>
internal static class ExternalIdClaims
{
    /// <summary>Creates an exact provider identity without using mutable profile claims.</summary>
    public static ExternalIdentityKey Map(
        ClaimsPrincipal principal,
        ExternalIdentityProviderId providerId)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (providerId == default)
        {
            throw new ArgumentException("A provider identity is required.", nameof(providerId));
        }

        var issuer = GetSingleClaim(principal, "iss");
        var subject = GetSingleClaim(principal, "sub");
        return new ExternalIdentityKey(
            providerId,
            new ExternalIdentityIssuer(issuer),
            new ExternalIdentitySubject(subject));
    }

    private static string GetSingleClaim(ClaimsPrincipal principal, string claimType)
    {
        var values = principal.Claims
            .Where(claim => string.Equals(claim.Type, claimType, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ToArray();
        if (values.Length != 1)
        {
            throw new InvalidOperationException("The external identity could not be validated.");
        }

        return values[0];
    }
}
