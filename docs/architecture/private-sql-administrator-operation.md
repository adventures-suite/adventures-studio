# Private Azure SQL Administrator Operation

**Status:** Hosted-runner implementation; no live identity or SQL authority

This boundary provides a read-only-first implementation for inspecting the
development Azure SQL bootstrap state from the reviewed GitHub-hosted VNet
runner. Repository presence does not create an identity, grant authority,
connect to SQL, or run the bootstrap.

## Administrator actor

The selected actor is one dedicated user-assigned managed identity named
`id-adventures-suite-sql-bootstrap-dev`. Every future approval binds its exact
Azure resource ID, tenant ID, client ID, principal/object ID, and immutable
organization-bound GitHub FIC. It is never the migration UAMI, an application
UAMI, a human credential, or a member of the existing administrator group.

The protected `database-development` workflow runs only on runner group
`private-sql-migration-vnet` with label `adventures-suite-private-sql`. GitHub
OIDC populates the Azure CLI session for the exact UAMI. The executable uses
only `AzureCliCredential` and requests
`https://database.windows.net/.default`; the resulting token remains in process
memory. Passwords, client secrets, certificates, interactive login, token
files, command-line tokens, durable caches, and token evidence are prohibited.

The administrator is established and later removed through separate Owner
boundaries. SQL user creation uses the reviewed `WITH SID ..., TYPE = E` form
bound to the migration application's client ID. It performs no Microsoft Graph
lookup and requires no Directory Readers authority.

## Reused private runner boundary

The administrator operation reuses the proven GitHub-hosted larger runner,
private DNS, `10.40.1.4:1433` route, restricted runner group and workflow, and
the attested migration package. It provisions no VM, NIC, disk, public IP,
broker, or registration. Every operation is capped at 30 minutes with zero
automatic retry.

## Modes and sequence

`baseline`, `bootstrap`, `cleanup`, and `denial-proof` are separately
dispatchable modes. Baseline requires an empty operation-approval digest.
Every other mode requires its own checksum-bound approval record. No mode
automatically invokes another.

A future authorized sequence is:

1. match repository/organization IDs, current protected-main SHA, workflow
   checksum, operation ID, package run/artifact IDs and checksums, every
   immutable identity/resource ID, SQL server, database, and private endpoint;
2. separately create the dedicated UAMI/FIC and establish its temporary SQL
   administrator authority;
3. execute baseline only and validate its bounded evidence schema;
4. review baseline evidence and approve bootstrap separately;
5. create only the schemas, roles, journal, migration contained user, and exact
   temporary migration catalog;
6. after migration, revoke that catalog and drop only the contained migration
   user while retaining schemas, roles, and journal; and
7. remove administrator authority and use a fresh OIDC/Azure CLI token to prove
   SQL authorization is denied.

Readback never falls through to bootstrap. Failure, timeout, cancellation,
runner loss, ambiguous output, schema mismatch, protected-main advance, or
partial cleanup ends the operation without retry.

## Baseline allowlist and evidence

The query reads only catalog metadata needed to classify the DbUp journal and
its ordered script names; `planning`, `auth`, and `audit` schemas and owners;
the four runtime roles, owners, and memberships; the dedicated administrator
and migration contained users; direct database/schema/object/role permissions;
and counts of required objects by approved schema and type. It never selects
application rows.

Evidence follows `infrastructure/private-sql-admin-operation/evidence.schema.json`.
It is size-bounded, rejects additional fields, hashes immutable Azure resource
IDs and database SIDs, and allows only documented names, counts, permissions,
outcomes, and zero-residue fields. Tokens, connection strings, package URLs,
raw claims, environment dumps, arbitrary SQL output, application data, private
content, and unapproved identifiers are prohibited.

## Separate future approvals

Dedicated UAMI/FIC creation, temporary administrator establishment, baseline,
bootstrap, migration, SQL cleanup, administrator restoration, and denial proof
remain independently reviewed live boundaries.
