using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;

namespace AdventuresSuite.DatabaseMigrator;

/// <summary>Provides finite, reviewed Container Apps Job execution modes.</summary>
internal static partial class MigrationContainerModes
{
    internal static int VerifyExecutionChannel()
    {
        var context = ReadContext(requireSqlTarget: false);
        WriteEnvelope(context, "ExecutionChannelComplete", 0, new
        {
            sqlAccessAttempted = false,
            environmentValidated = true
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
        ContainerOperationContext context, string connectionString)
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

    private static ContainerOperationContext ReadContext(bool requireSqlTarget)
    {
        var operationId = Require("ADVENTURESSUITE_MIGRATION_OPERATION_ID");
        if (!OperationIdPattern().IsMatch(operationId))
            throw new InvalidOperationException("The migration operation identifier is invalid.");
        var releaseSha = RequireHex("ADVENTURESSUITE_RELEASE_SHA", 40);
        var imageDigest = RequireImageDigest("ADVENTURESSUITE_IMAGE_DIGEST");
        var artifactChecksum = RequireHex("ADVENTURESSUITE_ARTIFACT_SHA256", 64);
        return new(
            operationId, releaseSha, imageDigest, artifactChecksum,
            DateTimeOffset.UtcNow,
            RequireGuid("ADVENTURESSUITE_MIGRATION_TENANT_ID"),
            RequireGuid("ADVENTURESSUITE_MIGRATION_PRINCIPAL_ID"),
            RequireGuid("ADVENTURESSUITE_MIGRATION_PRINCIPAL_CLIENT_ID"),
            Require("ADVENTURESSUITE_MIGRATION_PRINCIPAL_NAME"),
            requireSqlTarget ? Require("ADVENTURESSUITE_SQL_SERVER") : null,
            requireSqlTarget ? Require("ADVENTURESSUITE_SQL_DATABASE") : null);
    }

    private static void WriteEnvelope(
        ContainerOperationContext context, string classification, int exitCode, object evidence)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var payload = new
        {
            schemaVersion = 1,
            operationId = context.OperationId,
            releaseSha = context.ReleaseSha,
            imageDigest = context.ImageDigest,
            artifactChecksum = context.ArtifactChecksum,
            processStartedAt = context.StartedAt,
            processCompletedAt = completedAt,
            classification,
            processExitCode = exitCode,
            evidence,
            cleanupFinalState = "ProcessExiting"
        };
        var canonical = JsonSerializer.Serialize(payload);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            eventName = "migration-job-completion",
            payload,
            envelopeChecksum = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant()
        }));
    }

    private static string Require(string name) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))
            ? Environment.GetEnvironmentVariable(name)!.Trim()
            : throw new InvalidOperationException($"Set {name} for the reviewed migration job.");

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

    private static string RequireImageDigest(string name)
    {
        var value = Require(name);
        return DigestPattern().IsMatch(value)
            ? value : throw new InvalidOperationException($"Set a valid immutable {name} value.");
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{7,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex OperationIdPattern();

    [GeneratedRegex("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex DigestPattern();
}

internal sealed record ContainerOperationContext(
    string OperationId,
    string ReleaseSha,
    string ImageDigest,
    string ArtifactChecksum,
    DateTimeOffset StartedAt,
    Guid TenantId,
    Guid ObjectId,
    Guid ClientId,
    string PrincipalName,
    string? SqlServer,
    string? SqlDatabase);
