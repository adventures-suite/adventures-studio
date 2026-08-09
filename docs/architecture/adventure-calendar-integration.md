# Adventure Calendar Integration

**Status:** Approved Platform Capability

**Last Updated:** August 9, 2026

## Purpose

Adventure Calendar Integration lets an authorized traveler place selected
Planning itinerary items on that traveler's calendar. Tentative planning items
may produce clearly tentative events; confirmed itinerary items may carry
verified operational detail appropriate for calendar exposure.

The Planning Engine remains authoritative. Calendar services and device
calendars are downstream projections, never a second planning database.

```text
AdventurePlan
    -> authorized calendar projection
    -> ICS / device calendar / connected provider adapter
    -> versioned update or cancellation
```

## Ownership and Consent

- Planning Engine owns itinerary status, dates, local times, time zones, and
  confirmed facts.
- Calendar Integration owns projection, delivery, provider mapping, and sync
  state behind provider-neutral contracts.
- AdventuresCompanion provides traveler-facing device-calendar interaction.
- Notification and outbox infrastructure delivers approved asynchronous sync
  work; notification consent does not imply calendar consent.

Each traveler explicitly chooses whether to export or synchronize events to
their own calendar. Creator membership, plan participation, professional
engagement, possession of a link, or another traveler's approval never grants
calendar-write authority. A Creator or travel professional may propose calendar
content but cannot silently write to another person's calendar.

## Calendar Event Lifecycle

Operational Planning status maps deliberately to calendar behavior:

| Planning state | Calendar behavior |
| --- | --- |
| Proposed | Not exported by default |
| Planned | Optional tentative event |
| Reserved | Tentative or confirmed only under explicit policy |
| Confirmed | Confirmed event with verified allowed detail |
| Changed | Idempotent update to the existing event |
| Cancelled | Standards-compliant cancellation or explicit cancelled state |
| Completed | Retained without unnecessary synchronization |

Every projection has a stable event UID, source item identity and version,
calendar sequence, last-modified UTC time, destination-local time zone, sync
status, provider mapping when applicable, and idempotency identity. Updates
must not create duplicates. Stale work cannot overwrite a newer event.

## Event Content

An approved event may contain:

- traveler-safe title and status;
- local start/end values and authoritative IANA time zone;
- destination or meeting location;
- transportation, check-in, or arrival guidance;
- approved contact information and reminders; and
- a secure deep link to authorized AdventuresSuite or Companion detail.

Calendars may surface data on lock screens, watches, shared family calendars,
email clients, backups, and third-party integrations. Therefore events exclude
ticket QR/barcode values, passport or payment data, booking or room PINs,
private notes, precise breadcrumb history, another traveler's private details,
and permanent protected-Resource URLs. Sensitive operational detail remains in
AdventuresSuite behind a secure deep link.

## Time Semantics

Calendar projection uses the itinerary item's destination-local date, local
time, and IANA time-zone identifier. It must preserve daylight-saving and
international-date-line behavior and never substitute the Creator's current
time zone. All-day and floating-time items require explicit semantics.

Adapters may map IANA identifiers to provider-specific values only at their
boundary. Round-trip, DST-transition, ambiguity, invalid-local-time, and
cross-zone travel tests are required.

## Delivery Phases

### Phase 1: Universal ICS

- export one activity, one day, or an approved Adventure selection;
- support stable UID, `SEQUENCE`, status, updates, and cancellation;
- work with Apple Calendar, Outlook, Google Calendar, and standards-compliant
  clients without a provider account connection; and
- provide a privacy-safe default profile.

An imported ICS file may not remain connected to future changes. The product
must state whether an export is a snapshot or a subscription.

### Phase 2: AdventuresCompanion Device Calendar

- explicit Add to Calendar interaction;
- OS calendar permission requested just in time;
- traveler selection of items, target calendar, and reminders;
- stable local mapping and visible stale/update state; and
- useful reduced behavior when permission is denied.

### Phase 3: Connected Provider Synchronization

Provider-neutral adapters may support Microsoft Graph and Google Calendar.
Apple Calendar initially uses ICS or approved device APIs rather than assuming
server-side iCloud access.

Connected synchronization requires delegated OAuth consent, least-privilege
scopes, protected refresh material, revocation, environment isolation,
idempotent inbox/outbox processing, retry and poison handling, reconciliation,
rate limits, and provider-specific deletion/cancellation review. Provider
tokens and calendar identifiers do not enter core Planning contracts.

## Security, Audit, and Failure Behavior

Authorization and traveler consent are reevaluated before each export or sync.
Disconnecting a calendar stops future synchronization but does not modify the
Adventure Plan. Plan-access revocation prevents future detail retrieval and
triggers the approved provider cleanup policy; it does not promise deletion
from backups or copies outside AdventuresSuite control.

Audit records prove connection, consent, export, synchronization, cancellation,
disconnect, and administrative recovery using minimal identifiers and safe
reason categories. Calendar content, provider tokens, attendee addresses,
private notes, raw provider payloads, and protected links are prohibited from
ordinary logs and audit metadata.

Provider unavailability never corrupts Planning state. Failed synchronization
is bounded, retryable, visible, and reconcilable. The product identifies stale
or disconnected calendars without representing them as current.

## Initial Acceptance Gates

- exact Creator, plan, item, traveler, and actor authorization;
- explicit calendar consent and revocation;
- deterministic ICS across repeated generation;
- stable UID and update/cancellation behavior;
- no duplicate events under retry or concurrency;
- correct local-time and time-zone behavior;
- prohibited-data and shared-calendar leakage tests;
- provider-token and webhook/inbox security where connected sync exists;
- safe denial, disconnect, and provider-outage behavior; and
- durable audit intent for protected calendar actions.
