using System.Text.Json;
using AdventuresSuite.DatabaseMigrator;
using Azure.Core;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Verifies finite migration-container execution and evidence behavior.</summary>
public sealed class MigrationContainerModesTests
{
    [Fact]
    public async Task ExecutionChannelProducesBoundedSqlFreeEnvelope()
    {
        using var environment = ValidEnvironment();
        using var writer = new StringWriter();
        var original = Console.Out;
        Console.SetOut(writer);
        try
        {
            Assert.Equal(0, await MigrationContainerModes.VerifyExecutionChannelAsync(
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
        Assert.Equal("migration-job-completion",
            document.RootElement.GetProperty("eventName").GetString());
        var payload = document.RootElement.GetProperty("payload");
        Assert.Equal("ExecutionChannelComplete",
            payload.GetProperty("classification").GetString());
        Assert.False(payload.GetProperty("evidence").GetProperty("sqlAccessAttempted").GetBoolean());
        Assert.Matches("^[0-9a-f]{64}$",
            document.RootElement.GetProperty("envelopeChecksum").GetString());
    }

    [Fact]
    public async Task MutableImageReferenceIsRejectedBeforeAnySqlAccess()
    {
        using var environment = ValidEnvironment();
        environment.Set("ADVENTURESSUITE_IMAGE_DIGEST", "latest");
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => MigrationContainerModes.VerifyExecutionChannelAsync(new FixedTokenCredential(default)));
        Assert.Equal("Set a valid immutable ADVENTURESSUITE_IMAGE_DIGEST value.", exception.Message);
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
                "migration-job", "approved", "Approved"));
        Assert.Equal("The migration token tid claim is not approved.", exception.Message);
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

    private static EnvironmentScope ValidEnvironment()
    {
        var scope = new EnvironmentScope();
        scope.Set("ADVENTURESSUITE_MIGRATION_OPERATION_ID", "container-proof-0001");
        scope.Set("ADVENTURESSUITE_RELEASE_SHA", new string('a', 40));
        scope.Set("ADVENTURESSUITE_IMAGE_DIGEST", "sha256:" + new string('b', 64));
        scope.Set("ADVENTURESSUITE_MIGRATION_TENANT_ID", "00000000-0000-0000-0000-000000000001");
        scope.Set("ADVENTURESSUITE_MIGRATION_PRINCIPAL_ID", "00000000-0000-0000-0000-000000000002");
        scope.Set("ADVENTURESSUITE_MIGRATION_PRINCIPAL_CLIENT_ID", "00000000-0000-0000-0000-000000000003");
        scope.Set("ADVENTURESSUITE_MIGRATION_PRINCIPAL_NAME", "migration-job-proof");
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
