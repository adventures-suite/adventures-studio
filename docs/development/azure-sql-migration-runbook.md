# Azure SQL Bootstrap and Migration Runbook

**Status:** Design-only; execution remains blocked

The future one-job registration prerequisite is specified in
`docs/architecture/ephemeral-runner-registration-broker.md`. Its dedicated
scale-to-zero foundation and fictional-key custody importer are inert
repository artifacts. They create no GitHub App, broker, runner, token,
assignment, or
registration; live operation and zero-residue readback require separate exact
approvals.

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

Federation proof distinguishes token exchange from subscription visibility.
The Web and Companion deployment identities must see exactly the configured
development subscription. The intentionally unassigned foundation, RBAC, and
database-migration identities authenticate at tenant scope with Azure CLI
`allow-no-subscriptions`, must see no Azure subscription, and emit
`subscriptionConfigured: true` with `subscriptionVisibility: none_expected`.
Any subscription visible to those zero-authority identities fails the proof.
This evidence proves authentication and expected absence of control-plane
authority; it does not claim that the configured subscription was visible or
authenticated.

The next repository increment may define—but must not silently provision—a
one-job ephemeral GitHub self-hosted Azure VM in the existing VNet. A separate
Azure approval will be required for runner creation and independent cleanup; a
separate SQL approval will be required for the migration identity's exact
database permissions. Before implementation, review how the VM receives a
short-lived one-job runner registration, downloads and verifies the attested
artifact, resolves the private SQL endpoint, authenticates as the exact UAMI,
and is deleted after every outcome.

The repository-defined SQL boundary separates three authorities. An Entra
administrator creates and owns the `planning`, `auth`, and `audit` schemas,
the four dbo-owned runtime roles, the exact DbUp journal table, and the
contained migration user. The temporary migration principal receives only
`CONNECT`, `CREATE TABLE`, `VIEW DEFINITION`, schema `CONTROL` on those three
schemas, and journal `SELECT`/`INSERT`. It receives no fixed-role membership,
schema ownership, role administration, schema creation, journal
`UPDATE`/`DELETE`, or unrelated `dbo` authority. Runtime principals retain
only their separately verified application DML grants and denials. Live
bootstrap remains a later, exact approval boundary.

The repository-only administrator path is documented at
`docs/architecture/private-sql-administrator-operation.md`. Its mandatory
first mode is a statically allowlisted metadata baseline using the dedicated
`id-adventures-suite-sql-bootstrap-dev` UAMI and direct contained principal
`AdventuresSuiteSqlBootstrapDev`; neither exists or has authority merely
because the design is present. The identity is never the migration UAMI and
never gains authority through an Entra group. The inert workflow binds exact
repository and organization IDs, protected SHA, workflow checksum, operation
ID, identity IDs, server, database, and private endpoint, then fails before
Azure login. A baseline dispatch and any later bootstrap dispatch require
separate approval packets and independent cleanup and residue proof.

### Broker foundation authority boundary

The broker foundation uses dedicated future provisioner, cleanup, and residue
reader identities documented in
`docs/architecture/ephemeral-runner-registration-broker.md`. Do not reuse any
migration, SQL-bootstrap, application, human, or group identity. The
provisioner receives only create/reconcile actions for reviewed foundation
types and receives no delete or authorization authority.

Independent cleanup receives its fixed cleanup role only at the exact
verified-present cleanup-parent resource IDs, never at resource-group or
subscription scope. After any complete or partial deployment outcome, the
residue reader must first classify all 23 checksum-bound resource IDs and
types. Unknown, additional, substituted, duplicated, wrong-type, failed, or
ambiguous evidence stops before assignment or deletion. The Owner then creates
only the validated subset of deterministic resource-scoped assignments;
resources proven absent receive no assignment. Cleanup is dependency ordered,
bounded-polling, zero-retry, and stops after the first failed, timed-out, or
ambiguous operation. It does not purge the protected Key Vault. Complete
23-resource residue evidence and a separate RBAC assignment inventory are
mandatory before evidence may report clean residue.

This is an Owner-assisted lifecycle, not automatic or unconditional cleanup:
inventory, exact-subset validation, resource-scoped assignment, cleanup and
polling, full-graph residue proof, removal of every temporary assignment, and
fresh-session denial proof are separately approved steps.

The repository templates and manual workflow are inert. They do not create an
identity, role, assignment, resource, FIC, credential, or runner. Each live
step requires its own exact-SHA/checksum approval and post-operation readback.

The proposed VM uses existing UAMI `id-adventures-suite-migrate-job-dev`
(object ID `ffc9a4bd-67c4-44af-82dc-b7f663f8bea5`, client ID
`d0da8236-91dc-4454-8a3d-19d08a406e5d`). Repository text never substitutes for
fresh Azure and database identity readback.

An approved run will capture pre-state, acquire the zero-wait application lock,
execute the exact operation once through migration 0009, capture post-state,
classify `Complete`, `Migration0008Committed`, `Migration0007Committed`,
`NoScriptCommitted`, or `Unexpected`, and retain
bounded logs. Independent VM cleanup is mandatory even if GitHub loses the
runner. None of those operations is implemented or authorized here.

## Inert runner lifecycle definition

The repository-only design is documented in
`docs/architecture/ephemeral-private-migration-runner.md` and under
`infrastructure/private-migration-runner`. Its manual Environment-gated
workflow deliberately fails before login or provisioning until an OIDC
registration broker and exact temporary provisioning/cleanup assignments pass
separate reviews. This is not runner, SQL, or migration approval.
