# AdventuresSuite Planning Engine

**Version:** 1.0

**Status:** Approved Direction

**Last Updated:** August 9, 2026

## Purpose

The Planning Engine owns the private, structured state used to turn an idea
into an actionable Adventure. It supports the Dream, Plan, and Travel stages
without treating operational travel information as public editorial content.

The first reference scenario is The Simonton Adventures' 2027 Spain and
trans-Atlantic Adventure.

## Core Principle

> The Planning Engine owns the plan. AI proposes changes. The Creator approves
> them. The Content Engine publishes an intentionally selected result.

## Ownership Boundary

Every planning aggregate and record belongs to one Creator through a stable
`CreatorId`. User access is evaluated through authenticated Creator membership
and authorization; it is never inferred from a public host or mutable display
value.

Planning data is private by default. It may contain traveler details,
confirmation references, costs, private notes, unpublished dates, and draft
intentions. It must not be stored in public `wwwroot`, returned by public
Content Engine queries, indexed publicly, or exposed merely because related
Destination content is published.

## Planning and Published Content

An `AdventurePlan` is not a published Adventure, Volume, Journey, or
Destination.

```text
Private AdventurePlan
    reviewed and selected by a Creator
        ↓ explicit publication transformation
Public Content Engine records
        ↓
Rendering, Address, QR, Search, and Notification capabilities
```

Publication copies or derives approved information through a defined
application operation. It does not expose the private aggregate or create a
permanent live projection of every planning change. Confirmation numbers,
budgets, personal notes, traveler-private information, and other protected
fields are excluded unless an explicit, safe publishing contract permits them.

## Initial Domain

The first useful aggregate is `AdventurePlan`.

It may contain:

- `AdventurePlanId`
- `CreatorId`
- title and working description
- lifecycle and planning status
- planning date range
- travelers and travel preferences
- destination visits
- itinerary days
- planned activities
- transportation segments
- accommodations
- reservations
- planning notes
- planning tasks
- budget items
- packing items
- version and audit metadata

Each child record receives a stable identity when it must be edited,
referenced, proposed by AI, audited, or synchronized independently.

## Candidate Concepts

### Adventure Plan

The aggregate root for one private planning effort. It enforces Creator
ownership, lifecycle transitions, date consistency, and concurrency.

### Traveler

Represents a participant within an Adventure Plan. Personally identifiable,
health, accessibility, identity-document, and loyalty-program information
requires explicit data classification and must not be added casually to the
initial model.

### Destination Visit

Represents one planned visit to a Destination in this Adventure. It owns the
expected local date range, destination time-zone identity, ordering, and
visit-specific notes. Reusable Destination content does not own one Adventure's
operational schedule.

### Itinerary Day

Represents one local calendar day in the plan. Activities, transportation,
notes, and reservations may be organized beneath it.

### Planned Activity

Represents something proposed, considered, reserved, confirmed, changed,
cancelled, or completed. An activity is not assumed to be a reservation.

### Transportation and Accommodation

Represent operational plan items without binding the domain to an airline,
cruise line, hotel, booking service, or another vendor.

### Reservation

Records the planning state of a reservation and may reference protected
resources. A reservation record does not prove that AdventuresSuite booked or
paid for anything.

### Task, Note, Budget Item, and Packing Item

Support planning work without becoming public story content by default.

## Dates, Times, and Time Zones

- Travel calendar dates use `DateOnly` semantics.
- Local schedule times use `TimeOnly` semantics until an authoritative instant
  is actually known.
- Destination and visit time zones use IANA identifiers.
- System audit events use UTC timestamps.
- A Creator time zone is not a substitute for a destination time zone.
- A deployment, file modification, or AI-response time is not a travel or
  publication timestamp.
- Daylight-saving and international-date-line behavior must be tested using the
  destination's applicable time-zone rules.

## Status

Planning status and public publication status are separate.

Candidate planning states include:

```text
Idea → Draft → Planned → Upcoming → InProgress → Completed → Archived
```

The exact state machine must be approved during domain modeling. No state alone
makes an Adventure public.

Individual operational items may use narrower states such as Proposed,
Reserved, Confirmed, Changed, Cancelled, and Completed.

## Persistence Direction

Interactive planning data should use durable database storage because it needs
authorization, concurrency, transactions, audit history, private records, and
future background work. Existing published editorial content may remain JSON
while this capability is introduced.

Planning consumers depend on repository and application contracts rather than
EF Core, SQL, or database-specific types. Every query and unique constraint
that touches Creator-owned data includes `CreatorId`.

The initial persistence design should include:

- optimistic concurrency
- UTC audit metadata
- migrations
- Creator-scoped indexes and uniqueness
- recoverable deletion or archival policy
- explicit transaction boundaries
- integration tests proving cross-Creator isolation

The database provider is selected in the persistence phase, not assumed by the
domain model.

## Security and Privacy

The Planning Engine requires authentication before a Creator Workspace is
exposed. Authorization must be enforced at application and persistence
boundaries, not only in Razor components.

The initial implementation must classify data before accepting passport data,
payment data, medical details, precise live location, or full booking documents.
Secrets and sensitive confirmation details must not appear in logs, analytics,
AI prompts, exception messages, or public exports.

## Engine Relationships

- Creator Engine supplies identity and tenant boundaries.
- Identity and authorization determine which users may act for a Creator.
- Content Engine owns public editorial records and their publication lifecycle.
- Resource Engine owns files and protected resource metadata.
- AI Engine produces bounded planning proposals.
- Partner Collaboration Engine grants invited travel professionals bounded
  access through a plan-scoped engagement.

Professional collaboration does not create shared ownership. The customer
Creator remains authoritative, agency membership alone grants no customer
access, and professional edits default to reviewable proposals. No speculative
engagement fields belong in the current Planning persistence phase.
- Rendering Engine presents workspace, companion, and public views.
- Notification Engine consumes explicit approved public events, not draft saves.
- Commerce and booking providers remain outside the initial Planning scope.

## Traveler-Ready Outputs

Planning state must support useful private outputs without turning those
outputs into alternate sources of truth.

- The Adventure Travel Playbook produces a versioned, traveler-ready snapshot
  containing approved itinerary sections and selected protected Resources.
- Adventure Calendar Integration projects selected itinerary items into
  standards-compliant or provider-backed calendar events with explicit traveler
  consent.
- AdventuresCompanion consumes a minimized, encrypted offline projection and
  may initiate explicit device-calendar operations.

Generated Playbooks and calendar events retain source plan/item version,
Creator and traveler scope, local-time and time-zone semantics, stale state,
authorization, and audit evidence. They never create publication, membership,
or ownership and cannot write authoritative Planning state back from an
untrusted document or provider payload.

See `docs/architecture/adventure-travel-playbook.md` and
`docs/architecture/adventure-calendar-integration.md`.

Planning also supplies authoritative inputs for Adventure countdowns,
explainable readiness, change-impact evaluation, document linkage,
traveler-specific projections, acknowledgments, Today and Next, contingencies,
decisions, financial deadlines, and reusable templates. These capabilities do
not duplicate or reverse-update the aggregate through presentation state. See
`docs/architecture/adventure-readiness-and-change-management.md`.

## Initial Non-Goals

- Replacing travel agencies, agency CRM systems, GDS platforms, supplier
  booking systems, or professional commercial workflows.

- autonomous booking or purchasing
- a complete Creator Studio
- public exposure of private plans
- storing payment-card or passport data
- real-time collaborative editing
- offline synchronization
- commerce, subscriptions, or fulfillment
- AI directly mutating authoritative plans
- replacing existing JSON editorial content

## First Vertical Slice

The first end-to-end product milestone is **Plan an Adventure with AI**:

1. An authorized Creator creates a private Adventure Plan.
2. The Creator enters travelers, dates, destinations, and preferences.
3. AI returns a structured proposed itinerary.
4. The Creator previews individual proposed operations.
5. The Creator accepts or rejects each operation.
6. The Planning Engine commits only approved operations.
7. The Creator edits the resulting daily plan manually.
8. AdventuresSuite identifies gaps, conflicts, and unresolved tasks.
9. A read-only private summary can be previewed.
10. Nothing becomes public without a separate publication action.

## Definition of Done for the Foundation

- The domain represents the Spain/trans-Atlantic reference scenario.
- All authoritative records are Creator-scoped.
- Private planning and public content are demonstrably separate.
- Date, time, time-zone, and audit semantics are explicit and tested.
- Persistence supports transactions, auditability, and concurrency.
- Authorization and cross-Creator isolation tests exist.
- AI proposals cannot bypass Creator review.
- Each implementation phase remains deployable and reversible.
