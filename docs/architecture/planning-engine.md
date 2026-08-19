# AdventuresSuite Planning Engine

**Version:** 1.0

**Status:** Approved Direction

**Last Updated:** August 19, 2026

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

## Diverse Adventure Modes

AdventuresSuite plans Adventures rather than assuming a conventional
flight-hotel-tour vacation. The model must support combinations of travel and
experience capabilities needed by motorcycle touring, RV and campervan travel,
cycling and bikepacking, hiking and trekking, sailing and private boating, rail
journeys, cruises, overlanding and off-road travel, winter sports, diving and
expeditions, pilgrimages, accessible and multigenerational travel, festivals,
sporting events, and other emerging forms of travel.

Do not introduce one rigid `TripType` whose value controls domain behavior.
Describe an Adventure through composable, stable facets and capabilities such
as transportation modes, route style, terrain and surface, activity intensity,
daily distance or duration limits, accommodation patterns, equipment and
readiness needs, accessibility, group composition, and traveler preferences.
Facets support discovery and presentation; validated plan records remain the
authority for actual routes, dates, tasks, and operational state.

Motorcycle touring is the first proving scenario. The architecture must be
able to represent scenic-versus-fast route preference, paved/gravel/off-road
surface preference, daily distance and riding-time limits, fuel-range planning,
rest days, weather and closure contingencies, ferries, tolls, borders and
permits, vehicle-document readiness, motorcycle-friendly stays and secure
parking, gear and maintenance tasks, group-rider differences, and transport to
or from the riding route. These concepts must be introduced through reusable
contracts rather than motorcycle-specific conditionals scattered through the
Planner.

Route, weather, closure, fuel, border, safety, accessibility, price, and
availability information retains source, jurisdiction, freshness, assumptions,
and confidence. An Idea or filter match never proves that a route or service is
currently safe, legal, passable, accessible, available, or booked.

## Candidate Concepts

### Adventure Plan

The aggregate root for one private planning effort. It enforces Creator
ownership, lifecycle transitions, date consistency, and concurrency.

### Traveler

Represents a participant within an Adventure Plan. Personally identifiable,
health, accessibility, identity-document, and loyalty-program information
requires explicit data classification and must not be added casually to the
initial model.

A future reusable Traveler Profile is distinct from the plan-owned `Traveler`.
The plan stores only approved linkage, trip-specific values, and the minimum
authorized projections it owns. See
`docs/architecture/traveler-profile-and-preference-resolution.md`.

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
`docs/architecture/adventure-readiness-and-change-management.md` and
`docs/architecture/adventure-templates.md`.

Adventure Templates are versioned blueprints that instantiate new independent,
private, customer Creator-owned plans. They are not live plans, bookings, live
inventory, or continuing authorization for the template owner. Published
template versions are immutable, and later revisions may propose—but never
silently apply—changes to an existing plan.

Planning also provides an authorized spatial projection with progressive detail
across the whole Adventure, transportation or journey segments, destination
visits, itinerary days, selected places, and candidate points of interest.
Accepted plan records remain visually and structurally distinct from AI,
professional, template, or research suggestions. Maps never become the source
of truth or implicitly publish private Planning state. See
`docs/architecture/adventure-map-experience.md`.

Creators may also ingest a cruise or other itinerary from protected images,
documents, or pasted text. OCR and interpretation produce confidence-scored,
source-linked Journey Stop proposals for places, dates, arrival/departure times,
and IANA time zones. Only Creator-approved proposals mutate private Planning
records; a separate publication operation is required to create public Content
Engine `JourneyStop` records. See
`docs/architecture/itinerary-ingestion.md`.

Group Travel adds Adventure-scoped traveler participation, contextual
discussion, structured polls, planner decisions, announcements, and
acknowledgments. Participation does not grant Creator membership. Votes and
messages remain advisory inputs; only an authorized, validated Planning
operation changes the plan. See
`docs/architecture/group-travel-collaboration.md`.

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
