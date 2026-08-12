# Private Azure SQL Migration Execution

**Status:** Approved architecture; runner not implemented

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

## Future execution boundary

A later, separately reviewed increment may provision one ephemeral Azure VM in
the existing development VNet as a one-job GitHub self-hosted runner. It must:

- use the existing migration user-assigned managed identity and the Azure SQL
  private endpoint/private DNS path;
- receive one short-lived, one-job runner registration without a client secret;
- retrieve and verify the exact attested package bound to protected main;
- use Microsoft Entra managed-identity authentication, never a SQL password;
- execute once with zero automatic retry and bounded structured evidence; and
- be deleted after every success, failure, cancellation, timeout, runner loss,
  or inconclusive result through mandatory independent cleanup.

The intended existing UAMI is
`id-adventures-suite-migrate-job-dev` (object ID
`ffc9a4bd-67c4-44af-82dc-b7f663f8bea5`, client ID
`d0da8236-91dc-4454-8a3d-19d08a406e5d`). These identifiers are audit metadata,
not credentials; live identity and SQL-contained-user readback must still fail
closed before any later execution.

No persistent compute, ACR, container-image publication, public SQL access,
temporary firewall opening, runner provisioning, Environment configuration,
SQL permission change, or migration execution is authorized by this ADR.
Before the runner implementation begins, one design review must prove secure
one-job registration delivery, artifact retrieval/attestation verification,
private SQL reachability, and deletion after every outcome.
