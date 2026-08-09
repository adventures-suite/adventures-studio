# Adventure Readiness and Change Management

**Status:** Approved Direction

**Last Updated:** August 9, 2026

## Purpose

AdventuresSuite must help planners and travelers understand when an Adventure
begins, whether it is ready, what changed, what requires action, and what
happens next. This capability turns authoritative Planning data and selected
protected Resources into calm, role-appropriate operational guidance without
becoming a second source of truth.

Approved capabilities include:

- Adventure countdowns;
- a Travel Readiness Dashboard;
- change-impact analysis;
- a protected Travel Document Inbox;
- traveler-specific views and information policies;
- acknowledgments and action-required workflows;
- Today and Next views;
- contingency plans;
- offline maps and place collections;
- smart reminders;
- planning decisions and rationale;
- comments, proposals, and travel-professional handoff;
- multi-currency budgets;
- deadlines and cancellation-window tracking; and
- reusable plan templates and cloning.

## Ownership Boundaries

- The Planning Engine owns authoritative itinerary, status, dependencies,
  tasks, decisions, deadlines, contingency choices, and traveler assignments.
- The Resource Engine owns uploaded documents, extracted-document provenance,
  protected media, and generated artifacts.
- Travel Readiness and Change Management owns rebuildable projections,
  evaluations, acknowledgments, delivery state, and action queues.
- AdventuresCompanion owns mobile presentation, encrypted offline projections,
  and device adapters; it does not become authoritative.
- The Notification Engine delivers approved messages from durable events. A
  notification is not evidence that a traveler saw or accepted a change.
- AI may extract, classify, compare, explain, or propose. AI output remains
  untrusted until validated and, where required, approved.

## Adventure Countdown

Every Adventure in `Planned`, `Upcoming`, or another explicitly approved
committed state displays a countdown in the Planning Workspace and
AdventuresCompanion. The countdown is a projection of authoritative schedule
data, not a persisted decrementing counter.

Countdown rules:

- Derive the target from the authoritative start date and, when known, local
  start time plus IANA time-zone identifier.
- When only a date is known, show a day-level countdown such as `42 days`; do
  not invent a midnight departure instant.
- When an authoritative time and zone are known, the UI may progressively show
  days, hours, and minutes according to the approved experience.
- Show the target date and destination time zone in an accessible details view
  so the number is never ambiguous.
- Recalculate from a trusted current time and stored target. Do not write ticks
  to SQL, produce per-second audit events, or poll the server for every update.
- Offline Companion uses the last authorized schedule projection and visibly
  marks it stale when its synchronization policy is exceeded.
- Schedule changes update the countdown through normal versioned plan
  synchronization and change-impact processing.
- At the start boundary, show `Starts today` or `In progress`; after completion,
  show completion or memory state rather than a negative countdown.
- Archived, cancelled, and idea-stage Adventures do not show an active
  countdown unless a future product policy explicitly defines one.
- Presentation supports screen readers, localization, reduced motion, clock
  changes, daylight-saving transitions, and international date line travel.

The countdown does not change lifecycle state, confirm a booking, send a
notification, or grant access.

## Travel Readiness Dashboard

The dashboard provides an explainable view rather than one opaque score. It
groups status by itinerary completeness, conflicts, reservations,
transportation, accommodation, documents, tasks, packing, payments, deadlines,
calendar freshness, offline-package freshness, traveler acknowledgments, and
unresolved changes or contingencies.

Every warning identifies its source, affected travelers or items, severity,
last evaluation time, and a safe next action. `Unknown` is distinct from
`Ready`. A document or calendar event does not prove a reservation is confirmed.

## Change Impact Engine

When authoritative information changes, AdventuresSuite determines downstream
consequences using explicit provider-neutral relationships rather than free
text. A flight change may affect transfers, check-in tasks, calendar events,
reminders, Playbooks, and Companion offline state. A hotel or traveler change
may affect maps, documents, instructions, and access policies.

Impact results are versioned and explainable. Projection refreshes are
idempotent. Material changes require acknowledgment when policy says traveler
awareness is necessary. Provider or device failures remain visible and
reconcileable; they do not corrupt authoritative Planning state.

## Travel Document Inbox

Authorized users may upload or forward confirmations, tickets, vouchers,
insurance material, schedules, and accommodation instructions into a protected
inbox.

- Store originals as protected Resources outside public `wwwroot`.
- Record source, uploader, received time, checksum, classification, processing
  status, retention, malware-scan result, and affected plan items.
- Treat OCR, barcode recognition, and AI extraction as untrusted proposals.
- Show extracted values with provenance and confidence for human review.
- Never let a document silently overwrite Planning state or prove payment,
  identity, or confirmation by itself.
- Detect duplicates and superseded documents without destroying evidence.
- Apply least-privilege delivery, short-lived access, redaction, and traveler
  information policies.

## Traveler-Specific Views and Information Policies

Every traveler-facing projection applies an explicit information policy rather
than serializing an entire plan. Policies may account for traveler assignment,
guardian relationships, operational need-to-know, private notes, surprise
activities, financial visibility, confirmation details, protected documents,
professional access, and output profile.

Authorization remains the first gate. An information policy can reduce an
authorized view but cannot grant access. Policies are versioned, testable,
auditable, and fail closed when required facts are missing.

## Acknowledgments and Action Required

AdventuresSuite distinguishes delivery, viewing, acknowledgment, acceptance,
and completion. Each is a separate state with actor, source version, and
timestamp. Acknowledgment never substitutes for consent, contract acceptance,
payment, or completion.

Reminders are bounded, deduplicated, quiet-hour aware, localized, and
escalation-limited. Notification content is minimal and uses a secure deep link
to retrieve current authorized detail.

## Active-Travel Views and Contingencies

`Today and Next` presents the smallest useful traveler-specific view of the
current day, next transition, time zone, meeting point, transport,
accommodation, tasks, and approved contingency guidance. It remains useful
offline and distinguishes stale or unverified information.

A contingency plan records a reviewed alternative and its trigger; AI or an
external provider cannot silently activate it. Activation is an authorized,
audited Planning operation that produces ordinary change impacts.

Offline maps and place collections contain selected, licensed, minimized data.
They avoid permanent protected URLs and do not imply continuous tracking.

## Collaboration and Financial Operations

- Planning decisions retain the question, considered options, outcome,
  rationale, actor, and source plan version.
- Comments and professional recommendations remain distinct from authoritative
  changes and use the proposal/approval boundary.
- Travel-professional handoff packages are scoped, revocable, co-brandable,
  and never transfer plan ownership.
- Multi-currency budgets preserve original amount and ISO currency, exchange-
  rate source and timestamp, and a selected reporting currency. Estimated and
  settled amounts remain distinct.
- Deadlines and cancellation windows retain authoritative local/UTC semantics,
  source, and confidence. Reminders are not legal guarantees.
- Templates and cloning copy approved structure, not traveler identities,
  credentials, confirmations, protected documents, audit history, or stale
  provider mappings.

## Cross-Cutting Requirements

All capabilities preserve:

- source provenance, verification status, confidence, and freshness;
- stable item and projection identifiers and explicit dependencies;
- Creator and traveler scope at every boundary;
- consent, revocation, retention, deletion, and legal-hold rules;
- secure deep links instead of embedded secrets;
- transactional outbox delivery for required cross-boundary work;
- idempotency, reconciliation, retry limits, and stale-state detection;
- accessibility, localization, currency, date, time, and IANA time-zone rules;
- durable audit for protected actions without copying private content; and
- provider-neutral contracts for calendar, maps, documents, messaging, and
  financial data.

## Delivery Direction

These are approved platform requirements, not authorization to implement them
inside Slice 5F or as one large subsystem. Delivery remains incremental:

1. Preserve required domain hooks while building the Planning Workspace.
2. Add countdown and explainable readiness projections.
3. Add action-required and acknowledgment contracts.
4. Add change-impact relationships and durable projection refresh.
5. Add the protected Document Inbox and reviewed extraction.
6. Add traveler-specific information policies.
7. Add active-travel and Companion offline experiences.
8. Add provider integrations only behind approved adapters and security gates.
