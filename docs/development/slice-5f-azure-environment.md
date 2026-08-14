# Slice 5F Azure Development Environment

**Status:** Slice 5F Complete; External Provider Gate Passed

**Last Updated:** August 9, 2026

## Purpose

This document records the approved and observed non-production Azure foundation
for Phase 3 Slice 5F. Azure is the running environment; infrastructure as code
is the reproducible definition; runbooks explain cross-tenant and data-plane
operations. None replaces the others.

This inventory contains identifiers and resource names, not credentials. Never
add certificate private keys, tokens, connection passwords, storage keys, Data
Protection keys, or temporary bootstrap credentials.

## Approved Environment

| Property | Approved value |
| --- | --- |
| Environment | Development / non-production |
| Azure subscription | `5ace9cdd-06d1-47d9-8214-1e7c756d076a` |
| Resource group | `rg-adventures-suite-dev` |
| Azure region | West US 2 |
| Workspace origin | `https://adventures-suite-dev-e7a5g5dudneqgqcv.westus2-01.azurewebsites.net` |
| Estimated incremental cost | Approximately USD $29–$31/month under the approved estimate |

Cost is not a contractual quote. Record actual monthly cost, budgets, alerts,
and material variance after the first complete billing cycle.

## External ID Tenant

| Property | Observed value |
| --- | --- |
| Display name | `AdventuresSuite Development` |
| Initial domain | `adventuressuitedev.onmicrosoft.com` |
| Tenant ID | `4fc030b6-583b-4b7a-9718-c8c26019771e` |
| Data location | United States |
| Billing | MAU billing linked to the approved existing subscription |

The initial domain and data-location decision are durable environment facts.
Production requires a separate external tenant and a new explicit data-location
and billing approval.

Exact development callbacks are:

```text
https://adventures-suite-dev-e7a5g5dudneqgqcv.westus2-01.azurewebsites.net/signin-oidc
https://adventures-suite-dev-e7a5g5dudneqgqcv.westus2-01.azurewebsites.net/signout-callback-oidc
```

No wildcard, HTTP, alternate host, or inferred forwarded host is permitted.

## Azure SQL

| Property | Observed value |
| --- | --- |
| Logical server | `adventures-suite-dev-sql` |
| Database | `AdventuresSuiteDevelopment` |
| Service objective | Basic, 5 DTU |
| Maximum size | 2 GB |
| Authentication | Microsoft Entra only |
| Public network access | Disabled |
| Private endpoint IP | `10.40.1.4` for diagnostic inventory only |

Application configuration uses the logical server FQDN and database name. It
never hardcodes the private IP. Private DNS resolves the standard SQL hostname
to the private endpoint.

## Network

| Property | Approved value |
| --- | --- |
| Address space | `10.40.0.0/16` |
| App Service integration subnet | `10.40.0.0/26` |
| Private endpoint subnet | `10.40.1.0/27` |

Private DNS zones:

- `privatelink.database.windows.net`;
- `privatelink.vaultcore.azure.net`; and
- `privatelink.blob.core.windows.net`.

Each zone must be linked to the development VNet. App Service and the migration
app use VNet integration for outbound access. Integration and private-endpoint
subnets remain separate and retain the Azure-required delegation and network
policies.

Private IP addresses are diagnostic observations, not configuration contracts.
Infrastructure and tests resolve service hostnames through DNS.

## Key Vault and Data Protection

| Property | Observed value |
| --- | --- |
| Key Vault | `adventures-suite-dev-kv` |
| Authorization | Azure RBAC |
| Purge protection | Enabled |
| Public network access | Disabled |
| Private endpoint IP | `10.40.1.5` for diagnostic inventory only |
| Storage account | `advsuitedevdpkeys` |
| Shared-key access | Disabled |
| Public network access | Disabled |
| Blob private endpoint IP | `10.40.1.6` for diagnostic inventory only |

The exact Data Protection container, Key Vault certificate name, Data
Protection wrapping-key name, and version-independent object URIs are deployment
outputs and environment configuration. Record them during the reconciliation
gate without storing key material.

The application Managed Identity is scoped only to its Data Protection
container, public certificate metadata reads, assertion signing with the
certificate's Key Vault key, and wrapping-key operations. The External ID
private key is non-exportable and never downloaded by the application. The
application signs client assertions through Key Vault cryptographic operations.
Secret reads, certificate export, storage-account-wide data access, and broad
vault administration are prohibited.

At minimum, the runtime assignment must permit certificate metadata `get`, key
`sign`, and the Data Protection key's `wrapKey`/`unwrapKey` operations. It must
not permit secret `get`, certificate import/create/delete, key export, or vault
administration. The separately approved bootstrap identity may receive
time-bounded certificate/key creation authority for `--bootstrap-key-vault`;
remove that elevation after creation and verification.

## Workload Identities

### Proposed one-job migration runner (not provisioned)

The approved architecture is one ephemeral GitHub self-hosted Azure VM in the
existing VNet, using the existing migration UAMI and Azure SQL private endpoint.
It has no ACR, persistent compute, automatic retry, public SQL, or temporary
firewall rule. Before implementation, a separate design review must prove
short-lived one-job registration delivery, attested package retrieval, private
DNS/SQL reachability, and independent deletion after every outcome.

| Workload | Observed principal/object ID | Boundary |
| --- | --- | --- |
| Application App Service | `43f88b68-e853-4ece-9379-bd2079af8ec0` | Runtime DML and approved Key Vault/Blob data operations |
| Migration App Service | `ce76a652-2741-4324-8a1c-18f25409dee0` | Migration DDL and migration-journal DML only |

These generated IDs are recorded for bootstrap verification and audit. IaC,
deployment, and bootstrap scripts resolve current identities from Azure resource
outputs rather than embedding principal IDs in application code.

Existing identities are not deleted by this architecture change and remain
unauthorized unless separately assigned. The former migration App Service path
must not receive a new execution approval.

## SQL Permission Boundary

Application identity:

- `CONNECT` to the one application database;
- schema-scoped `SELECT`, `INSERT`, `UPDATE`, and approved `DELETE` required by
  current repositories;
- approved stored-procedure `EXECUTE` only when introduced;
- no schema, table, index, user, role, DbUp-journal, or other DDL authority; and
- no ability to run migrations or elevate itself.

Migration identity:

- `CONNECT` to the one application database;
- database `CREATE TABLE` and `VIEW DEFINITION`;
- schema-scoped `CONTROL` only on administrator-owned `planning`, `auth`, and
  `audit` schemas;
- `SELECT` and `INSERT` only on the administrator-created
  `dbo.AdventuresSuiteSchemaVersions` journal;
- no journal `UPDATE` or `DELETE`, fixed database-role membership, database
  `ALTER ANY ROLE` or `CREATE SCHEMA`, or authority over unrelated `dbo`
  objects;
- no `db_owner`;
- no server-level database, login, or security administration; and
- no runtime application assignment.

The administrator, not the migration identity, owns the three schemas, creates
the exact DbUp journal shape and four dbo-owned runtime roles, and creates the
contained migration user. The bootstrap and verifier fail closed on missing,
additional, inherited, fixed-role, or incorrectly scoped authority.

## Infrastructure-as-Code Boundary

The approved IaC definition must own or reconcile:

- resource group references, region, naming, and required tags;
- VNet, subnets, delegations, and network policies;
- private endpoints, private DNS zones, links, and records;
- SQL server, database, Entra-only authentication, SKU, size, and public-access
  denial;
- Key Vault RBAC mode, purge protection, public-access denial, and private
  endpoint;
- Storage account security, Blob container, public/shared-key denial, and
  private endpoint;
- application and migration Managed Identities;
- App Service VNet integration and migration-app security state;
- least-privilege Azure RBAC assignments;
- non-secret App Service settings and GitHub Environment variables;
- diagnostic settings, budgets, alerts, and deployment outputs; and
- drift-detection assertions for security-critical properties.

IaC must not contain External ID users, private certificate material, Data
Protection keys, SQL bootstrap credentials, or generated runtime tokens.

## Manual and Cross-Tenant Boundaries

Versioned runbooks govern operations that are manual or cross a control/data
plane:

- External ID tenant billing, application registration, user flow, callback,
  and public-certificate registration;
- Key Vault certificate creation, retrieval authorization, and rotation;
- one-time SQL Entra administrator and contained-user bootstrap;
- migration artifact deployment and execution through private networking;
- first real browser sign-in and revocation smoke tests; and
- emergency disablement, rollback, evidence collection, and teardown.

The private SQL execution path must run the database steps in this exact order:

1. An Entra database administrator runs `--bootstrap-sql` once with
   `ADVENTURESSUITE_ADMIN_SQL_CONNECTION_STRING` and the approved migration
   principal object ID, client ID, and exact display name. This creates only
   the migration contained user, administrator-owned schemas, exact DbUp
   journal, empty source-controlled runtime roles, and the explicit temporary
   migration permission catalog. Runtime roles are pre-created under
   administrator authority so the migration identity never receives role
   administration or schema ownership.
2. The migration workload identity runs `--migrate` with
   `ADVENTURESSUITE_SQL_CONNECTION_STRING`.
3. The Entra database administrator runs `--bind-runtime` only after the
   migration has created the runtime database role.
4. The migration workload identity runs `--verify-permissions` to prove its
   exact database, schema, and journal catalog and the absence of broader or
   inherited authority.

`--bootstrap-key-vault` is a separate, explicit control-plane/data-plane
operation. It must not run implicitly with a database migration. It creates a
non-exportable signing certificate and the Data Protection wrapping key, then
prints public certificate material only for External ID registration.

## Known Private-Execution Gates

Public data-plane access is intentionally disabled. Therefore a normal
GitHub-hosted runner cannot be assumed to:

- connect to Azure SQL for contained-user bootstrap or migrations;
- reach Key Vault or Blob data-plane endpoints; or
- rely on a public deployment endpoint for migration execution.

Before those operations, approve and prove a private execution path. Acceptable
directions include a tightly controlled ephemeral or self-hosted runner in the
VNet, or another reviewed Azure-native mechanism that preserves workload
identity, immutable artifacts, logs, time bounds, and cleanup. Do not temporarily
enable broad public networking merely to make a hosted workflow pass.

The chosen path, cost, operator identity, artifact flow, and cleanup evidence
must be documented before SQL bootstrap or the first migration.

The selected future administrator actor is a dedicated database-scoped UAMI,
`id-adventures-suite-sql-bootstrap-dev`, represented by direct contained
principal `AdventuresSuiteSqlBootstrapDev`. Exact Azure resource, tenant,
client, and principal IDs must be resolved and approved before use. It is not
the migration UAMI, a human, or a group member. The read-only-first operation
in `docs/architecture/private-sql-administrator-operation.md` reuses the
reviewed ephemeral runner boundary but remains inert until runner registration,
provisioning, cleanup, SQL authority, and baseline execution are separately
approved. Bootstrap is a different later mode and cannot follow baseline
implicitly.

## Reconciliation Gate

Before application configuration:

1. export/read the live resource inventory;
2. compare names, region, SKU, network, DNS, public-access, identity, and RBAC
   state with approved IaC;
3. record currently unresolved resource names and object URIs;
4. run DNS resolution from each VNet-integrated workload;
5. prove public data-plane access is denied;
6. confirm no persistent migration runner is active;
7. estimate actual monthly cost and configure budget visibility; and
8. retain sanitized evidence without keys, tokens, connection strings, or
   private customer data.

Any material drift blocks Slice 5F until reconciled or explicitly approved.

## Slice 5F Exit Evidence

- reviewed identity-contract extraction removes the Web/adapters cycle;
- IaC deployment/reconciliation passes;
- External ID registration and certificate readiness pass;
- SQL contained users and least-privilege grants are verified;
- exact attested migrator package runs once and the ephemeral runner is deleted;
- application reaches SQL, Key Vault, and Blob only over approved paths;
- shared Data Protection keys survive restart and multiple instances;
- first sign-in maps exact `iss`/`sub` and creates one atomic session;
- cookie, restart, circuit revalidation, revocation, and POST sign-out pass;
- public Creator and unknown hosts remain anonymous/denied as designed;
- exact SHA, health, Creator validation, and Resource validation pass; and
- rollback, rotation, incident, and teardown runbooks are approved.

Slice 6 remains blocked until this evidence is complete.

### Development Gate Result

The development External Provider gate passed on August 9, 2026. Evidence is
sanitized: it records resource names, aggregate results, timestamps, commit
identifiers, and workflow support identifiers without tokens, cookies,
connection strings, private keys, certificate material, or raw identity
claims.

| Gate | Result |
| --- | --- |
| Deployed application | Commit `9df3544137c899b85478ce7627fbefb28b1cba8e` active and healthy |
| Deployment validation | GitHub Actions run `31345223719` passed health, Creator, Resource, and package checks |
| SQL migration validation | GitHub Actions run `31346969130` passed for the exact final commit |
| Browser sign-in | Exact workspace sign-in, External ID callback, authoritative cookie session, and authenticated workspace response passed |
| Durable session | The authenticated session survived deployment to a new App Service instance, proving shared Data Protection continuity |
| Identity persistence | One platform user and one active external-identity mapping were present after first sign-in; evidence did not disclose issuer or subject values |
| Creator boundary | Authentication did not grant Creator access; the workspace remained unavailable pending Slice 6 membership persistence |
| Sign-out | Antiforgery-protected `POST /authentication/sign-out` passed; `GET` remained rejected with `405 Method Not Allowed` |
| Administrative revocation | Four outstanding sessions for the sole development user were atomically revoked with zero active sessions remaining; the open browser returned to anonymous state on reload |
| Public-host isolation | Development App Service did not bind the public Creator hostname; public-host and unknown-host behavior remains covered by the automated host-isolation suite |
| Circuit security | Exact-origin SignalR transports and fail-closed circuit revalidation passed automated coverage; no live circuit mutation existed in the current server-rendered workspace shell to exercise independently |
| Information leakage | Application and authentication telemetry retained PII logging disabled and contained no raw tokens, cookies, subjects, secrets, or certificate material |
| Privileged cleanup | Temporary SQL administrator-group membership was removed, its CLI session was cleared, and the SQL administration VM was deallocated |

The SQL preflight authenticated through the configured workforce-tenant SQL
administrator group and targeted only `AdventuresSuiteDevelopment`. It observed
one user and one active external identity before the guarded revocation. The
revocation transaction required that inventory to match, changed only session
revocation fields, and left no active session. The temporary member account had
no Azure subscription access and its group membership was removed immediately
after the gate.

The final commit changes only workspace navigation behavior relative to the
previous authoritative migration run. The exact-commit SQL workflow nevertheless
rebuilt the solution, ran the SQL Server migration integration gate, published
the immutable migrator artifact, and retained diagnostics and migration
evidence.

Slice 5F is therefore complete for the Azure development environment. Slice 6
may begin, while production identity activation remains subject to its own
environment approval and gates.
