# Companion Read Projection Boundary

**Status:** Implemented below the HTTP composition layer
**Last Updated:** August 10, 2026

## Boundary

Authoritative Companion reads use this dependency direction:

```text
API composition (future)
    -> provider-neutral application query contracts
    -> Companion application projections
    -> private Dapper persistence rows and explicit mapping
    -> Azure SQL
```

The application query contracts contain no Dapper, SQL Server, ASP.NET Core,
or API DTO types. The SQL adapter owns its row types privately. Mapping is
handwritten from rows to application projections; the existing API mapping
boundary remains separate and is not activated by this slice.

## Authoritative Traveler Access

`planning.Travelers` describes a person within one Adventure Plan. It is not a
platform identity and grants no access. Creator membership grants access to a
Creator workspace, but it does not grant a mobile user access to every plan.

An accepted, active, unexpired `planning.TravelerParticipations` row is the
explicit binding between an authenticated platform `UserId` and one plan's
`TravelerId`. The binding is scoped by `CreatorId` and `AdventurePlanId`, has
its own optimistic version, and can be revoked. An authoritative Companion
query requires all of the following inside the SQL statement:

- exact Creator ownership;
- the authenticated `UserId`;
- a current active Creator membership with the caller's exact membership
  version; and
- an accepted, effective, unexpired participation for that same plan.

The query never loads a broader set and filters it in memory. Missing,
cross-Creator, cross-traveler, stale-membership, expired, invited, revoked, and
archived cases all produce the same unavailable result.

## Projection Rules

- Lists are capped at 50 rows and ordered by `StartDate`, then opaque
  `AdventurePlanId`.
- Only `Planned`, `Upcoming`, `InProgress`, and `Completed` planning states are
  eligible. They map explicitly to Planned, Committed, InProgress, and
  Completed Companion lifecycle values.
- Archived, Idea, and Draft plans are not traveler-visible.
- Calendar values remain `DateOnly`; destination time zones remain exact IANA
  identifiers. Authorization evaluation and version timestamps are UTC.
- Plan and participation versions plus their latest UTC update instant provide
  sufficient input for a private ETag when the HTTP layer is composed later.
- The primary time zone is the first destination visit's zone by deterministic
  sequence, with `Etc/UTC` only for a plan that has no destination visit. No
  local clock time is invented.

## Activation and Permissions

Migration `0007_create_traveler_participations.sql` creates the binding and its
authorized-list index. It intentionally creates no runtime grant. The deployed
API remains disabled and has no SQL permission until the projection gate,
Managed Identity review, and HTTP composition slice are separately approved.

The authoritative SQL gate creates a disposable real SQL Server database and
verifies list/detail behavior, lifecycle handling, isolation, stale versions,
bounds, deterministic ordering, revocation, the required index, and absence of
a missing-index recommendation for the scoped access path.

## Authoritative Bearer Access Context Foundation

The inert authoritative access-context foundation accepts only a configured
identity-provider identifier and the issuer and subject already validated by
the bearer transport. Issuer and subject remain exact, ordinal,
case-sensitive values. Creator, traveler, membership, role, permission,
ownership, information-policy, and revocation claims are never authorization
inputs.

The SQL resolver uses the persisted exact-key hash together with binary-
collated provider, issuer, and subject predicates. It then establishes the
active platform user, current Creator membership, accepted traveler
participation, Adventure ownership, and exact `AdventurePlan.View` authority
from server-owned roles or grants. Successful results carry the exact external-
identity mapping plus user security, membership, participation, and information-
policy versions. Evaluation time comes only from an injected `TimeProvider`.
A separate projection-read contract requires those facts to be rechecked within
the later data read rather than treating resolution as a durable authorization
decision.

The default information policy is closed. The resolver is not registered with
the API host, the `Bearer + Sql` startup prohibition remains in place, and no
endpoint can consume this foundation yet. No authorization-context cache is
permitted by this increment.

### Adventure overview profile decision

`companion_adventure_overview_v1` is the only code-defined profile in this
increment. It is not a Creator default. It can be selected only by an exact,
active, unexpired assignment to one Creator-owned Adventure/traveler
participation at that participation's current version. Missing assignments,
unknown profile keys, scope mismatches, stale participation versions, and
inactive, expired, or revoked assignments fail closed without fallback.
An assignment binds both the profile key and exact code-definition version;
deploying a new definition never silently upgrades an existing assignment.
Effective time is read from an injected `TimeProvider`; the start instant is
inclusive and the optional expiry instant is exclusive.

The profile permits only:

- opaque Adventure and destination-visit identities;
- the Adventure title and destination names after the authorized Creator has
  explicitly assigned this profile to that exact participation;
- Adventure status, local dates, primary IANA time zone, and derived countdown
  inputs without invented times or persisted ticks; and
- destination local dates, IANA time zones, and presentation sequence.

Existing `AdventurePlans.Title` and `DestinationVisits.Name` values are not
automatically classified as traveler-safe. The explicit assignment is the
Creator's approval to disclose those values under this closed profile. Detail
presentation uses generic description and readiness text, emits an empty
capability-link collection, and omits hero Resources and next-item content.

Traveler lists and names, preferences, destination notes, WorkingDescription,
Planning notes and tasks, budgets, reservations and confirmation references,
accommodation and private transportation detail, protected Resources and
paths, precise or live location, sensitive readiness actions, itinerary and
surprise content, protected documents, Playbook content, and authorization or
persistence identifiers remain excluded.

This slice defines only the catalog, assignment contract, and closed evaluator.
The assignment provider supplied here always returns no assignment. Persistence,
mutation authorization and audit, runtime grants, indexes, projection mapping,
ETags, service registration, endpoints, and Bearer + SQL activation remain
separately reviewed work.

The implementation-ready persistence proposal, including the approved
parent-compatible seek plus BIN2 residual comparison rule, is defined in
`docs/architecture/companion-policy-assignment-persistence.md`. That design is
documentation only and does not authorize migration `0010` or activation.

Activation remains dependent on separately reviewed work:

1. add separately reviewed persistence for explicit profile assignments and
   their positive versions, effective windows, and revocation state;
2. add a forward migration granting the Companion runtime narrowly scoped
   reads of `auth.Users` and `auth.ExternalIdentities`;
3. validate any User-first participation and Adventure lookup indexes against
   real disposable-SQL execution plans before adding them in that migration;
4. integrate the recheck into projection reads and bind the security,
   membership, participation, and policy versions into ETags; and
5. enable `Bearer + Sql` only after the migration is proven and deployment
   identity and configuration are separately approved.
