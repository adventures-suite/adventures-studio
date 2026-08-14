# Private Azure SQL Migration Execution

**Status:** GitHub-hosted VNet runner selected; networking not configured

The authoritative migration mechanism is the existing, standalone DbUp
executable packaged as a deterministic self-contained `linux-x64` artifact.
The Azure Container Apps Job and Azure Container Registry design is
superseded. It has no active workflow, image, or executable IaC surface.

## Preserved database contract

The package embeds the immutable ordered SQL scripts and uses
`dbo.AdventuresSuiteSchemaVersions`, one transaction per script, and the
zero-wait `AdventuresSuite.DatabaseMigrator` application lock. The reviewed
operation captures and classifies pre/post journal state, schema and permission
state, and application-data fingerprints. A partial commit is repair-forward:
it is never automatically retried, rolled back destructively, or hidden by
journal edits. Migrations never run from web or API startup. This decision does
not convert the migration model to DACPAC.

## Package authority

Only the protected-main workflow may produce an executable release artifact.
Its evidence binds the full source SHA, package SHA-256, ordered migration
catalog SHA-256, exact SDK and runtime identifier, hashes of every dedicated
`linux-x64` dependency lock file in the migrator project graph, GitHub build
run ID, and GitHub build provenance attestation. The package is self-contained;
loose scripts and local rebuilds are not release artifacts.

## Selected execution boundary

A GitHub-hosted Ubuntu larger runner connected to a dedicated subnet in the
existing development VNet is the selected execution boundary. Its runner group
must be restricted to this repository and exact migration workflow, maximum
concurrency one, and the protected `database-development` Environment.

The workflow must:

- exchange GitHub OIDC through the exact organization-bound FIC and use
  `AzureCliCredential` only in explicit hosted-runner mode;
- use the Azure SQL private endpoint/private DNS path;
- retrieve and verify the exact attested package bound to protected main;
- use Microsoft Entra managed-identity authentication, never a SQL password;
- execute once with zero automatic retry and bounded structured evidence; and
- rely on GitHub's hosted-runner disposal rather than persistent customer VM
  cleanup.

The intended existing UAMI is
`id-adventures-suite-migrate-job-dev` (object ID
`ffc9a4bd-67c4-44af-82dc-b7f663f8bea5`, client ID
`d0da8236-91dc-4454-8a3d-19d08a406e5d`). These identifiers are audit metadata,
not credentials; live identity and SQL-contained-user readback must still fail
closed before any later execution.

The custom GitHub App/JIT broker and self-hosted VM path is superseded, dormant,
and must not be deployed. No persistent compute, ACR, container-image publication, public SQL access,
temporary firewall opening, runner provisioning, Environment configuration,
SQL permission change, or migration execution is authorized by this ADR.
The workflow remains inert until separate approval creates the exact runner
group and label, delegated subnet, and GitHub network settings.
Its proof-only operation validates OIDC, identity, package, attestation, private
DNS, and TCP 1433 without issuing a SQL command.

VNet attachment does not attach a managed identity. Hosted execution uses only
`AzureCliCredential`; `ManagedIdentityCredential` remains only for genuine
Azure-hosted compute. Neither mode falls back. SQL token requests use only
`https://database.windows.net/.default`.

The dedicated NSG denies all inbound traffic, permits Azure DNS on TCP/UDP 53,
the fixed private SQL endpoint `10.40.1.4/32` on TCP 1433, and outbound HTTPS
on TCP 443, then denies other outbound traffic. The HTTPS rule is necessarily
address-broad: NSGs do not filter FQDNs. GitHub now recommends DNS/domain-based
egress control because its legacy static IP template is being retired; Azure
Firewall, NAT Gateway, DNS filtering, or another paid egress service remains a
separate option requiring approval.

The checksum-bound Azure network definition is
`infrastructure/github-hosted-private-migration-network/main.bicep`. It binds
the existing `vnet-adventures-suite-dev`, dedicated subnet
`snet-github-private-sql-migration` (`10.40.3.0/27`), dedicated NSG, immutable
organization business ID `316268438`, and
`GitHub.Network/networkSettings@2024-04-02` named
`private-sql-migration-vnet`. Its `tags.GitHubId` output is the only reviewed
network-configuration identifier for the later GitHub configuration boundary.

The separate read-only-first administrator operation is defined in
`docs/architecture/private-sql-administrator-operation.md`. It reuses the
reviewed ephemeral network, compute, and deletion design with a dedicated
administrator UAMI that is immutable-ID-bound and explicitly different from
the migration UAMI. Its workflow remains inert before Azure login. Baseline
readback, administrator authority establishment, bootstrap, migration, and
cleanup remain non-combinable approvals.
