# Private Azure SQL Administrator Operation

**Status:** Inert repository design; no live authority or execution

This boundary provides a read-only-first design for inspecting the development
Azure SQL bootstrap state from the reviewed ephemeral private runner. It does
not provision a runner or identity, grant authority, connect to SQL, or run the
bootstrap.

## Administrator actor

The selected actor is one dedicated user-assigned managed identity named
`id-adventures-suite-sql-bootstrap-dev`. Every future approval binds its exact
Azure resource ID, tenant ID, client ID, and principal/object ID. The database
principal alias is `AdventuresSuiteSqlBootstrapDev` and must map directly to
that object ID. It is never a group member, human identity, server login,
migration UAMI, application UAMI, or GitHub identity.

The identity is attached only to one operation-scoped VM. Guest code requests
one short-lived token from IMDS for `https://database.windows.net/` and passes
it in memory to the reviewed metadata reader. Passwords, client secrets,
certificates, interactive login, token files, command-line tokens, durable
caches, and environment evidence are prohibited. Creating this identity,
establishing its exact database authority, and assigning it to the VM are
separate future approvals.

A future reviewed metadata reader must use the managed-identity SDK with the
fixed Azure SQL audience and exact administrator client ID through an in-process
connection callback. Shell token extraction is prohibited: no token may cross
process arguments, environment variables, files, logs, or evidence. That
reader does not exist in this inert increment and requires a separate
repository approval before the workflow can progress beyond its guard.

## Reused private runner boundary

The administrator wrapper calls the reviewed runner Bicep module. It therefore
inherits the dedicated `10.40.3.0/27` operation subnet, private SQL
`10.40.1.4:1433` route, pinned Ubuntu image and VM size, no public IP, deny-all
inbound NSG, guest HTTPS allowlist, delete-with-VM NIC and OS disk, 45-minute
deadline, zero automatic retry, and independent cleanup/residue contract. The
wrapper requires the administrator and migration UAMI resource IDs to differ.
It never uses `snet-devtools` or the retired SQL administration VM.

## Modes and sequence

`baseline` and `bootstrap` are distinct operation modes. Baseline requires an
empty bootstrap-approval digest and contains only the statically allowlisted
metadata query. Bootstrap requires a separate 64-character approval-packet
digest, but no bootstrap invocation exists in this increment. The workflow
remains deliberately inert and fails before Azure login, provisioning, or SQL.

A future authorized sequence is:

1. match repository/organization IDs, current protected-main SHA, workflow
   checksum, operation ID, package run/artifact IDs and checksums, every
   immutable identity/resource ID, SQL server, database, and private endpoint;
2. separately establish runner registration, provisioning, cleanup, and SQL
   administrator authorities;
3. provision the reviewed one-job private runner;
4. execute baseline only and validate its bounded evidence schema;
5. clean up independently and prove zero residue;
6. review baseline evidence and prepare a new exact bootstrap packet; and
7. only under that later approval, execute a separate bootstrap operation.

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

The registration broker; runner provisioning role; independent cleanup role;
dedicated UAMI creation and VM attachment; direct contained-user creation and
administrator SQL permissions; private DNS/reachability proof; baseline
dispatch; bootstrap dispatch; migration authority; artifact transfer; DbUp
execution; and final cleanup each remain independently reviewed live
boundaries.
