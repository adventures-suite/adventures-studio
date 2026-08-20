# Azure SQL Bootstrap and Migration Runbook

**Status:** Hosted VNet runner selected; execution remains blocked

The custom GitHub App/JIT broker and self-hosted VM path is superseded and must
remain undeployed. The selected boundary is a GitHub-hosted Linux larger runner
connected through GitHub Azure VNet private networking.

AdventuresSuite uses the non-container `AdventuresSuite.DatabaseMigrator` DbUp
executable for ordered, forward-only private Azure SQL migrations. The
authoritative execution decision is
`docs/architecture/private-sql-migration-execution.md`.

## Invariants

- Azure SQL public access stays disabled; no temporary firewall rule is used.
- Hosted-runner authentication uses the exact organization-bound GitHub OIDC
  FIC and Azure CLI token cache. Genuine Azure-hosted execution may use an
  attached UAMI. Neither mode falls back, and no SQL password, key, or client
  secret is permitted. Both modes request
  `https://database.windows.net/.default`, while token validation requires the
  selected mode's exact emitted audience: `https://database.windows.net` for
  Azure CLI or `https://database.windows.net/` for managed identity.
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

The manual hosted-runner workflow is inert until separately approved GitHub and
Azure configuration supplies its exact runner group, label, delegated subnet,
and network settings. Environment approval occurs before
the job is sent to the runner. Proof-only validates federation, package,
attestation, private DNS, and TCP 1433 without SQL. Migration is a distinct
operation with no automatic retry.

The runner group is limited to this repository and workflow, with concurrency
one. Keep the $2 monthly Actions spending stop. Deploy the checksum-bound
`infrastructure/github-hosted-private-migration-network/main.bicep` only under
a separate exact-SHA approval. Its exact existing-VNet binding creates only the
dedicated `10.40.3.0/27` subnet, dedicated NSG, and
`GitHub.Network/networkSettings@2024-04-02` for organization business ID
`316268438`. Validate all six outputs, including the authoritative
`tags.GitHubId` network-configuration ID, before configuring GitHub.

The NSG denies inbound traffic; allows only the fixed SQL private endpoint on
TCP 1433 and Internet TCP 443; and denies other ordinary outbound traffic.
Azure's default platform DNS remains available despite an ordinary outbound
deny rule unless it is explicitly blocked with Azure's special platform tag;
the template contains no DNS allow or deny rule. The HTTPS destination is broad
because an NSG cannot filter FQDNs.
GitHub recommends separately managed DNS/domain controls and is retiring its
legacy static-IP template. NAT Gateway, Azure Firewall, DNS filtering, or any
other paid egress service is outside this decision and requires separate
approval.

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

The future Companion policy mutation role is a separate administrator-owned
prerequisite. Repository support for the fixed
`AdventuresSuiteCompanionPolicyRuntime` role creates only an absent, dbo-owned,
empty role and otherwise verifies exact zero authority. It never grants future
migration-0010 permissions. Repository review and merge do not execute the
operation; persistent-database execution requires separate approval and
readback before migration-0010 preparation.

The one-time development initial-owner operation uses the same private,
Environment-approved administrator boundary but is a separate DML decision.
Use `bootstrap-initial-owner` only after a fresh baseline proves the expected
complete schema and an operator has reviewed the exact protected-main SHA,
workflow and package checksums, fixed Creator `creator_tsa_01`, exact opaque
target `UserId`, support ID, correlation ID, and distinct operation-approval
digest. It refuses any pre-existing Creator membership state and atomically
creates only the fixed non-expiring Owner membership plus required audit
evidence. Follow it with mandatory administrator cleanup and a fresh-session
denial proof. Never substitute email, claims, a public SQL path, application
startup, or direct interactive SQL editing.

### Temporary development SQL administrator authority

The dedicated administrator UAMI is intentionally not a member of the normal
administrator group and has no standing SQL access. When a separately approved
administrator operation is required, use the Owner-assisted finite boundary in
`infrastructure/private-sql-admin-authority/operate.sh` from a clean checkout of
the exact current protected-main SHA and a human Azure Owner session.

1. Run `prepare-establish` and retain its bounded JSON and digest.
2. Review and separately approve that exact digest.
3. Run `establish`; stop unless exact UAMI readback succeeds.
4. Execute only the independently approved SQL operation.
5. Regardless of its outcome, run `prepare-restore` and separately approve its
   different digest.
6. Run `restore`; require exact normal-group and Azure AD-only readback.
7. Acquire a fresh GitHub OIDC/Azure CLI session and run the separately approved
   SQL `denial-proof` operation.

Do not retry a failed or ambiguous transition, add the UAMI to the group, open
public SQL, use a migration or application identity, or combine authority,
baseline, bootstrap, restoration, and denial proof into one approval.

The repository-only administrator path is documented at
`docs/architecture/private-sql-administrator-operation.md`. Its mandatory
first mode is a statically allowlisted metadata baseline using the dedicated
`id-adventures-suite-sql-bootstrap-dev` UAMI; it does not exist or have authority merely
because the design is present. The identity is never the migration UAMI and
never gains authority through an Entra group. The baseline recognizes only
absent, the exact canonical `At0006` state, the exact `At0009` prerequisite for
the bounded `0010`-through-`0013` operation, the exact `At0012` prerequisite for
the bounded `0013` operation, or complete through `0013`. `At0006` requires the
exact fully qualified DbUp journal
prefix and the reviewed schemas, runtime roles, permissions, and object counts.
The `At0009` prerequisite includes the exact dbo-owned, empty, authority-free
`AdventuresSuiteCompanionPolicyRuntime` role; ownership, membership, or direct
permission drift fails closed;
arbitrary partial states are rejected. The hosted-runner workflow binds exact
repository and organization IDs, protected SHA, workflow checksum, operation
ID, identity IDs, server, database, private endpoint, attested package, and
baseline SQL checksum. It uses GitHub OIDC with explicit `AzureCliCredential`.
Baseline, bootstrap, cleanup, and fresh-session denial proof require separate
approval packets. Bootstrap uses `CREATE USER ... WITH SID ..., TYPE = E`, so
no Directory Readers grant is introduced. Cleanup revokes the exact temporary
catalog and drops only the migration contained user; the schemas, four runtime
roles, and DbUp journal remain.

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

An approved run will capture the exact `At0009` pre-state, acquire the zero-wait
application lock, execute migrations `0010`, `0011`, `0012`, and `0013` once in order,
capture post-state, classify `Complete`, `Migration0012Committed`, `Migration0011Committed`,
`Migration0010Committed`, `NoScriptCommitted`, or `Unexpected`, and retain
bounded logs. Independent VM cleanup is mandatory even if GitHub loses the
runner. Repository implementation and review do not authorize live execution.

## Inert runner lifecycle definition

The repository-only design is documented in
`docs/architecture/ephemeral-private-migration-runner.md` and under
`infrastructure/private-migration-runner`. Its manual Environment-gated
workflow deliberately fails before login or provisioning until an OIDC
registration broker and exact temporary provisioning/cleanup assignments pass
separate reviews. This is not runner, SQL, or migration approval.
