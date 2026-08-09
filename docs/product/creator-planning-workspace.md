# Creator Planning Workspace

**Version:** 1.0

**Status:** Product Direction

**Last Updated:** August 9, 2026

## Product Outcome

The Creator Planning Workspace is the first private, authenticated product area
of AdventuresSuite. It helps a Creator plan an Adventure manually and then use
AI as a reviewable copilot.

The first milestone is **Plan an Adventure with AI**.

## Reference Scenario

The initial proving ground is The Simonton Adventures' 2027 Spain and
trans-Atlantic Adventure. It exercises multiple destinations, cruise and land
segments, changing time zones, sea days, accommodations, activities, and
transportation without inventing a synthetic product scenario.

## Primary User

An authorized member acting for a Creator who needs to organize a real future
Adventure. The initial experience may support one primary planner while the
domain and authorization model preserve a path to multiple members.

## User Journey

1. Sign in and enter an authorized Creator Workspace.
2. Create or open a private Adventure Plan.
3. Enter working title, dates, travelers, preferences, and constraints.
4. Add and sequence destination visits.
5. Build itinerary days and add activities, travel, and accommodations.
6. Record reservation state, notes, tasks, packing, and budget items.
7. Ask AI for a bounded proposal.
8. Review a clear before/after change preview.
9. Accept or reject individual suggestions.
10. Continue editing the authoritative plan manually.
11. Preview a private summary of the Adventure.
12. Generate a versioned private Adventure Travel Playbook.
13. Optionally export selected itinerary items to the user's calendar.
14. Review the Adventure countdown, readiness, changes, and required actions.
15. Explicitly choose information for a future publication workflow.

## Initial Workspace Areas

### Adventure Dashboard

- list accessible private plans
- show planning status, date range, and unresolved work
- show an accessible countdown for every Planned, Upcoming, or otherwise
  approved committed Adventure
- show explainable readiness categories and material changes requiring action
- create, open, archive, and resume a plan according to permissions

### Adventure Overview

- title and working description
- dates and status
- travelers and high-level preferences
- planning completeness and important warnings

### Destinations and Route

- ordered destination visits
- planned local arrival and departure dates
- IANA time zone
- visit-specific notes
- route and transfer gaps

### Daily Itinerary

- local day and destination context
- activities and flexible time blocks
- transportation and accommodations
- reservation linkage
- conflict and missing-information indicators

### Planning Lists

- tasks
- packing items
- notes
- budget items
- proposed, reserved, confirmed, changed, and cancelled items

### AI Proposal Review

- purpose and summary
- source plan version
- individual proposed operations
- rationale and sources where applicable
- before/after preview
- accept, reject, or defer per operation
- visible validation and conflict results

### Private Preview

- readable Adventure summary
- planned route and daily outline
- clear private/draft labeling
- no permanent public address or subscriber event

### Travel Playbook

- deterministic private preview generated from one plan version
- trip overview, route, daily itinerary, transportation, accommodations,
  reservations, readiness guidance, and selected protected documents
- explicit Creator Master, Traveler, Print, Companion Offline, Shareable, and
  Memory profiles
- PDF first, with DOCX and mobile packaging added incrementally
- visible generated-at, plan-version, stale, privacy, and intended-audience state
- no invented confirmation, schedule, price, or meeting information

The acceptance reference is a cohesive travel package comparable in usefulness
to `ITALY_MASTER.docx`, produced from structured Planning and Resource data
without manual assembly. See
`docs/architecture/adventure-travel-playbook.md`.

### Calendar

- privacy-safe ICS export for one item, day, or selected Adventure itinerary
- tentative state for planned but unconfirmed items
- stable updates and cancellations when confirmed details change
- exact local time and destination time zone
- explicit traveler consent before device or provider calendar writes
- secure deep links instead of ticket codes, PINs, private notes, or permanent
  protected-document URLs

Initial ICS export requires no provider account. Connected Microsoft or Google
calendar synchronization is a later provider-adapter capability. See
`docs/architecture/adventure-calendar-integration.md`.

### Readiness and Change Management

- readiness dashboard with source-backed warnings and clear next actions
- change-impact preview across itinerary, calendar, Playbook, Companion,
  reminders, tasks, budgets, and documents
- protected Travel Document Inbox with reviewed extraction and provenance
- traveler-specific views and information policies
- distinct delivered, viewed, acknowledged, accepted, and completed states
- Today and Next, contingencies, offline places, and smart reminders
- planning decisions, comments, proposals, and professional handoff
- multi-currency budgets, deadlines, and cancellation-window tracking
- safe plan templates and cloning

See `docs/architecture/adventure-readiness-and-change-management.md`.

## Experience Principles

- Manual planning must work without AI.
- AI is visible as assistance, never hidden automation.
- The authoritative plan is always distinguishable from a proposal.
- Privacy state is obvious on every workspace screen.
- Dates are shown in relevant local context.
- Destructive and publication actions require deliberate confirmation.
- Complex travel plans should remain understandable without reading raw JSON.
- The workspace should reduce planning anxiety rather than add administrative
  burden.

## Privacy and Safety

The initial workspace should avoid collecting highly sensitive information
unless necessary. Full passport numbers, payment-card data, health records, and
unredacted booking documents are outside the first release.

Reservation summaries and confirmation references are private. Logs,
telemetry, AI prompts, screenshots, public previews, and support diagnostics
must not expose them.

## Publication Boundary

The private preview is not publication. The future publishing flow must show
exactly which plan information becomes public Content Engine material.

Private-by-default exclusions include:

- traveler-private details
- confirmation references
- costs and budgets
- internal tasks and notes
- unpublished operational changes
- protected resources

Public publishing emits explicit domain events only after a successful,
Creator-approved publication transaction.

## Initial AI Experiences

The first release provides only:

- proposed day-by-day itinerary
- schedule gap and conflict review
- unresolved planning-task suggestions

Research, packing, photography planning, reservation extraction, companion
chat, and publication assistance follow after the proposal boundary is proven.

## Accessibility and Responsive Design

The workspace must meet WCAG 2.2 AA expectations. It must support keyboard
operation, clear focus states, semantic forms, understandable validation,
screen-reader announcement of proposal changes, and usable mobile layouts.

Large itinerary editing may optimize for desktop first, but essential viewing,
tasks, and proposal decisions must remain usable on a phone.

## Success Measures

- A Creator can model the reference Adventure without editing JSON.
- Manual plan creation is understandable without AI assistance.
- AI proposals save time without obscuring what will change.
- No proposal changes a plan without approval.
- Users can identify incomplete, conflicting, and unconfirmed work.
- Private information never appears in a public route.
- The plan can later support Companion and publication experiences without
  re-entry.
- A Creator can generate a coherent private Travel Playbook from one known plan
  version without copying itinerary details into a separate document.
- A consenting traveler can add selected itinerary items to a calendar without
  exposing protected travel credentials or creating duplicate events.
- Planned and committed Adventures show a correct, accessible countdown in the
  Workspace and Companion without inventing unknown times.
- Users can tell what is ready, what changed, who is affected, and what action
  is required without relying on a black-box score.

## Explicitly Deferred

### Future Professional Collaboration

A customer may invite a travel professional to collaborate on one Adventure
Plan through a time-bounded, revocable engagement. The customer remains the
owner, professional recommendations normally arrive as proposals, and
co-branding may recognize the agency without displacing the customer identity.
Agency membership alone never reveals customer data.

AdventuresSuite will not replace agency CRM, GDS, supplier booking, commission,
or fulfillment systems. Those systems may integrate later through
provider-neutral boundaries.

- full Creator content editor
- autonomous booking
- payment and commerce
- subscriber notifications
- native mobile applications
- real-time multi-user collaboration
- offline synchronization
- full document ingestion
- public sharing of live private itineraries
