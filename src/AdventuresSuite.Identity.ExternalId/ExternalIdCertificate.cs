using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AdventuresSuite.Identity.ExternalId;

/// <summary>Resolves certificate material without coupling authentication to its storage provider.</summary>
public interface IExternalIdClientCertificateSource
{
    /// <summary>Resolves the configured certificate reference.</summary>
    X509Certificate2 Resolve(string certificateReference);
}

/// <summary>Validates a certificate before it can authenticate the confidential client.</summary>
public static class ExternalIdClientCertificateValidator
{
    private const string ClientAuthenticationEku = "1.3.6.1.5.5.7.3.2";

    /// <summary>Rejects unavailable, unusable, or incorrectly purposed certificates.</summary>
    public static void Validate(X509Certificate2? certificate, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (utcNow.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Certificate validation time must be UTC.", nameof(utcNow));
        }

        if (!certificate.HasPrivateKey
            || utcNow < certificate.NotBefore.ToUniversalTime()
            || utcNow >= certificate.NotAfter.ToUniversalTime())
        {
            throw new InvalidOperationException("The external identity client certificate is unavailable.");
        }

        var keyUsage = certificate.Extensions.OfType<X509KeyUsageExtension>().SingleOrDefault();
        if (keyUsage is null
            || !keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
        {
            throw new InvalidOperationException("The external identity client certificate is unavailable.");
        }

        var enhancedUsage = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().SingleOrDefault();
        if (enhancedUsage is null
            || !enhancedUsage.EnhancedKeyUsages.Cast<Oid>().Any(
                usage => string.Equals(usage.Value, ClientAuthenticationEku, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The external identity client certificate is unavailable.");
        }
    }
}
