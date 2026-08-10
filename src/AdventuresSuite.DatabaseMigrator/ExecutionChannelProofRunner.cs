using System.Text.Json;
using System.Text.RegularExpressions;

namespace AdventuresSuite.DatabaseMigrator;

/// <summary>Emits a harmless, SQL-free proof that the reviewed executable started.</summary>
internal static partial class ExecutionChannelProofRunner
{
    internal static int Run()
    {
        var operationId = Require("ADVENTURESSUITE_MIGRATION_OPERATION_ID");
        var releaseSha = RequireHex("ADVENTURESSUITE_RELEASE_SHA", 40);
        var artifactChecksum = RequireHex("ADVENTURESSUITE_ARTIFACT_SHA256", 64);
        if (!OperationIdPattern().IsMatch(operationId))
            throw new InvalidOperationException("The execution-channel operation identifier is invalid.");

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            eventName = "execution-channel-payload-executed",
            operationId,
            releaseSha,
            artifactChecksum,
            executedAt = DateTimeOffset.UtcNow,
            sqlAccessAttempted = false
        }));
        return 0;
    }

    private static string Require(string name) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))
            ? Environment.GetEnvironmentVariable(name)!.Trim()
            : throw new InvalidOperationException($"Set {name} for execution-channel verification.");

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
