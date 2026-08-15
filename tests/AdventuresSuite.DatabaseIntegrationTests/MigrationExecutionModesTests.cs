using System.Text.Json;
using AdventuresSuite.DatabaseMigrator;
using Azure.Core;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Verifies finite private SQL migration execution and evidence behavior.</summary>
public sealed class MigrationExecutionModesTests
{
    [Fact]
    public void SqlConnectionFactoryCarriesOnlyTheExplicitCredentialMode()
    {
        const string connectionString =
            "Server=approved.database.windows.net;Database=Approved;Encrypt=True";
        const string sensitiveToken = "reviewed-token-must-not-be-emitted";
        var token = new AccessToken(sensitiveToken, DateTimeOffset.UtcNow.AddMinutes(5));

        using var writer = new StringWriter();
        using var errorWriter = new StringWriter();
        var original = Console.Out;
        var originalError = Console.Error;
        Console.SetOut(writer);
        Console.SetError(errorWriter);
        try
        {
            var azureCliFactory = MigrationSqlConnectionFactory.Create(
                connectionString, MigrationCredentialMode.GitHubOidcAzureCli, token);
            using var azureCliConnection = azureCliFactory.CreateConnection();
            Assert.Equal(sensitiveToken, azureCliConnection.AccessToken);

            var managedIdentityFactory = MigrationSqlConnectionFactory.Create(
                connectionString, MigrationCredentialMode.AzureManagedIdentity, token);
            using var managedIdentityConnection = managedIdentityFactory.CreateConnection();
            Assert.Null(managedIdentityConnection.AccessToken);
        }
        finally
        {
            Console.SetOut(original);
            Console.SetError(originalError);
        }

        Assert.DoesNotContain(sensitiveToken, writer.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveToken, errorWriter.ToString(), StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => MigrationSqlConnectionFactory.Create(
            connectionString,
            MigrationCredentialMode.GitHubOidcAzureCli,
            default));
    }

    [Fact]
    public void CredentialModesAreExplicitAndNeverFallBack()
    {
        using var environment = ValidEnvironment();
        environment.Set(MigrationCredentialFactory.ModeVariable, "azure-managed-identity");
        var managedIdentity = MigrationCredentialFactory.Create(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(MigrationCredentialMode.AzureManagedIdentity, managedIdentity.Mode);
        Assert.IsType<Azure.Identity.ManagedIdentityCredential>(managedIdentity.Credential);
        environment.Set(MigrationCredentialFactory.ModeVariable, "github-oidc-azure-cli");
        var azureCli = MigrationCredentialFactory.Create(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(MigrationCredentialMode.GitHubOidcAzureCli, azureCli.Mode);
        Assert.IsType<Azure.Identity.AzureCliCredential>(azureCli.Credential);
        environment.Set(MigrationCredentialFactory.ModeVariable, "");
        Assert.Throws<InvalidOperationException>(() =>
            MigrationCredentialFactory.Create(Guid.NewGuid(), Guid.NewGuid()));
        environment.Set(MigrationCredentialFactory.ModeVariable, "default");
        Assert.Throws<InvalidOperationException>(() =>
            MigrationCredentialFactory.Create(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task ExecutionChannelProducesBoundedSqlFreeEnvelope()
    {
        using var environment = ValidEnvironment();
        using var writer = new StringWriter();
        var original = Console.Out;
        Console.SetOut(writer);
        try
        {
            Assert.Equal(0, await MigrationExecutionModes.VerifyExecutionChannelAsync(
                new FixedTokenCredential(Token(new Dictionary<string, string>
                {
                    ["tid"] = "00000000-0000-0000-0000-000000000001",
                    ["oid"] = "00000000-0000-0000-0000-000000000002",
                    ["appid"] = "00000000-0000-0000-0000-000000000003",
                    ["aud"] = "https://management.azure.com/"
                }))));
        }
        finally
        {
            Console.SetOut(original);
        }

        var output = writer.ToString();
        Assert.DoesNotContain("connection string", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT ", output, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(output);
        Assert.Equal("private-sql-migration-completion",
            document.RootElement.GetProperty("eventName").GetString());
        var payload = document.RootElement.GetProperty("payload");
        Assert.Equal("ExecutionChannelComplete",
            payload.GetProperty("classification").GetString());
        Assert.False(payload.GetProperty("evidence").GetProperty("sqlAccessAttempted").GetBoolean());
        Assert.Matches("^[0-9a-f]{64}$",
            document.RootElement.GetProperty("envelopeChecksum").GetString());
    }

    [Fact]
    public async Task MalformedPackageHashIsRejectedBeforeAnySqlAccess()
    {
        using var environment = ValidEnvironment();
        environment.Set("ADVENTURESSUITE_MIGRATION_PACKAGE_SHA256", "latest");
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => MigrationExecutionModes.VerifyExecutionChannelAsync(new FixedTokenCredential(default)));
        Assert.Equal("Set a valid ADVENTURESSUITE_MIGRATION_PACKAGE_SHA256 value.", exception.Message);
    }

    [Fact]
    public async Task MismatchedCatalogHashIsRejectedBeforeAnySqlAccess()
    {
        using var environment = ValidEnvironment();
        environment.Set("ADVENTURESSUITE_MIGRATION_CATALOG_SHA256", new string('d', 64));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => MigrationExecutionModes.VerifyExecutionChannelAsync(new FixedTokenCredential(default)));
        Assert.Equal("The migration catalog checksum does not match the embedded catalog.", exception.Message);
    }

    [Fact]
    public async Task WrongTenantIsRejectedBeforeSqlConnection()
    {
        var token = Token(new Dictionary<string, string>
        {
            ["tid"] = "00000000-0000-0000-0000-000000000099",
            ["oid"] = "00000000-0000-0000-0000-000000000002",
            ["appid"] = "00000000-0000-0000-0000-000000000003",
            ["aud"] = "https://database.windows.net/"
        });
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MigrationIdentityValidator.ValidateAsync(token,
                "Server=approved.database.windows.net;Database=Approved;Authentication=Active Directory Managed Identity;User ID=00000000-0000-0000-0000-000000000003",
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Guid.Parse("00000000-0000-0000-0000-000000000003"),
                "migration-runner", "approved", "Approved"));
        Assert.Equal("The migration token tid claim is not approved.", exception.Message);
    }

    [Theory]
    [InlineData("github-oidc-azure-cli", "https://database.windows.net", "https://database.windows.net/")]
    [InlineData("azure-managed-identity", "https://database.windows.net/", "https://database.windows.net")]
    public void SqlTokenAudienceIsExactForCredentialMode(
        string credentialModeName, string expectedAudience, string otherModeAudience)
    {
        var credentialMode = CredentialMode(credentialModeName);
        var claims = ValidSqlClaims(expectedAudience);
        var evidence = MigrationIdentityValidator.ValidateSqlWorkloadToken(
            Token(claims),
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            credentialMode);
        Assert.Equal(expectedAudience, evidence.Audience);

        claims["aud"] = otherModeAudience;
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MigrationIdentityValidator.ValidateSqlWorkloadToken(
                Token(claims), evidence.TenantId, evidence.ObjectId, evidence.ClientId, credentialMode));
        Assert.Equal("The migration token aud claim is not approved.", exception.Message);
    }

    [Theory]
    [InlineData("github-oidc-azure-cli", null)]
    [InlineData("github-oidc-azure-cli", "https://database.windows.net/.default")]
    [InlineData("github-oidc-azure-cli", "https://DATABASE.windows.net")]
    [InlineData("github-oidc-azure-cli", "https://database.windows.net//")]
    [InlineData("azure-managed-identity", null)]
    [InlineData("azure-managed-identity", "https://database.windows.net/.default")]
    [InlineData("azure-managed-identity", "https://DATABASE.windows.net/")]
    [InlineData("azure-managed-identity", "https://management.azure.com/")]
    public void SqlTokenAudienceRejectsMissingSubstitutedOrUnrelatedClaims(
        string credentialModeName, string? audience)
    {
        var credentialMode = CredentialMode(credentialModeName);
        var claims = ValidSqlClaims(MigrationIdentityValidator.AzureCliSqlAudience);
        if (audience is null) claims.Remove("aud"); else claims["aud"] = audience;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MigrationIdentityValidator.ValidateSqlWorkloadToken(
                Token(claims),
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Guid.Parse("00000000-0000-0000-0000-000000000003"),
                credentialMode));
        Assert.Equal("The migration token aud claim is not approved.", exception.Message);
    }

    [Theory]
    [InlineData("github-oidc-azure-cli", "tid", "The migration token tid claim is not approved.")]
    [InlineData("github-oidc-azure-cli", "oid", "The migration token oid claim is not approved.")]
    [InlineData("github-oidc-azure-cli", "appid", "The migration token client identity is not approved.")]
    [InlineData("github-oidc-azure-cli", "aud", "The migration token aud claim is not approved.")]
    [InlineData("azure-managed-identity", "tid", "The migration token tid claim is not approved.")]
    [InlineData("azure-managed-identity", "oid", "The migration token oid claim is not approved.")]
    [InlineData("azure-managed-identity", "appid", "The migration token client identity is not approved.")]
    [InlineData("azure-managed-identity", "aud", "The migration token aud claim is not approved.")]
    public void SqlTokenValidationRejectsEveryMissingRequiredClaim(
        string credentialModeName, string missingClaim, string expectedMessage)
    {
        var credentialMode = CredentialMode(credentialModeName);
        var audience = credentialMode == MigrationCredentialMode.GitHubOidcAzureCli
            ? MigrationIdentityValidator.AzureCliSqlAudience
            : MigrationIdentityValidator.ManagedIdentitySqlAudience;
        var claims = ValidSqlClaims(audience);
        claims.Remove(missingClaim);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MigrationIdentityValidator.ValidateSqlWorkloadToken(
                Token(claims),
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Guid.Parse("00000000-0000-0000-0000-000000000003"),
                credentialMode));
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData("oid", "00000000-0000-0000-0000-000000000099", "The migration token oid claim is not approved.")]
    [InlineData("appid", "00000000-0000-0000-0000-000000000099", "The migration token client identity is not approved.")]
    [InlineData("aud", "https://database.windows.net/", "The migration token aud claim is not approved.")]
    public void ArmIdentityProofRejectsMismatchedIdentityMetadata(
        string claim, string value, string expectedMessage)
    {
        var claims = new Dictionary<string, string>
        {
            ["tid"] = "00000000-0000-0000-0000-000000000001",
            ["oid"] = "00000000-0000-0000-0000-000000000002",
            ["appid"] = "00000000-0000-0000-0000-000000000003",
            ["aud"] = "https://management.azure.com/"
        };
        claims[claim] = value;
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MigrationIdentityValidator.ValidateWorkloadToken(Token(claims),
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Guid.Parse("00000000-0000-0000-0000-000000000003"),
                "https://management.azure.com/"));
        Assert.Equal(expectedMessage, exception.Message);
    }

    private static AccessToken Token(IReadOnlyDictionary<string, string> claims)
    {
        static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new AccessToken(
            $"{Encode("{\"alg\":\"none\"}")}.{Encode(JsonSerializer.Serialize(claims))}.",
            DateTimeOffset.UtcNow.AddMinutes(5));
    }

    private static Dictionary<string, string> ValidSqlClaims(string audience) => new()
    {
        ["tid"] = "00000000-0000-0000-0000-000000000001",
        ["oid"] = "00000000-0000-0000-0000-000000000002",
        ["appid"] = "00000000-0000-0000-0000-000000000003",
        ["aud"] = audience
    };

    private static MigrationCredentialMode CredentialMode(string name) => name switch
    {
        "github-oidc-azure-cli" => MigrationCredentialMode.GitHubOidcAzureCli,
        "azure-managed-identity" => MigrationCredentialMode.AzureManagedIdentity,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static EnvironmentScope ValidEnvironment()
    {
        var scope = new EnvironmentScope();
        scope.Set("ADVENTURESSUITE_MIGRATION_OPERATION_ID", "private-sql-proof-0001");
        scope.Set("ADVENTURESSUITE_RELEASE_SHA", new string('a', 40));
        scope.Set("ADVENTURESSUITE_MIGRATION_PACKAGE_SHA256", new string('b', 64));
        scope.Set("ADVENTURESSUITE_MIGRATION_CATALOG_SHA256",
            MigrationCatalog.CalculateOrderedCatalogSha256(typeof(MigrationCatalog).Assembly));
        scope.Set("ADVENTURESSUITE_MIGRATION_TENANT_ID", "00000000-0000-0000-0000-000000000001");
        scope.Set("ADVENTURESSUITE_MIGRATION_PRINCIPAL_ID", "00000000-0000-0000-0000-000000000002");
        scope.Set("ADVENTURESSUITE_MIGRATION_PRINCIPAL_CLIENT_ID", "00000000-0000-0000-0000-000000000003");
        scope.Set("ADVENTURESSUITE_MIGRATION_PRINCIPAL_NAME", "migration-runner-proof");
        return scope;
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> original = new(StringComparer.Ordinal);

        internal void Set(string name, string value)
        {
            if (!original.ContainsKey(name)) original[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            foreach (var value in original)
                Environment.SetEnvironmentVariable(value.Key, value.Value);
        }
    }

    private sealed class FixedTokenCredential(AccessToken token) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext,
            CancellationToken cancellationToken) => token;

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext,
            CancellationToken cancellationToken) => ValueTask.FromResult(token);
    }
}
