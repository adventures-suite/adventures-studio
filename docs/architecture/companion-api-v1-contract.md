# AdventuresCompanion API v1 Contract Baseline

**Status:** Approved Initial Contract Baseline

**Last Updated:** August 9, 2026

## Purpose

This document makes the current AdventuresCompanion read contract concrete
enough to implement, generate as OpenAPI 3.1, exercise through Scalar, and
consume from the MAUI application. It reflects what AdventuresSuite knows now
and deliberately permits additive evolution within v1.

It does not expose the Planning aggregate or predict every future feature.
Fields with unresolved privacy, ownership, or operational semantics are omitted
until their owning design is approved.

## Compatibility Position

- The base path is `/api/v1/companion`.
- Existing required fields keep their name, type, meaning, and nullability for
  the supported life of v1.
- New optional fields and endpoints may be added to v1.
- Removing a field, changing its meaning or type, making an optional field
  required, or narrowing a previously valid value requires a new major version.
- Security-sensitive status values are closed. A client that cannot safely
  interpret one presents an unavailable/update-required state.
- The generated OpenAPI document and compatibility report are retained for each
  release.

The first implementation uses fictional deterministic projections. Production
activation remains gated on mobile OAuth, authoritative participation,
Planning-service authorization, traveler information policy, and protected
Resource delivery.

## Common HTTP Rules

- Requests and ordinary responses use UTF-8 `application/json`.
- Errors use `application/problem+json`.
- Protected endpoints require `Authorization: Bearer {access-token}`.
- Responses include a server-generated `X-Support-Id` header.
- Cacheable projections return an `ETag` and accept `If-None-Match`.
- `304 Not Modified` has no response body and does not extend authorization or
  offline retention by itself.
- Dates use `YYYY-MM-DD`; local times use `HH:mm:ss`; IANA time zones remain
  explicit; authoritative instants are RFC 3339 UTC values ending in `Z`.
- Identities are bounded, opaque, case-sensitive strings. Clients display,
  store, and return them but never parse meaning from them.
- JSON property names use camel case.
- Unknown JSON properties are ignored by clients.

DTOs are constructed only through explicit, hand-written mappings from
authorized application projections. They are never produced through reflection
copying, convention mapping, matching-property helpers, or serialization of a
Dapper record or domain aggregate. API DTO properties form an intentional
field allowlist.

## Initial Endpoint Matrix

| Operation ID | Route | Purpose | Policy | Cache | Audit |
| --- | --- | --- | --- | --- | --- |
| `ListCompanionAdventures` | `GET /adventures` | List current, committed, and planned Adventures visible to the traveler | Active user and current Adventure participation for each result | Private ETag; short freshness | Aggregate operational read; no per-item audit by default |
| `GetCompanionAdventure` | `GET /adventures/{adventureId}` | Retrieve the traveler-safe overview for one Adventure | Current participation, ownership, and information profile | Private ETag | Policy-classified protected read |
| `GetCompanionToday` | `GET /adventures/{adventureId}/today` | Retrieve Today and Next in the Adventure's applicable local context | Current participation and itinerary visibility | Private ETag; short freshness | Policy-classified protected read |
| `GetCompanionItinerary` | `GET /adventures/{adventureId}/itinerary` | Retrieve authorized itinerary days and items | Current participation and itinerary visibility | Private ETag | Policy-classified protected read |
| `GetCompanionReadiness` | `GET /adventures/{adventureId}/readiness` | Retrieve traveler-visible readiness summary and actions | Current participation and readiness profile | Private ETag; short freshness | Sensitive actions excluded; protected read classification |
| `GetCompanionPlaybook` | `GET /adventures/{adventureId}/playbook` | Retrieve the structured traveler Playbook projection | Current participation and selected Playbook profile | Private ETag | Required when policy marks included sections sensitive |
| `DownloadCompanionResource` | `GET /resources/{resourceId}/content` | Stream one currently authorized protected Resource | Current participation, Resource ownership, classification, rights, malware, and retention policy | No shared cache; range policy by media type | Audited protected download |

All route fragments above follow `/api/v1/companion`. A caller does not submit a
Creator ID. The server resolves authoritative Creator ownership from the target
resource and current participation. Another Creator's or traveler's identifier
returns the same safe unavailable response as an unknown identifier where
enumeration matters.

## Shared Projection Metadata

Every JSON response root includes:

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `schemaVersion` | string | yes | Wire schema understood by the client; initially `1.0` |
| `projectionVersion` | string | yes | Opaque version of this authorized projection |
| `generatedAtUtc` | UTC timestamp | yes | Server generation time |
| `freshUntilUtc` | UTC timestamp | yes | Time after which the client visibly marks data stale |
| `syncCursor` | string | no | Opaque cursor for a future compatible incremental read |

`projectionVersion` and `syncCursor` are not concurrency grants, access tokens,
or authorization evidence.

## Adventure Collection

`CompanionAdventureCollectionDto` contains the shared metadata plus:

| Field | Type | Required |
| --- | --- | --- |
| `adventures` | array of `CompanionAdventureSummaryDto` | yes |
| `continuationToken` | string | no |

`CompanionAdventureSummaryDto`:

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `adventureId` | opaque string | yes | API resource identity |
| `title` | bounded string | yes | Traveler-safe working title |
| `subtitle` | bounded string | no | Optional safe context |
| `status` | enum | yes | `planned`, `committed`, `inProgress`, `completed` |
| `startDate` | date | yes | Adventure-local calendar date |
| `endDate` | date | yes | Adventure-local calendar date |
| `primaryTimeZone` | IANA string | yes | Countdown and initial display context |
| `countdown` | `CompanionCountdownDto` | yes | Derived presentation input, not persisted ticks |
| `heroResource` | `CompanionResourceSummaryDto` | no | Authorized display image metadata |
| `offlineState` | enum | yes | `notAvailable`, `available`, `stale`, `expired`, `revoked` |

`CompanionCountdownDto` contains `targetDate`, optional `targetLocalTime`,
`timeZone`, `evaluatedAtUtc`, and `state`. `state` is `future`, `today`,
`inProgress`, or `complete`. The device may animate a display from these inputs;
it does not write countdown ticks to the server.

## Adventure Detail

`CompanionAdventureDto` contains shared metadata plus:

- `adventureId`, `title`, optional `subtitle`, and safe description;
- `status`, `startDate`, `endDate`, and `primaryTimeZone`;
- `countdown`;
- optional authorized hero Resource;
- ordered `destinations`;
- `nextItemSummary` when one is available;
- `readinessSummary` with counts and safe state only;
- available capability links such as itinerary, Today, readiness, Playbook, and
  offline package; and
- `informationProfileVersion`, an opaque indication of the policy used to
  construct the projection.

`CompanionDestinationSummaryDto` contains `destinationVisitId`, traveler-safe
name, start and end dates, IANA time zone, sequence, optional hero Resource, and
optional approximate map position. Exact lodging, private notes, and live
traveler locations are absent.

Capability links are same-origin relative API paths selected by the server.
Their presence improves discovery but does not grant authorization.

## Today and Next

`CompanionTodayDto` contains shared metadata plus:

- `adventureId`;
- `localDate` and `timeZone` used for the projection;
- `state`: `beforeAdventure`, `active`, `afterAdventure`, or `noScheduledItems`;
- ordered `todayItems`;
- optional `nextItem`; and
- optional safe day-level notice.

`CompanionScheduleItemDto` contains:

- `itemId` and `itemType`;
- title and optional safe summary;
- local date;
- optional start and end local times;
- IANA time zone;
- `timeStatus`: `scheduled`, `allDay`, `toBeConfirmed`, or `cancelled`;
- `operationalStatus`: `proposed`, `reserved`, `confirmed`, `changed`,
  `cancelled`, or `completed`;
- optional safe place summary;
- optional transportation summary;
- zero or more authorized Resource summaries;
- `requiresAcknowledgment`; and
- optional safe action label and same-origin action route.

Reservation confirmations, tickets, private notes, payment details, and
traveler lists are never embedded in a schedule item.

## Itinerary

`CompanionItineraryDto` contains shared metadata, `adventureId`, and ordered
`days`.

`CompanionItineraryDayDto` contains:

- `itineraryDayId`;
- `localDate` and IANA `timeZone`;
- day number and optional traveler-safe title;
- destination visit identity and safe destination name;
- ordered `items` using `CompanionScheduleItemDto`;
- optional safe day summary; and
- `hasMaterialChange` plus optional acknowledgment identity.

Days are ordered by local date and stable server sequence. Items are ordered by
explicit sequence and local time, with unscheduled items placed deterministically.
The map projection will later reference these same stable identities.

## Readiness

`CompanionReadinessDto` contains shared metadata plus:

- `adventureId`;
- `overallState`: `unknown`, `attentionRequired`, `inProgress`, or `ready`;
- `evaluatedAtUtc`;
- `categories`; and
- traveler-visible `actions`.

`CompanionReadinessCategoryDto` contains a closed category, state, safe title,
and counts. Initial categories are `travel`, `lodging`, `activities`,
`documents`, `tasks`, and `packing`.

`CompanionReadinessActionDto` contains an opaque action identity, category,
safe title, optional due date/local time/time zone, urgency, completion state,
and same-origin action route when the current app version supports it. It does
not expose another traveler's completion, private task, financial amount,
medical information, or document content.

Readiness is an explainable projection, not a guarantee that travel is safe,
booked, legally permitted, or disruption-free.

## Structured Playbook

`CompanionPlaybookDto` contains shared metadata plus:

- `adventureId`;
- `playbookVersion` and source `planVersion`;
- generated and expiry timestamps;
- `staleState`: `current`, `stale`, `expired`, or `revoked`;
- ordered sections; and
- selected protected Resource summaries.

`CompanionPlaybookSectionDto` contains a stable section identity, section type,
title, optional safe introduction, and typed entries. Initial section types are
`overview`, `itinerary`, `transportation`, `accommodations`, `activities`,
`readiness`, `packing`, `contingencies`, and `resources`.

Entries reuse approved Companion DTOs or explicitly defined Playbook entry
schemas. They are never arbitrary serialized Planning records or unreviewed
HTML. Rich text requires a separately approved sanitized representation.

This endpoint supports the structured data behind an Italy-style travel guide;
PDF, EPUB, or other generated editions are protected Resources rather than the
authoritative mobile data model.

## Resource Summary and Delivery

`CompanionResourceSummaryDto` contains:

- `resourceId`;
- media type and bounded byte length when known;
- safe title, alternative text, and optional attribution;
- checksum algorithm and value when offline integrity requires them;
- `availability`: `available`, `processing`, `blocked`, `expired`, or `revoked`;
- `offlineEligible` and optional `retainUntilUtc`; and
- same-origin `contentPath` only when currently deliverable.

The content endpoint reauthorizes every request. It supports `HEAD` and bounded
range requests only for approved media types. It never redirects to a permanent
public location. A later implementation may issue a short-lived provider URL
without changing the Resource identity, provided the Resource architecture's
authorization, expiry, audit, and leakage controls remain intact.

## Safe Problems

All problems follow an AdventuresSuite extension of RFC 9457 Problem Details:

| Field | Type | Required |
| --- | --- | --- |
| `type` | HTTPS URI | yes |
| `title` | safe string | yes |
| `status` | integer | yes |
| `code` | stable snake-case string | yes |
| `supportId` | opaque server-generated string | yes |
| `retryable` | boolean | yes |
| `retryAfterSeconds` | integer | no |

Initial safe codes include `invalid_request`, `authentication_required`,
`insufficient_scope`, `resource_unavailable`, `projection_expired`,
`precondition_failed`, `conflict`, `rate_limited`, `upgrade_required`, and
`temporarily_unavailable`.

Validation errors may include a bounded property-name-to-safe-code map. They do
not echo rejected values. Resource and participation denials use
`resource_unavailable` when distinction would enable enumeration.

## Example Adventure Collection

```json
{
  "schemaVersion": "1.0",
  "projectionVersion": "pv_demo_01",
  "generatedAtUtc": "2026-08-09T22:00:00Z",
  "freshUntilUtc": "2026-08-09T22:15:00Z",
  "adventures": [
    {
      "adventureId": "adv_demo_spain_2027",
      "title": "Spain & Trans-Atlantic Cruise",
      "subtitle": "Barcelona to Fort Lauderdale",
      "status": "planned",
      "startDate": "2027-10-25",
      "endDate": "2027-11-15",
      "primaryTimeZone": "Europe/Madrid",
      "countdown": {
        "targetDate": "2027-10-25",
        "targetLocalTime": null,
        "timeZone": "Europe/Madrid",
        "evaluatedAtUtc": "2026-08-09T22:00:00Z",
        "state": "future"
      },
      "offlineState": "available"
    }
  ],
  "continuationToken": null
}
```

All examples are fictional and contain no actual traveler, reservation,
credential, confirmation, or protected-delivery data.

## Explicitly Deferred from the Initial Read Contract

- user-to-traveler participation persistence and invitation commands;
- device installation and push-token registration;
- polls, collaboration, acknowledgments, and task completion;
- calendar commands;
- breadcrumb capture and synchronization;
- media upload and memory capture;
- map and offline-package schemas;
- booking changes, purchasing, payment, passport, medical, loyalty, or full
  confirmation data;
- arbitrary rich text;
- a general-purpose query language; and
- public or anonymous access to private Companion projections.

Deferral means the initial schemas do not reserve speculative fields. Later
features add explicit DTOs and policies through reviewed, compatible changes.

## Implementation Gate

The next implementation increment may add provider-neutral DTO records,
deterministic fictional projection services, Minimal API route declarations,
complete endpoint metadata, OpenAPI 3.1 generation, Scalar, examples, and
contract tests in the independent `AdventuresSuite.Api` host.

It must not enable production data, Azure deployment, real bearer
authentication, protected downloads, or persistence queries until the
activation gates in `companion-openapi.md` pass.
