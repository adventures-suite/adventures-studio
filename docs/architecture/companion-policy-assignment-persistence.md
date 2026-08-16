# Companion Information-Policy Assignment Persistence

**Status:** Implementation-ready design; migration and runtime implementation not approved

**Last Updated:** August 16, 2026

## Purpose and activation boundary

This document defines the reviewed persistence, mutation, audit, permission,
runtime-role, migration-evidence, and query-plan design for explicit Companion
Adventure-overview policy assignments. It does not authorize migration `0010`,
role or grant creation, mutation services, endpoint or projection integration,
dependency-injection registration, or `Bearer + Sql` activation.

The code-defined `companion_adventure_overview_v1` profile remains the only
approved profile. Persistence selects an exact profile key and definition
version; it cannot define fields, text, capability links, Resources, or another
presentation expansion. Authorization runs before information-policy
evaluation, and policy can only reduce access.

## Authorization decision

The stable permission is `AdventurePlan.ManageCompanionPolicy`. It applies to
one exact `AdventurePlan` resource instance, is human-only, and requires atomic
mutation audit. It covers creating, changing, expiring, and revoking an
assignment.

An actor must satisfy both gates:

1. an active, effective Creator membership with role `Owner` or
   `Administrator`; and
2. the exact `AdventurePlan.ManageCompanionPolicy` permission in the effective
   server-owned permission set.

The permission is bundled only with `Owner` and `Administrator`. An explicit
permission grant does not make `Planner`, `Contributor`, or `Viewer` eligible
in v1. Unknown roles, grants, or permissions fail closed. `AdventurePlan.View`,
`AdventurePlan.Edit`, traveler participation, mobile bearer scope, external
claims, agency membership, or another Creator's membership never satisfies
policy administration.

## Optimistic-concurrency operations

The provider-neutral mutation boundary exposes four commands:

- `CreateCompanionPolicyAssignment` requires no existing row and produces
  assignment version `1`.
- `ChangeCompanionPolicyAssignment` changes the exact profile selection or
  effective window and requires the current assignment version.
- `ExpireCompanionPolicyAssignment` sets an exclusive future expiry and
  requires the current assignment version.
- `RevokeCompanionPolicyAssignment` performs the irreversible v1 terminal
  transition and requires the current assignment version.

Every command carries the human actor, exact Creator/Adventure/traveler scope,
current participation version, expected assignment version where applicable,
UTC operation time from `TimeProvider`, and bounded audit-event and correlation
identities. Change, expiry, and revocation produce exactly
`ExpectedAssignmentVersion + 1`. The mutation re-reads Adventure ownership,
the exact participation and version, membership role and version, permission,
and current assignment inside the write transaction.

Closed outcomes are `Succeeded`, `AlreadyExists`, `NotFound`,
`ConcurrencyConflict`, `ParticipationChanged`, `Unauthorized`,
`InvalidProfile`, `InvalidEffectiveWindow`, `AlreadyRevoked`, and
`OperationallyUnavailable`. Cancellation propagates separately. Protected
existence is not disclosed before authorization.

## Parent collation inventory

Migration `0002` declares `planning.AdventurePlans.CreatorId` and
`AdventurePlanId` without an explicit collation, so both inherit the database's
actual `DATABASE_DEFAULT`. Migration `0007` does the same for
`planning.TravelerParticipations.CreatorId`, `AdventurePlanId`, and
`TravelerId`. Migration `0004` declares `auth.Users.UserId` as
`Latin1_General_100_BIN2`. Migration `0006` declares
`audit.AuditEvents.AuditEventId` as `Latin1_General_100_BIN2`.

Migration preflight and tests must read `sys.columns.collation_name` for every
referenced parent column. Each child foreign-key column must use that exact
parent collation:

| Child columns | Required storage collation |
|---|---|
| assignment Planning identity columns | exact corresponding parent collation; currently inherited `DATABASE_DEFAULT` |
| assignment `CreatedByUserId`, `UpdatedByUserId` | `Latin1_General_100_BIN2`, matching `auth.Users.UserId` |
| audit detail `AuditEventId` | `Latin1_General_100_BIN2`, matching `audit.AuditEvents.AuditEventId` |
| profile, status, and operation vocabulary | `Latin1_General_100_BIN2` |

Any parent-column disagreement between the Adventure, traveler, and
participation keys fails migration validation. Migration `0010` must not alter
existing Planning identity collations and must not omit foreign keys.

### Exact-match and seek rule

Every lookup or mutation uses two predicates for each parent-compatible
identity component:

1. ordinary parent-compatible equality predicates, which must drive the
   composite index seek; and
2. `Latin1_General_100_BIN2` residual equality predicates over both the stored
   value and parameter, which must pass before accepting the row.

Values written to assignment scope and actor columns are copied from the
authoritative parent rows returned by those exact predicates. Actor values are
likewise copied from exact, active `auth.Users` rows and never from caller text.
Caller text is never inserted directly. Profile keys are compared directly
under their BIN2 storage collation. A case-altered Creator, Adventure, traveler,
user, audit-event, or profile identity cannot be copied, inserted, resolved,
mutated, or accepted into audit evidence. If an approved query shape cannot
retain a bounded seek with the BIN2 checks as residual predicates,
implementation stops; it must not add a speculative index or weaken exact
comparison.

## Assignment table

`planning.CompanionInformationPolicyAssignments` has exactly these columns:

| Column | SQL type | Nullability and meaning |
|---|---|---|
| `CreatorId` | `nvarchar(64)` with matching Planning parent collation | not null |
| `AdventurePlanId` | `nvarchar(64)` with matching Planning parent collation | not null |
| `TravelerId` | `nvarchar(64)` with matching Planning parent collation | not null |
| `ProfileKey` | `varchar(64) COLLATE Latin1_General_100_BIN2` | not null |
| `ProfileDefinitionVersion` | `bigint` | not null, positive |
| `ParticipationVersion` | `bigint` | not null, positive |
| `AssignmentVersion` | `bigint` | not null, positive |
| `Status` | `varchar(16) COLLATE Latin1_General_100_BIN2` | not null; `Active` or `Revoked` |
| `EffectiveFromUtc` | `datetimeoffset(7)` | not null UTC; inclusive |
| `ExpiresAtUtc` | `datetimeoffset(7)` | nullable UTC; exclusive |
| `RevokedAtUtc` | `datetimeoffset(7)` | nullable UTC; present exactly when revoked |
| `CreatedAtUtc` | `datetimeoffset(7)` | not null UTC |
| `UpdatedAtUtc` | `datetimeoffset(7)` | not null UTC |
| `CreatedByUserId` | `nvarchar(64) COLLATE Latin1_General_100_BIN2` | not null |
| `UpdatedByUserId` | `nvarchar(64) COLLATE Latin1_General_100_BIN2` | not null |

The primary key is `(CreatorId, AdventurePlanId, TravelerId)`. Non-cascading
foreign keys target:

- `planning.AdventurePlans (CreatorId, AdventurePlanId)`;
- the existing unique key on `planning.TravelerParticipations
  (CreatorId, AdventurePlanId, TravelerId)`;
- `auth.Users (UserId)` for each actor column.

Participation version is deliberately not part of a foreign key. Advancing a
participation version makes an assignment stale and closed without preventing
the participation update or silently rebinding the assignment.

Named checks require positive versions; the exact status vocabulary; UTC for
all timestamps; `ExpiresAtUtc > EffectiveFromUtc`; `UpdatedAtUtc >=
CreatedAtUtc`; and an exact pairing of `Revoked` with a non-null
`RevokedAtUtc`. An active row has no revocation time. Revocation time cannot
precede creation or the effective start and cannot be later than update time.
Profile keys are trimmed, bounded, and limited to the closed ASCII vocabulary
shape; the application catalog still decides whether the exact key and
definition version are known.

There is no cascading delete, repository delete operation, or runtime delete
grant. Revocation is terminal. Administrative retention or correction is a
separate governed operation, not ordinary application behavior.

Every update includes the exact expected assignment version in its predicate
and advances it by exactly one. A zero-row update is a concurrency failure;
there is no last-write-wins behavior, version skip, retry, resurrection, or
transition from `Revoked` back to `Active`.

Assignment version, code-owned profile-definition version, participation
version, user security version, Creator-membership version, and later
projection version are independent values. None may be substituted for,
derived from, or advanced on behalf of another. Later projection and ETag
integration must bind every applicable version without changing this table's
concurrency semantics.

## Append-only audit detail

`audit.CompanionInformationPolicyAssignmentEvents` has schema version `1` and
the following bounded columns:

- `AuditEventId nvarchar(64) COLLATE Latin1_General_100_BIN2`;
- `SchemaVersion int` fixed to `1`;
- Creator, Adventure, and traveler scope using the corresponding assignment
  storage collations;
- `Operation varchar(16) COLLATE Latin1_General_100_BIN2`, limited to
  `Created`, `Changed`, `Expired`, or `Revoked`;
- nullable previous and required resulting assignment versions;
- nullable previous and required resulting participation versions;
- nullable previous and required resulting profile keys and definition
  versions;
- nullable previous and required resulting statuses;
- nullable previous and required resulting effective, expiry, and revocation
  UTC timestamps.

`AuditEventId` is both the primary key and a non-cascading foreign key to
`audit.AuditEvents(AuditEventId)`. The detail table deliberately has no foreign
key to the mutable assignment row: audit retention and legal hold remain
independent from assignment lifecycle. Create details require every previous
field to be null and resulting assignment version `1`. Other operations require
complete previous state and a resulting assignment version exactly one greater.
Operation, lifecycle, and timestamp combinations must agree.

The parent envelope records permission
`AdventurePlan.ManageCompanionPolicy`, resource type `AdventurePlan`, exact
Adventure resource ID, human actor, successful outcome, prior/resulting
assignment versions, correlation ID, and UTC occurrence time. Assignment,
ordinary audit envelope, and audit-detail insert execute through one SQL
connection and transaction and commit atomically or all roll back.

The Audit platform owner owns retention, legal hold, evidence access, and
eventual governed disposal. Runtime access is append-only and contains no plan
content, traveler names, claims, tokens, URLs, or arbitrary metadata.

## Runtime database roles

The existing `AdventuresSuiteCompanionReadRuntime` receives only `SELECT` on
the assignment table, with explicit `DENY INSERT, UPDATE, DELETE`. It receives
no audit-detail access.

A new administrator-created, dbo-owned
`AdventuresSuiteCompanionPolicyRuntime` receives only:

- `SELECT, INSERT, UPDATE` on the assignment table and `DENY DELETE`;
- `SELECT` on the exact Adventure and traveler-participation objects required
  for transactional validation;
- `INSERT` on `audit.AuditEvents` and the policy audit-detail table;
- `DENY UPDATE, DELETE` on both audit tables; and
- `DENY ALTER` on `planning` and `audit`.

It receives no schema ownership, fixed-role membership, parent-role membership,
role administration, broad schema DML, membership-management authority, or
Companion read-role membership. The migration and application principals remain
distinct. The role must exist as an administrator prerequisite because the
restricted migration catalog does not include role-administration authority.
Migration `0010` may grant and deny only against that exact pre-existing role
and the exact pre-existing `AdventuresSuiteCompanionReadRuntime` role. It must
not create, alter ownership of, add members to, nest, drop, or otherwise
administer either role.

## Migration and evidence boundary

### Mandatory pre-DDL preflight

Before the migration transaction executes any DDL or permission statement, the
reviewed operation must prove all of the following from bounded metadata:

- the journal is the exact ordered, fully qualified `0001` through `0009`
  prefix with no missing, duplicate, reordered, extra, or future entry;
- the referenced Adventure, traveler-participation, user, and audit-event
  tables and exact primary/unique keys exist;
- every referenced parent column has its expected name, type, length,
  nullability, and actual collation, including agreement across the Planning
  composite keys;
- `AdventuresSuiteCompanionReadRuntime` and
  `AdventuresSuiteCompanionPolicyRuntime` both already exist, are owned by
  `dbo`, have no members or parent roles, and have no unapproved permissions;
  and
- the complete reviewed `At0009` object, constraint, index, permission, and
  application-fingerprint baseline is exact.

Missing, substituted, differently owned, differently collated, additional, or
ambiguous metadata stops before DDL. The operation must not repair, normalize,
create a role, or continue with inferred compatibility.

Clean installation must produce exactly ten ordered journal entries and the
same final schema as an exact `0009` to `0010` upgrade. Upgrade preflight
requires the complete reviewed `At0009` state and selects only migration
`0010`. The migration package, catalog checksum, operation ceiling,
administrator baseline, state capture, fingerprints, and classifications must
be extended explicitly to `0010`, never made generic or unbounded.

Evidence must prove:

- exact parent and child collations and compatible foreign keys;
- every named table, column, key, constraint, index, role, grant, and denial;
- zero role members during migration validation;
- empty new tables immediately after migration;
- unchanged fingerprints for every existing application-data table;
- exact `At0009` after a pre-commit failure or exact `At0010` after success;
- no partial objects, grants, journal entries, or unexpected principals; and
- clean database/login cleanup and zero disposable resource residue on every
  disposable-SQL path, including cancellation and failure.

There is no destructive rollback or automatic retry. Ambiguous or partial state
is `Unexpected` and requires reviewed repair-forward handling.

## Query shapes and plan acceptance

The Adventure list starts from the existing user-first authorized
participation index, applies exact effective-state predicates, and joins the
assignment primary key by Creator, Adventure, and traveler. Detail resolves the
exact Adventure and participation before seeking the same assignment key.
Both shapes require matching participation version, active/effective assignment,
and known profile key and definition version before projection.

Disposable SQL tests use representative bounded cardinalities and retained
actual plans. Acceptance requires:

- compatible equality predicates drive seeks on participation and assignment
  composite keys;
- every BIN2 identity comparison remains a residual predicate and is evaluated
  before a row is accepted;
- no assignment or participation scan for the approved list/detail shapes;
- at most one assignment estimate/result per exact participation;
- Creator remains the leading scope and list bounds remain effective;
- no missing-index recommendation for either approved shape; and
- no speculative status/effective-window index.

Negative tests cover case-altered Creator, Adventure, traveler, created/updated
user, audit-event, and profile identifiers. They also cover unknown profiles,
stale participation versions, revoked/expired assignments, duplicate or
contradictory rows, role/permission ambiguity, and cross-Creator substitution.

If the seek-plus-residual plan cannot be demonstrated for any exact-match
query, implementation stops for renewed design review.
