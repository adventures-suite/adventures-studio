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
memory. The live Azure CLI token's audience is required to equal exactly
`https://database.windows.net`; it is not normalized or treated as equivalent
to the managed-identity audience ending in `/`. Passwords, client secrets,
certificates, interactive login, token files, command-line tokens, durable
caches, and token evidence are prohibited.

The administrator is established and later removed through separate Owner
boundaries. SQL user creation uses the reviewed `WITH SID ..., TYPE = E` form
bound to the migration application's client ID. It performs no Microsoft Graph
lookup and requires no Directory Readers authority.

The Owner-assisted authority boundary is implemented by
`infrastructure/private-sql-admin-authority/operate.sh`. It never adds the
dedicated identity to the normal administrator group and never changes public
networking. `prepare-establish` first proves the exact normal group remains the
Azure SQL administrator and emits a checksum-bound packet. Only `establish`
with that separately approved digest may replace the server administrator with
the exact dedicated UAMI. The operation then requires exact live readback and
preserves Azure AD-only authentication.

Restoration is an independent approval. `prepare-restore` accepts only the
exact temporary UAMI pre-state and emits a different packet; `restore` requires
its digest, reinstates the exact normal administrator group and verifies live
readback. Neither operation runs SQL, invokes baseline or bootstrap, changes
group membership, retries automatically, or treats an unexpected live state as
repairable. Baseline failure, bootstrap failure, timeout, cancellation, runner
loss, or ambiguity still requires restoration followed by a fresh-session
`denial-proof` operation.

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

### Companion policy runtime-role prerequisite

The finite administrator boundary also supports the separately approved
`bootstrap-policy-role` operation. Its implementation owns one compile-time
constant only: `AdventuresSuiteCompanionPolicyRuntime`. It creates that role
only when absent with `AUTHORIZATION dbo` and grants no permission or
membership. A conforming pre-existing role is idempotently accepted.

Before commit, the operation requires the exact principal to be a non-fixed
database role owned by `dbo`, with zero members, zero parent-role memberships,
zero explicit permissions, and zero owned schemas or objects. A case-altered or
same-name non-role principal, different owner, direct or inherited authority,
or ambiguous metadata fails closed for human remediation. The operation never
drops, renames, re-owns, revokes, grants, repairs, or adds membership. It also
captures the existing `AdventuresSuiteCompanionReadRuntime` state before and
after and requires it to remain equivalent.

The role operation runs only through this administrator workflow after its own
exact approval. It is not reachable from application or API startup, ordinary
deployment, or the migration principal. Its bounded v1 evidence contains only
the fixed operation and role names, created/pre-existing outcome, `dbo`
ownership, zero authority/membership counts, unchanged-read-role result,
bounded support/correlation/operation identifiers, and UTC occurrence time.
Tokens, connections, SQL parameters, and private content are prohibited.

Creating the empty role does not authorize migration `0010`; a later migration
preflight must independently re-read the role and every documented
prerequisite.

### Development initial Creator Owner

The finite administrator boundary also supports one separately approved
`bootstrap-initial-owner` operation for the fixed development Creator
`creator_tsa_01`. This is an initial-ownership bootstrap, not general membership
administration and not a Planner, web-startup, or authentication behavior.

The operation accepts one exact opaque platform `UserId` using the canonical
authorization-identity format. It requires that user to be active and retain at
least one active External ID mapping. It acquires a zero-wait Creator-specific
transaction lock and refuses to operate when any membership already exists for
the Creator or when its fixed audit identity is present. It never selects a
user by email, display name, issuer, subject, or provider claim.

On success, the operation inserts only fixed membership
`membership_tsa_initial_owner`, active version 1 with the non-expiring `Owner`
role and no direct permission grants. The membership, role, and required
`Creator.ManageMembers` audit record commit in the same SQL transaction. The
audit actor is honestly classified as `System`; the reviewed GitHub Environment
approval, operation packet, support identifier, and correlation identifier
retain the separate human administrative authorization evidence. The legacy
membership attribution columns reference the target user to satisfy their
existing foreign-key contract and do not replace the authoritative audit actor.

Bounded evidence includes only fixed resource identifiers, the resulting
membership version, a SHA-256 hash of the opaque target UserId, safe approval
identifiers, outcome, and UTC time. It excludes email, External ID issuer or
subject, tokens, claims, SQL values, and Creator content. The operation has zero
automatic retry. Repository review, merge, and ordinary deployment do not run
it; live execution requires a fresh baseline, a distinct checksum-bound
approval packet, and the normal cleanup and fresh-session denial-proof sequence.

## Baseline allowlist and evidence

The query reads only catalog metadata needed to classify the DbUp journal and
its ordered script names; `planning`, `auth`, and `audit` schemas and owners;
the runtime roles, owners, and memberships; the dedicated administrator
and migration contained users; direct database/schema/object/role permissions;
and counts of required objects by approved schema and type. It never selects
application rows.

Evidence follows `infrastructure/private-sql-admin-operation/evidence.schema.json`.
The baseline accepts only four reviewed states: absent, canonical `At0006`,
the exact `At0009` prerequisite for the bounded `0010`-through-`0012` migration, and complete
through `0012`. `At0009` requires the exact nine-script journal prefix, three
schema owners, five runtime roles and membership shape, including the exact
dbo-owned, empty, authority-free `AdventuresSuiteCompanionPolicyRuntime` role, zero migration
principal, complete permission allowlist, and exact object counts. DbUp journal
records must use the exact
`AdventuresSuite.DatabaseMigrator.Database.Migrations.` prefix. That prefix is
removed only for comparison and bounded evidence against the authoritative
ordered catalog; every other partial state fails closed.
It is size-bounded, rejects additional fields, hashes immutable Azure resource
IDs and database SIDs, and allows only documented names, counts, permissions,
outcomes, and zero-residue fields. Tokens, connection strings, package URLs,
raw claims, environment dumps, arbitrary SQL output, application data, private
content, and unapproved identifiers are prohibited.

## Separate future approvals

Dedicated UAMI/FIC creation, temporary administrator establishment, baseline,
bootstrap, migration, SQL cleanup, administrator restoration, and denial proof
remain independently reviewed live boundaries.
