# Creator Planning Workspace

**Version:** 1.0

**Status:** Product Direction

**Last Updated:** August 7, 2026

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
12. Explicitly choose information for a future publication workflow.

## Initial Workspace Areas

### Adventure Dashboard

- list accessible private plans
- show planning status, date range, and unresolved work
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

## Explicitly Deferred

- full Creator content editor
- autonomous booking
- payment and commerce
- subscriber notifications
- native mobile applications
- real-time multi-user collaboration
- offline synchronization
- full document ingestion
- public sharing of live private itineraries
