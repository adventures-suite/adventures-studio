# Slice 5F Azure Development Environment

**Status:** Provisioned Foundation; Application Integration In Progress

**Last Updated:** August 8, 2026

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

| Workload | Observed principal/object ID | Boundary |
| --- | --- | --- |
| Application App Service | `43f88b68-e853-4ece-9379-bd2079af8ec0` | Runtime DML and approved Key Vault/Blob data operations |
| Migration App Service | `ce76a652-2741-4324-8a1c-18f25409dee0` | Migration DDL and migration-journal DML only |

These generated IDs are recorded for bootstrap verification and audit. IaC,
deployment, and bootstrap scripts resolve current identities from Azure resource
outputs rather than embedding principal IDs in application code.

The migration app:

- shares the existing B1 App Service plan;
- is stopped by default;
- is HTTPS-only;
- has public ingress disabled;
- uses the development VNet integration subnet; and
- has no application runtime assignment.

Starting, deploying, invoking, and stopping it are protected operational actions
with retained evidence.

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
- approved development `db_ddladmin` membership;
- read/write access to `dbo.AdventuresSuiteSchemaVersions` and migration-required
  data changes;
- no `db_owner`;
- no server-level database, login, or security administration; and
- no runtime application assignment.

Before production, review whether a custom migration role can replace the broad
fixed development `db_ddladmin` role.

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
   the migration contained user and its
   development migration grants.
2. The migration workload identity runs `--migrate` with
   `ADVENTURESSUITE_SQL_CONNECTION_STRING`.
3. The Entra database administrator runs `--bind-runtime` only after the
   migration has created the runtime database role.
4. The migration workload identity runs `--verify-permissions` to prove its
   connection, DDL role, migration journal, and authentication schema access.

`--bootstrap-key-vault` is a separate, explicit control-plane/data-plane
operation. It must not run implicitly with a database migration. It creates a
non-exportable signing certificate and the Data Protection wrapping key, then
prints public certificate material only for External ID registration.

## Known Private-Execution Gates

Public data-plane access is intentionally disabled. Therefore a normal
GitHub-hosted runner cannot be assumed to:

- connect to Azure SQL for contained-user bootstrap or migrations;
- reach Key Vault or Blob data-plane endpoints; or
- deploy through a migration app's publicly disabled SCM endpoint.

Before those operations, approve and prove a private execution path. Acceptable
directions include a tightly controlled ephemeral or self-hosted runner in the
VNet, or another reviewed Azure-native mechanism that preserves workload
identity, immutable artifacts, logs, time bounds, and cleanup. Do not temporarily
enable broad public networking merely to make a hosted workflow pass.

The chosen path, cost, operator identity, artifact flow, and cleanup evidence
must be documented before SQL bootstrap or the first migration.

## Reconciliation Gate

Before application configuration:

1. export/read the live resource inventory;
2. compare names, region, SKU, network, DNS, public-access, identity, and RBAC
   state with approved IaC;
3. record currently unresolved resource names and object URIs;
4. run DNS resolution from each VNet-integrated workload;
5. prove public data-plane access is denied;
6. confirm the migration app is stopped;
7. estimate actual monthly cost and configure budget visibility; and
8. retain sanitized evidence without keys, tokens, connection strings, or
   private customer data.

Any material drift blocks Slice 5F until reconciled or explicitly approved.

## Slice 5F Exit Evidence

- reviewed identity-contract extraction removes the Web/adapters cycle;
- IaC deployment/reconciliation passes;
- External ID registration and certificate readiness pass;
- SQL contained users and least-privilege grants are verified;
- exact migrator package runs once and the migration app returns to stopped;
- application reaches SQL, Key Vault, and Blob only over approved paths;
- shared Data Protection keys survive restart and multiple instances;
- first sign-in maps exact `iss`/`sub` and creates one atomic session;
- cookie, restart, circuit revalidation, revocation, and POST sign-out pass;
- public Creator and unknown hosts remain anonymous/denied as designed;
- exact SHA, health, Creator validation, and Resource validation pass; and
- rollback, rotation, incident, and teardown runbooks are approved.

Slice 6 remains blocked until this evidence is complete.
