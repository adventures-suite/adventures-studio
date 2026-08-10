# AdventuresCompanion API and Synchronization

**Status:** Approved Platform Requirement and Architecture Direction

**Last Updated:** August 9, 2026

## Purpose

AdventuresCompanion uses a versioned HTTPS API to retrieve traveler-specific
Adventure information as JSON and to access explicitly authorized media,
documents, maps, or offline packages. It never connects to Azure SQL and never
receives Dapper persistence records or server domain aggregates.

## Binding Boundary

```text
Azure SQL
    -> Dapper persistence adapter
    -> application query service
    -> authorization and traveler information policy
    -> purpose-built Companion DTO
    -> versioned JSON response
    -> encrypted, bounded device cache
```

Each boundary is deliberate:

- Dapper and SQL types remain server-side infrastructure.
- Application services compose an authorized projection rather than exposing a
  table shape.
- Information policy removes fields the authenticated traveler does not need or
  may not see.
- Mobile DTOs are stable API contracts independent from SQL migrations and
  internal domain refactoring.
- JSON serialization occurs only after validation, authorization, minimization,
  and safe link generation.

Companion must not receive connection strings, SQL queries, Dapper records,
persistence models, complete Planning aggregates, provider credentials, raw
audit/events, server configuration, or permanent protected-Resource URLs.

## Transported Data

Connected experiences receive:

- versioned JSON responses;
- authorized media streams or short-lived media delivery URLs;
- protected documents through narrowly scoped, expiring delivery operations;
- licensed map tiles or offline map/place packages from approved adapters; and
- minimal push payloads containing opaque routing identifiers.

"JSON files" means JSON API representations and versioned offline JSON
manifests. Normal connected use calls REST endpoints; it does not depend on
public loose `.json` files or mirror the server's content directories.

## API Shape

Illustrative read endpoints include:

```text
GET /api/v1/companion/adventures
GET /api/v1/companion/adventures/{adventureId}
GET /api/v1/companion/adventures/{adventureId}/today
GET /api/v1/companion/adventures/{adventureId}/itinerary
GET /api/v1/companion/adventures/{adventureId}/map
GET /api/v1/companion/adventures/{adventureId}/readiness
GET /api/v1/companion/adventures/{adventureId}/polls
GET /api/v1/companion/adventures/{adventureId}/notifications
GET /api/v1/companion/adventures/{adventureId}/offline-package
```

Mutations use explicit commands with idempotency and concurrency semantics:

```text
POST /api/v1/companion/polls/{pollId}/responses
POST /api/v1/companion/announcements/{announcementId}/acknowledgments
POST /api/v1/companion/tasks/{taskId}/completion
POST /api/v1/companion/device-installations
DELETE /api/v1/companion/device-installations/{installationId}
```

Exact routes are approved during API design. Core contracts do not depend on
ASP.NET Core controllers, Dapper, SQL Server, MAUI, or one serializer.

## Contract Rules

- APIs use explicit major versions and additive forward-compatible evolution.
- Responses declare schema/projection version, generation time, freshness or
  expiration, and an opaque synchronization cursor where applicable.
- Dates, local times, UTC timestamps, IANA zones, money, identifiers, enums,
  nullability, precision, and units have explicit wire formats.
- Unknown JSON properties can be ignored safely; unknown required enum values
  fail closed where their meaning affects security or authority.
- Collections are bounded and paginated where necessary.
- Errors use safe, stable problem categories without private details.
- URLs are allowlisted, purpose-scoped, HTTPS, and short-lived when protected.
- API documentation and contract compatibility tests are generated from the
  approved contract rather than inferred from database tables.

## Authorization and Information Policy

Every request validates mobile token issuer, audience, lifetime, and scope,
then independently resolves the platform actor and authoritative server-side
authorization facts. Tokens, device identity, links, cached Creator IDs, and
JSON fields never prove membership, traveler participation, professional
engagement, or resource ownership.

Queries and cache keys include Creator and Adventure scope. The server applies
the traveler's current information policy on every response. Plan revocation,
participation removal, user disablement, permission changes, and protected-
resource withdrawal take effect independently from the device's cached claims.

## Offline Projection and Synchronization

The initial successful sync provides a bounded authorized snapshot. Subsequent
sync may use `ETag`, conditional GET, opaque cursors, and incremental changes.
The protocol supports:

- stable resource identities and projection versions;
- additions, replacements, deletions, and revocation tombstones;
- idempotent retryable commands;
- optimistic concurrency and explicit conflict responses;
- interrupted/partial transfer recovery;
- full resynchronization when a cursor or schema expires;
- visible last-synchronized, stale, expired, and access-revoked state; and
- deterministic clearing after logout, revocation, retention expiry, or lost-
  device response.

Offline packages contain a signed or integrity-protected manifest, authorized
JSON projections, and explicitly selected encrypted media. They record Creator,
Adventure, intended user/device, source projection versions, generated time,
expiry, checksum, contents, and required minimum app schema.

The device cache is encrypted and partitioned by account and Creator. It is
never a durable replica or authority for server Planning state.

## Media and Protected Resources

Large binary content is not embedded as base64 inside ordinary JSON. JSON
contains safe metadata and a short-lived delivery operation or opaque Resource
identity. The server reauthorizes protected downloads; expiry, revocation,
range requests, integrity, malware state, media rights, and offline retention
remain enforceable.

Media upload uses a narrowly scoped, short-lived authorization followed by
server-side Resource registration. A successful blob transfer does not itself
create an authorized Resource or publish content.

## Notifications and Synchronization

A push payload is a wake-up or navigation hint, not an Adventure update. It may
contain a notification category, opaque notification/subject identity, and safe
deep-link route. It excludes private itinerary content, confirmation details,
precise location, credentials, authorization claims, and permanent URLs.

On activation, Companion authenticates and retrieves current JSON. A stale,
duplicated, delayed, reordered, or forged push cannot become authoritative.
The in-app notification center is server-backed because push delivery is not
guaranteed.

## Observability and Audit

Operational telemetry uses route templates and bounded result categories. It
does not record JSON bodies, raw URLs, query values, SQL parameters, media,
protected-resource links, tokens, traveler content, precise location, or
unbounded Creator/Adventure dimensions.

Durable audit proves protected reads, commands, exports, offline-package
issuance, device registration, acknowledgment, and revocation where required.
Audit metadata does not duplicate the returned JSON or downloaded media.

## Definition of Done

- Companion can operate from versioned JSON and authorized media alone.
- No server persistence or provider model crosses the API boundary.
- Every response is reauthorized and minimized for the traveler and purpose.
- Contract versions evolve independently from SQL migrations.
- Offline synchronization handles retry, deletion, revocation, schema change,
  conflict, corruption, and expiry deterministically.
- Push notifications cannot inject or replace authoritative state.
- Protected media uses expiring, scoped delivery and never permanent URLs.
- IDOR, cross-Creator, cross-traveler, replay, enumeration, stale-cache,
  prohibited-data, compatibility, accessibility, and failure tests pass.
