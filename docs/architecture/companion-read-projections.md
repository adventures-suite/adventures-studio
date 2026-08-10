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
