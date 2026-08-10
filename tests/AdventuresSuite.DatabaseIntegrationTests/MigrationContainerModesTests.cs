using System.Text.Json;
using AdventuresSuite.DatabaseMigrator;
using Azure.Core;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Verifies finite migration-container execution and evidence behavior.</summary>
public sealed class MigrationContainerModesTests
{
    [Fact]
    public void ExecutionChannelProducesBoundedSqlFreeEnvelope()
    {
        using var environment = ValidEnvironment();
        using var writer = new StringWriter();
        var original = Console.Out;
        Console.SetOut(writer);
        try
        {
            Assert.Equal(0, MigrationContainerModes.VerifyExecutionChannel());
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
    public void MutableImageReferenceIsRejectedBeforeAnySqlAccess()
    {
        using var environment = ValidEnvironment();
        environment.Set("ADVENTURESSUITE_IMAGE_DIGEST", "latest");
        var exception = Assert.Throws<InvalidOperationException>(
            () => _ = MigrationContainerModes.VerifyExecutionChannel());
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
        scope.Set("ADVENTURESSUITE_ARTIFACT_SHA256", new string('c', 64));
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
}
