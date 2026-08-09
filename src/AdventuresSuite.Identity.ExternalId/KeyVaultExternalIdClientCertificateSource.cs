using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Security.KeyVault.Certificates;

namespace AdventuresSuite.Identity.ExternalId;

/// <summary>
/// Loads the configured confidential-client certificate from one approved
/// Azure Key Vault using the workload's Azure credential.
/// </summary>
public sealed class KeyVaultExternalIdClientCertificateSource : IExternalIdClientCertificateSource
{
    private readonly CertificateClient certificateClient;

    /// <summary>Initializes a certificate source for one exact vault URI.</summary>
    public KeyVaultExternalIdClientCertificateSource(Uri vaultUri, TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(vaultUri);
        ArgumentNullException.ThrowIfNull(credential);
        if (!vaultUri.IsAbsoluteUri
            || vaultUri.Scheme != Uri.UriSchemeHttps
            || vaultUri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(vaultUri.Query)
            || !string.IsNullOrEmpty(vaultUri.Fragment)
            || !string.IsNullOrEmpty(vaultUri.UserInfo))
        {
            throw new ArgumentException("An exact HTTPS Key Vault URI is required.", nameof(vaultUri));
        }

        certificateClient = new CertificateClient(vaultUri, credential);
    }

    /// <inheritdoc />
    public X509Certificate2 Resolve(string certificateReference)
    {
        if (string.IsNullOrWhiteSpace(certificateReference)
            || certificateReference.Length > 127
            || certificateReference != certificateReference.Trim()
            || certificateReference.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException(
                "A bounded Key Vault certificate name is required.",
                nameof(certificateReference));
        }

        return certificateClient.DownloadCertificate(new DownloadCertificateOptions(certificateReference)
        {
            KeyStorageFlags = X509KeyStorageFlags.EphemeralKeySet
        }).Value;
    }
}
