# AdventuresSuite Database Migration Job Architecture

**Status:** Proposed PR #18 implementation; not provisioned

Azure Container Apps Jobs are the permanent migration execution boundary.
Database migration never runs in the web application, Companion API, App
Service startup, a developer workstation, or an interactive VM session.

## Execution model

`job-adventures-suite-migrate-dev` is manual-only, has no ingress, uses one
replica, parallelism one, retry limit zero, and a 900-second timeout. Each start
uses a unique operation ID and an image reference containing an immutable ACR
digest. A protected `database-development` GitHub Environment gates every Azure
mutation or Job start. Automatic workflows may build and validate but cannot
start migrations.

Provisioning is two-phase. The foundation creates networking, logging, ACR,
identities, OIDC federated credentials, minimum roles, and the Container Apps
environment. Only after the full-SHA image is pushed and its manifest digest is
resolved from ACR may the second template deploy the digest-bound dormant Job.
This removes the registry/image bootstrap cycle.

The container entrypoint allowlists four modes:

- `--verify-execution-channel`: obtains an ARM token through the explicitly
  selected migration identity, validates tenant/object/client/audience, and
  emits a checksummed completion envelope with `sqlAccessAttempted=false`;
- `--capture-migration-state`: read-only journal/catalog/permission/count and
  fingerprint evidence;
- `--run-reviewed-operation`: requires exact 0006, holds the zero-wait SQL
  application lock across pre-state, DbUp, and post-state, and applies 0007 then
  0008 with one transaction per script; and
- `--verify-migration-state`: read-only verification of exact 0008 and the
  reviewed pre-operation application fingerprint.

`Complete` is the only successful migration result. `Migration0007Committed`,
`NoScriptCommitted`, and `Unexpected` return nonzero, retain evidence, prohibit
automatic retry, and require a new review.

## Identity boundaries

The Job explicitly selects a dedicated user-assigned migration Managed Identity
for Azure SQL tokens. A separate user-assigned pull identity has only `AcrPull`
on the migration registry. The existing migration App Service system-assigned
identity cannot be transferred and is not reused.

Before SQL access, the process validates workforce tenant, token audience,
object ID, client ID, SQL FQDN, database, connection authentication mode, and
the expected contained SQL principal. Tokens are never logged or persisted.
Creating the identity and its contained SQL user is a later, separately approved
bootstrap operation.

The persistent Job definition omits operation ID and artifact checksum. The
starter injects both as start-time container overrides, after rejecting another
active execution, so stale operation values cannot survive in the template.

| Identity | Minimum scope | Required actions | Explicit exclusions |
| --- | --- | --- | --- |
| GitHub image publisher | Migration ACR repository | ACR push/read metadata through OIDC | Job start/update, SQL, role assignment |
| GitHub Job configurator | Development migration resource group | Validate/read/write the reviewed deployment; read/update dormant Job; read and attach existing migration/pull identities | Job start/delete, SQL, ACR push, identity creation or role assignment |
| GitHub Job starter/reader | Migration Job | Start one execution; read exact execution and logs | Job definition mutation, ACR push, SQL |
| Migration user-assigned identity | `AdventuresSuiteDevelopment` contained principal | ARM token identity proof; reviewed migration DDL/journal access only | Azure control-plane role, ACR push, runtime DML, `db_owner` |
| Registry pull identity | Migration ACR | Built-in `AcrPull` only | Push/delete, Job control, SQL |

The foundation defines separate GitHub publisher, configurator, and
starter/reader identities, their environment-scoped OIDC credentials, and the
minimum built-in/custom role assignments. GitHub identities cannot assign
roles; deploying those definitions remains a separately approved
infrastructure-administrator action.

## Network and supply chain

The proposed `10.40.3.0/27` delegated subnet is inside recorded VNet
`10.40.0.0/16` and does not overlap recorded `10.40.0.0/26` or `10.40.1.0/27`.
Live prefixes must be revalidated before deployment. SQL resolves through the
existing private DNS zone and private endpoint. The Job has no ingress or
inbound private endpoint.

Development uses ACR Basic with anonymous pull and admin credentials disabled.
Images use a full-SHA tag for publication. The publisher compares the push
manifest digest with the registry-authoritative ACR manifest digest, and only
that exact digest flows into Job configuration and execution. GitHub uses OIDC.
Validation builds the pinned,
multi-stage, non-root image; inspects labels/user/runtime contents; scans the
image; generates an SBOM; and tests the SQL-free mode.

## Evidence contract

Every finite execution emits one bounded JSON completion envelope containing
operation ID, UTC timing, release SHA, image digest, artifact/catalog identity,
safe identity/target metadata when SQL is used, pre/post journal classification,
verification booleans, fingerprint comparison, classification, process exit
code, and final-state status. Missing terminal status, missing or malformed
envelope, checksum mismatch, unbounded output, or ambiguous execution ID fails
closed. SQL text, connection strings, tokens, environment dumps, exception
details, private operational values, and application data are prohibited.

## Bridge retirement

The stopped migration App Service and temporary VM are superseded but unchanged.
They may be removed only after separately approved Container Apps Job
provisioning, a successful SQL-free execution-channel proof, and one successful
reviewed migration with retained evidence.
