# Ephemeral Runner Registration Broker

**Status:** inert repository design; no broker or registration authority exists

The broker is the sole future boundary permitted to translate one independently approved operation into one repository-scoped GitHub just-in-time runner configuration. It does not provision compute, access SQL, retrieve migration packages, or grant Azure authority. It complements, but never weakens, the VM and independent-cleanup design in `ephemeral-private-migration-runner.md` and the administrator boundary in `private-sql-administrator-operation.md`.

## Identity and authority

A dedicated GitHub App is installed on selected repository `adventures-suite/adventures-studio` (`1317655952`). It has repository Administration write because GitHub requires that permission for repository JIT generation/list/delete; it has no organization permission, user permission, OAuth flow, or webhook. The broker alone reads one immutable App-key version and holds short-lived App JWT/installation tokens in memory. Migration and SQL-bootstrap UAMIs, humans, groups, runners, and workflows never receive GitHub administration credentials.

Arm/cleanup callers use exactly bound GitHub OIDC assertions. Redeem callers use exact Entra workload assertions for a separately approved runner actor. Issuer, audience, repository and owner IDs, workflow ref/SHA, main ref, event, Environment, actor policy, operation, purpose, runner labels, and deadline are validated ordinally. Ambiguous group membership is prohibited.

## Operation and threats

The only state path is `Approved -> Issuing -> Issued -> Cleaning -> Closed`. Any failure is terminal. Atomic compare-and-exchange makes redemption one-use. A lost or ambiguous JIT response changes the operation to Failed; it is never regenerated. Caller-selected repositories, names, groups, labels, work directories, refs, or purposes are rejected.

The GitHub adapter contains only JIT generation, repository-runner listing, and one exact runner deletion. Redirects and non-GitHub origins fail. The JIT configuration is returned once to the reviewed VM over TLS and may exist only in memory or restrictive transient storage until runner startup. It is excluded from logs, application state, workflow data, custom data, artifacts, and evidence.

Cleanup is independent of VM/runner health. It closes redemption, matches the exact derived runner identity, issues at most one delete, and requires zero-residue readback. Multiple matches are ambiguous and cause no deletion. Automatic GitHub expiry is defense in depth, not proof.

## Hosting, cost, and key rotation

The future host is a dedicated Azure Functions Flex Consumption app in `westus2`, scaled to zero, with a system identity, private operation table, and private Key Vault access. HTTPS ingress accepts no anonymous operation; exact token validation remains application policy. No SQL, VM, network, or role-management permission belongs to the broker.

Expected steady-state cost is near zero compute while idle plus minimal execution, storage transaction/capacity, Key Vault operation, logging, and outbound API charges. Before provisioning, an Azure price estimate must bind the selected runtime, memory, invocation assumptions, storage, Key Vault tier, logging retention, network path, and monthly budget; this repository does not encode an unaudited currency estimate.

Rotation creates a new immutable App-key version, deploys the exact approved version ID after validation, proves the old version unused, then separately revokes it. No unversioned secret reference or automatic fallthrough is allowed.

## Evidence and recovery

Evidence follows `evidence.schema.json`; additions fail schema validation. It excludes secrets, tokens, raw claims/API responses, arbitrary labels, private content, package/SQL material, and environment dumps. Cancellation, timeout, response loss, runner loss, ambiguous matches, partial cleanup, and residue are explicit failures. There is no automatic retry or destructive rollback. Recovery is a new reviewed operation after independent cleanup and residue proof, never a state rewind.

## Remaining live boundaries

Repository implementation review/merge, GitHub App creation/installation, Azure broker provisioning, exact permissions/key placement, workload registrations, one operation arm, VM provisioning, JIT redemption, runner use, and independent registration cleanup each require separate approval.
