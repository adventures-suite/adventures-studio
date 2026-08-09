using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;

namespace AdventuresSuite.Identity.ExternalId;

/// <summary>Signs External ID client assertions with a non-exportable Azure Key Vault key.</summary>
public sealed class KeyVaultExternalIdSignedAssertionProvider : ICustomSignedAssertionProvider
{
    /// <summary>The stable Microsoft.Identity.Web custom-provider name.</summary>
    public const string ProviderName = "AdventuresSuiteKeyVaultSignedAssertion";

    private readonly CertificateClient certificateClient;
    private readonly TokenCredential credential;
    private readonly string certificateName;
    private KeyVaultClientAssertion? assertionProvider;

    /// <summary>Initializes a signer for one exact vault and certificate name.</summary>
    public KeyVaultExternalIdSignedAssertionProvider(
        Uri vaultUri,
        string certificateName,
        TokenCredential credential)
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

        this.certificateName = ValidateCertificateName(certificateName);
        this.credential = credential;
        certificateClient = new CertificateClient(vaultUri, credential);
    }

    /// <inheritdoc />
    public CredentialSource CredentialSource => CredentialSource.CustomSignedAssertion;

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public async Task LoadIfNeededAsync(
        CredentialDescription credentialDescription,
        CredentialSourceLoaderParameters? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(credentialDescription);
        assertionProvider ??= await CreateProviderAsync(CancellationToken.None);
        credentialDescription.CachedValue = assertionProvider;
        credentialDescription.Skip = false;
    }

    /// <summary>Proves that the certificate is valid and its Key Vault key can sign.</summary>
    public async Task VerifyAsync(CancellationToken cancellationToken = default)
    {
        assertionProvider ??= await CreateProviderAsync(cancellationToken);
        await assertionProvider.VerifySigningAsync(cancellationToken);
    }

    private async Task<KeyVaultClientAssertion> CreateProviderAsync(CancellationToken cancellationToken)
    {
        var certificate = (await certificateClient.GetCertificateAsync(
            certificateName,
            cancellationToken)).Value;
        if (certificate.KeyId is null)
            throw new InvalidOperationException("The external identity signing key is unavailable.");

        var publicCertificate = X509CertificateLoader.LoadCertificate(certificate.Cer);
        ExternalIdPublicCertificateValidator.Validate(publicCertificate, DateTimeOffset.UtcNow);
        return new KeyVaultClientAssertion(
            publicCertificate,
            new CryptographyClient(certificate.KeyId, credential));
    }

    private static string ValidateCertificateName(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 127
        && value == value.Trim()
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            ? value
            : throw new ArgumentException("A bounded Key Vault certificate name is required.", nameof(value));

    private sealed class KeyVaultClientAssertion(
        X509Certificate2 certificate,
        CryptographyClient cryptographyClient) : ClientAssertionProviderBase
    {
        protected override async Task<ClientAssertion> GetClientAssertionAsync(
            AssertionRequestOptions? assertionRequestOptions)
        {
            ArgumentNullException.ThrowIfNull(assertionRequestOptions);
            var issuedAt = DateTimeOffset.UtcNow;
            var expiresAt = issuedAt.AddMinutes(5);
            var header = EncodeJson(new Dictionary<string, object>
            {
                ["alg"] = "RS256",
                ["typ"] = "JWT",
                ["x5t"] = Base64Url(certificate.GetCertHash(HashAlgorithmName.SHA1))
            });
            var payload = EncodeJson(new Dictionary<string, object>
            {
                ["aud"] = assertionRequestOptions.TokenEndpoint,
                ["iss"] = assertionRequestOptions.ClientID,
                ["sub"] = assertionRequestOptions.ClientID,
                ["jti"] = Guid.NewGuid().ToString("D"),
                ["nbf"] = issuedAt.ToUnixTimeSeconds(),
                ["iat"] = issuedAt.ToUnixTimeSeconds(),
                ["exp"] = expiresAt.ToUnixTimeSeconds()
            });
            var unsigned = $"{header}.{payload}";
            var digest = SHA256.HashData(Encoding.ASCII.GetBytes(unsigned));
            var result = await cryptographyClient.SignAsync(
                SignatureAlgorithm.RS256,
                digest,
                assertionRequestOptions.CancellationToken);
            return new ClientAssertion($"{unsigned}.{Base64Url(result.Signature)}", expiresAt);
        }

        public async Task VerifySigningAsync(CancellationToken cancellationToken)
        {
            var digest = SHA256.HashData("AdventuresSuite.ExternalId.Readiness.v1"u8);
            var result = await cryptographyClient.SignAsync(SignatureAlgorithm.RS256, digest, cancellationToken);
            using var rsa = certificate.GetRSAPublicKey()
                ?? throw new InvalidOperationException("The external identity signing key is unavailable.");
            if (!rsa.VerifyHash(digest, result.Signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                throw new InvalidOperationException("The external identity signing key is unavailable.");
        }

        private static string EncodeJson(IReadOnlyDictionary<string, object> value) =>
            Base64Url(JsonSerializer.SerializeToUtf8Bytes(value));

        private static string Base64Url(byte[] value) =>
            Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

/// <summary>Validates public certificate metadata without requiring private-key export.</summary>
public static class ExternalIdPublicCertificateValidator
{
    private const string ClientAuthenticationEku = "1.3.6.1.5.5.7.3.2";

    /// <summary>Rejects unavailable, expired, or incorrectly purposed public certificates.</summary>
    public static void Validate(X509Certificate2 certificate, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (utcNow.Offset != TimeSpan.Zero
            || utcNow < certificate.NotBefore.ToUniversalTime()
            || utcNow >= certificate.NotAfter.ToUniversalTime())
            throw new InvalidOperationException("The external identity signing certificate is unavailable.");

        var keyUsage = certificate.Extensions.OfType<X509KeyUsageExtension>().SingleOrDefault();
        var enhancedUsage = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().SingleOrDefault();
        if (keyUsage is null
            || !keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature)
            || enhancedUsage is null
            || !enhancedUsage.EnhancedKeyUsages.Cast<Oid>().Any(
                usage => string.Equals(usage.Value, ClientAuthenticationEku, StringComparison.Ordinal)))
            throw new InvalidOperationException("The external identity signing certificate is unavailable.");
    }
}
