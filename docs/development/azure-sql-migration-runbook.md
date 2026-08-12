# Azure SQL Bootstrap and Migration Runbook

**Status:** Design-only; execution remains blocked

AdventuresSuite uses the non-container `AdventuresSuite.DatabaseMigrator` DbUp
executable for ordered, forward-only private Azure SQL migrations. The
authoritative execution decision is
`docs/architecture/private-sql-migration-execution.md`.

## Invariants

- Azure SQL public access stays disabled; no temporary firewall rule is used.
- Workload authentication uses Microsoft Entra managed identity, never SQL
  passwords, access keys, or client secrets.
- The web and API never execute migrations or receive migration DDL authority.
- DbUp scripts, `dbo.AdventuresSuiteSchemaVersions`, the application lock,
  transaction-per-script behavior, state classification, fingerprints, and
  verification remain authoritative.
- A failed or ambiguous run is not retried automatically. Preserve evidence,
  classify the journal, and obtain a new repair-forward approval.
- Already-applied immutable migrations are not destructively rolled back and
  the journal is never edited to manufacture success.

## Release package gate

The `Validate SQL Migrations` protected-main run produces the only release
package. Retained evidence must contain and match:

1. full protected-main source SHA;
2. self-contained package SHA-256;
3. ordered embedded migration-catalog SHA-256;
4. exact .NET SDK/toolchain and `linux-x64` runtime identifier;
5. SHA-256 for every dedicated `packages.linux-x64.lock.json` in the migrator
   project graph;
6. GitHub build run ID; and
7. GitHub artifact provenance attestation.

The package must contain the evidence-capable migrator and
`run-reviewed-migration-operation.sh`. Reject mutable references, loose scripts,
unattested artifacts, lock drift, a source-SHA mismatch, or an unexpected
catalog.

## Future one-job procedure

Organization-bound GitHub federation is proven through the manual-only
`Prove Organization Federation` workflow before any personal-owner federated
credential is removed. The proof is independently Environment-gated, validates
the immutable organization/repository subject plus exact Azure workload
identity, and performs only account/token introspection. It never connects to
SQL, deploys resources, changes RBAC, executes migrations, or rebuilds the
migration package. Raw GitHub and Azure tokens, headers, request URLs, and
environment dumps are never retained. The `database-development` Environment
is protected-branch-only, requires reviewer `ssimonton007` (user ID
`55812276`), disables administrator bypass, contains only the four reviewed
non-secret identity/subscription variables, and contains zero secrets.

The next repository increment may define—but must not silently provision—a
one-job ephemeral GitHub self-hosted Azure VM in the existing VNet. A separate
Azure approval will be required for runner creation and independent cleanup; a
separate SQL approval will be required for the migration identity's exact
database permissions. Before implementation, review how the VM receives a
short-lived one-job runner registration, downloads and verifies the attested
artifact, resolves the private SQL endpoint, authenticates as the exact UAMI,
and is deleted after every outcome.

The proposed VM uses existing UAMI `id-adventures-suite-migrate-job-dev`
(object ID `ffc9a4bd-67c4-44af-82dc-b7f663f8bea5`, client ID
`d0da8236-91dc-4454-8a3d-19d08a406e5d`). Repository text never substitutes for
fresh Azure and database identity readback.

An approved run will capture pre-state, acquire the zero-wait application lock,
execute the exact operation once, capture post-state, classify `Complete`,
`Migration0007Committed`, `NoScriptCommitted`, or `Unexpected`, and retain
bounded logs. Independent VM cleanup is mandatory even if GitHub loses the
runner. None of those operations is implemented or authorized here.
