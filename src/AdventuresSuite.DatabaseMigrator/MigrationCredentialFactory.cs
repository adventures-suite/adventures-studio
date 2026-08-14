using Azure.Core;
using Azure.Identity;

namespace AdventuresSuite.DatabaseMigrator;

/// <summary>Selects exactly one reviewed credential source for a migration process.</summary>
internal static class MigrationCredentialFactory
{
    internal const string ModeVariable = "ADVENTURESSUITE_MIGRATION_CREDENTIAL_MODE";

    internal static MigrationCredentialSelection Create(Guid tenantId, Guid clientId)
    {
        var mode = Environment.GetEnvironmentVariable(ModeVariable);
        return mode switch
        {
            "azure-managed-identity" => new(
                MigrationCredentialMode.AzureManagedIdentity,
                new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId.ToString()))),
            "github-oidc-azure-cli" => new(
                MigrationCredentialMode.GitHubOidcAzureCli,
                new AzureCliCredential(new AzureCliCredentialOptions { TenantId = tenantId.ToString() })),
            _ => throw new InvalidOperationException(
                $"Set {ModeVariable} to one reviewed credential mode; fallback is prohibited.")
        };
    }
}

internal enum MigrationCredentialMode
{
    AzureManagedIdentity,
    GitHubOidcAzureCli
}

internal sealed record MigrationCredentialSelection(
    MigrationCredentialMode Mode,
    TokenCredential Credential);
