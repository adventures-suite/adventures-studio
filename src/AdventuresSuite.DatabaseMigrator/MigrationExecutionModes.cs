using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;

namespace AdventuresSuite.DatabaseMigrator;

/// <summary>Provides finite, reviewed private SQL migration execution modes.</summary>
internal static partial class MigrationExecutionModes
{
    internal static Task<int> VerifyExecutionChannelAsync()
    {
        var clientId = RequireGuid("ADVENTURESSUITE_MIGRATION_PRINCIPAL_CLIENT_ID");
        var credential = new ManagedIdentityCredential(
            ManagedIdentityId.FromUserAssignedClientId(clientId.ToString()));
        return VerifyExecutionChannelAsync(credential);
    }

    internal static async Task<int> VerifyExecutionChannelAsync(TokenCredential credential)
    {
        var context = ReadContext(requireSqlTarget: false);
        AccessToken token = await credential.GetTokenAsync(
            new TokenRequestContext(["https://management.azure.com/.default"]),
            CancellationToken.None);
        var identity = MigrationIdentityValidator.ValidateWorkloadToken(
            token, context.TenantId, context.ObjectId, context.ClientId,
            "https://management.azure.com/");
        WriteEnvelope(context, "ExecutionChannelComplete", 0, new
        {
            sqlAccessAttempted = false,
            environmentValidated = true,
            identity = new
            {
                identity.TenantId,
                identity.ObjectId,
                identity.ClientId,
                identity.Audience
            }
        });
        return 0;
    }

    internal static async Task<int> CaptureMigrationStateAsync(string connectionString)
    {
        var context = ReadContext(requireSqlTarget: true);
        var identity = await ValidateIdentityAsync(context, connectionString);
        var state = await MigrationOperationalState.CaptureAsync(connectionString);
        WriteEnvelope(context, "StateCaptured", 0, new
        {
            identity = SafeIdentity(identity),
            journalClassification = MigrationOperationalState.Classify(state.Journal).ToString(),
            state.Journal,
            state.RelevantObjects,
            state.CompanionPermissions,
            state.ApplicationDataSignatures,
            state.ApplicationFingerprint
        });
        return 0;
    }

    internal static async Task<int> VerifyMigrationStateAsync(string connectionString)
    {
        var context = ReadContext(requireSqlTarget: true);
        var identity = await ValidateIdentityAsync(context, connectionString);
        var state = await MigrationOperationalState.CaptureAsync(connectionString);
        var expectedFingerprint = Require("ADVENTURESSUITE_EXPECTED_APPLICATION_FINGERPRINT");
        var journal = MigrationOperationalState.Classify(state.Journal);
        var verified = journal == MigrationJournalOutcome.At0008
            && MigrationOperationRunner.VerifyExpectedPostState(state)
            && string.Equals(expectedFingerprint, state.ApplicationFingerprint, StringComparison.Ordinal);
        var exitCode = verified ? 0 : 1;
        WriteEnvelope(context, verified ? "Complete" : "Unexpected", exitCode, new
        {
            identity = SafeIdentity(identity),
            journalClassification = journal.ToString(),
            schemaAndPermissionsVerified = MigrationOperationRunner.VerifyExpectedPostState(state),
            fingerprintMatched = string.Equals(expectedFingerprint, state.ApplicationFingerprint, StringComparison.Ordinal)
        });
        return exitCode;
    }

    private static async Task<MigrationIdentityEvidence> ValidateIdentityAsync(
        MigrationOperationContext context, string connectionString)
    {
        var credential = new ManagedIdentityCredential(
            ManagedIdentityId.FromUserAssignedClientId(context.ClientId.ToString()));
        AccessToken token = await credential.GetTokenAsync(
            new TokenRequestContext(["https://database.windows.net/.default"]));
        return await MigrationIdentityValidator.ValidateAsync(
            token, connectionString, context.TenantId, context.ObjectId, context.ClientId,
            context.PrincipalName, context.SqlServer!, context.SqlDatabase!);
    }

    private static object SafeIdentity(MigrationIdentityEvidence identity) => new
    {
        identity.TenantId,
        identity.ObjectId,
        identity.ClientId,
        identity.SqlPrincipalAlias,
        identity.Server,
        identity.Database
    };

    private static MigrationOperationContext ReadContext(bool requireSqlTarget)
    {
        var operationId = Require("ADVENTURESSUITE_MIGRATION_OPERATION_ID");
        if (!OperationIdPattern().IsMatch(operationId))
            throw new InvalidOperationException("The migration operation identifier is invalid.");
        var releaseSha = RequireHex("ADVENTURESSUITE_RELEASE_SHA", 40);
        var packageSha256 = RequireHex("ADVENTURESSUITE_MIGRATION_PACKAGE_SHA256", 64);
        var catalogSha256 = RequireHex("ADVENTURESSUITE_MIGRATION_CATALOG_SHA256", 64);
        if (!string.Equals(catalogSha256,
            MigrationCatalog.CalculateOrderedCatalogSha256(typeof(MigrationCatalog).Assembly),
            StringComparison.Ordinal))
            throw new InvalidOperationException("The migration catalog checksum does not match the embedded catalog.");
        return new(
            operationId, releaseSha, packageSha256, catalogSha256,
            DateTimeOffset.UtcNow,
            RequireGuid("ADVENTURESSUITE_MIGRATION_TENANT_ID"),
            RequireGuid("ADVENTURESSUITE_MIGRATION_PRINCIPAL_ID"),
            RequireGuid("ADVENTURESSUITE_MIGRATION_PRINCIPAL_CLIENT_ID"),
            Require("ADVENTURESSUITE_MIGRATION_PRINCIPAL_NAME"),
            requireSqlTarget ? Require("ADVENTURESSUITE_SQL_SERVER") : null,
            requireSqlTarget ? Require("ADVENTURESSUITE_SQL_DATABASE") : null);
    }

    private static void WriteEnvelope(
        MigrationOperationContext context, string classification, int exitCode, object evidence)
        => WriteCompletionEnvelope(context.OperationId, context.ReleaseSha, context.PackageSha256,
            context.CatalogSha256, context.StartedAt, classification, exitCode, evidence);

    internal static void WriteCompletionEnvelope(
        string operationId,
        string releaseSha,
        string packageSha256,
        string catalogSha256,
        DateTimeOffset startedAt,
        string classification,
        int exitCode,
        object evidence)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var payload = new
        {
            schemaVersion = 1,
            operationId,
            releaseSha,
            packageSha256,
            orderedMigrationCatalogSha256 = catalogSha256,
            processStartedAt = startedAt,
            processCompletedAt = completedAt,
            classification,
            processExitCode = exitCode,
            evidence,
            cleanupFinalState = "ProcessExiting"
        };
        var canonical = JsonSerializer.Serialize(payload);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            eventName = "private-sql-migration-completion",
            payload,
            envelopeChecksum = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant()
        }));
    }

    private static string Require(string name) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))
            ? Environment.GetEnvironmentVariable(name)!.Trim()
            : throw new InvalidOperationException($"Set {name} for the reviewed migration operation.");

    private static Guid RequireGuid(string name) =>
        Guid.TryParse(Require(name), out var value)
            ? value : throw new InvalidOperationException($"Set a valid {name} value.");

    private static string RequireHex(string name, int length)
    {
        var value = Require(name);
        return value.Length == length && value.All(Uri.IsHexDigit)
            ? value.ToLowerInvariant()
            : throw new InvalidOperationException($"Set a valid {name} value.");
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{7,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex OperationIdPattern();

}

internal sealed record MigrationOperationContext(
    string OperationId,
    string ReleaseSha,
    string PackageSha256,
    string CatalogSha256,
    DateTimeOffset StartedAt,
    Guid TenantId,
    Guid ObjectId,
    Guid ClientId,
    string PrincipalName,
    string? SqlServer,
    string? SqlDatabase);
