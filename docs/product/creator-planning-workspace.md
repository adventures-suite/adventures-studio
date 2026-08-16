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
4. Add and sequence destination visits manually or review Journey Stops
   extracted from uploaded itinerary text or images.
5. Build itinerary days and add activities, travel, and accommodations.
6. Explore the Adventure, route segments, destinations, days, and candidate
   points of interest on a layered map.
7. Invite travelers into an Adventure-scoped group experience.
8. Discuss proposals, collect structured votes, and record planner decisions.
9. Record reservation state, notes, tasks, packing, and budget items.
10. Ask AI for a bounded proposal.
11. Review a clear before/after change preview.
12. Accept or reject individual suggestions.
13. Continue editing the authoritative plan manually.
14. Preview a private summary of the Adventure.
15. Generate a versioned private Adventure Travel Playbook.
16. Optionally export selected itinerary items to the user's calendar.
17. Review the Adventure countdown, readiness, changes, and required actions.
18. Explicitly choose information for a future publication workflow.

## Initial Workspace Areas

### Adventure Dashboard

- list accessible private plans
- show planning status, date range, and unresolved work
- show an accessible countdown for every Planned, Upcoming, or otherwise
  approved committed Adventure
- show explainable readiness categories and material changes requiring action
- create, open, archive, and resume a plan according to permissions

### Workspace Shell and Navigation

- place primary Planner tools in a persistent left-side workspace pane
- support expanded, compact icon-rail, user-resized, auto-hidden, and explicitly
  hidden states
- restore hidden navigation through a persistent accessible control
- open auto-hidden navigation for keyboard focus as well as pointer interaction
- use a mobile overlay drawer rather than permanently compressing the planning
  canvas
- render a cohesive AdventuresSuite SVG icon family with the tool name normally
  beneath each icon in the expanded state
- persist user navigation and theme preferences without treating client state as
  authorization
- provide light, dark, and system themes; dark mode is required for workspace
  launch
- build both themes from semantic surface, text, focus, and planning-state tokens
  that meet WCAG 2.2 AA expectations

The navigation is an authorized and entitlement-aware tool projection, not a
static role menu. Tool visibility and usability may depend on user relationship,
Creator permission, resource context, stable Platform Capability, remaining
allowance, time-bounded grant, feature rollout, and service availability.
Subscription-locked tools may be discoverable with a restrained explanation;
tools hidden by authorization or irrelevant resource context normally remain
undisclosed. Every underlying operation enforces the same applicable gates below
the UI.

See `docs/product/workspace-experience-and-value.md` and
`docs/architecture/platform-billing-entitlements.md`. Shared component,
accessibility, build-versus-buy, and audience-specific experience direction is
defined in `docs/architecture/experience-design-system.md`.

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
- correction of an existing activity's title and optional local times without
  changing its identity, day, or planning status
- correction of an existing transportation segment's route, local schedule,
  and IANA time zones without changing its identity or planning status
- correction of an existing accommodation's name, inclusive start and end
  dates, and IANA time zone without changing its identity or planning status
- read-first contextual Add and Edit disclosures with only one Planner board
  action expanded at a time
- transportation and accommodations
- reservation linkage
- conflict and missing-information indicators

### Adventure Map

- whole-Adventure overview with ordered destinations and major travel legs
- progressive drill-down by journey segment, destination visit, itinerary day,
  and place
- filters for planned, proposed, reserved, confirmed, cancelled, and candidate
  items
- selected and possible points of interest with source, freshness, confidence,
  and planning status
- visible route, timing, missing-coordinate, and stale-data warnings
- accessible textual itinerary and list alternative to the visual map
- no implication that inferred routes or candidate places are booked, open,
  safe, accessible, or navigation-ready

See `docs/architecture/adventure-map-experience.md`.

### Itinerary Import

- optionally find a published sailing by cruise line, ship, and departure date
  after an approved commercial data provider is available
- upload a protected cruise-itinerary image or PDF, or paste itinerary text
- review ordered Journey Stop proposals beside the source
- capture place, local date, arrival and departure time, and proposed IANA time
  zone without inventing missing values
- see field-level confidence, source evidence, ambiguity, duplicates, conflicts,
  and explicit versus inferred values
- correct, accept, or reject stops before any private Planning mutation
- require a separate publication workflow before any stop becomes public

See `docs/architecture/itinerary-ingestion.md` and
`docs/architecture/published-cruise-itinerary-import.md`.

### Group Travel Collaboration

- invite authenticated travelers without granting Creator membership
- organize optional households, rooms, cabins, vehicles, or activity subgroups
- discuss an Adventure, proposal, destination, day, activity, poll, or change in
  its own contextual thread
- run structured polls for dates, destinations, activities, lodging,
  transportation, budget ranges, and other planning choices
- support preference, ranking, approval, availability, interest, and abstention
  without forcing travelers to disclose private reasons
- close a poll and record an explicit planner decision before any Planning
  mutation
- issue safe announcements with distinct delivery, viewing, acknowledgment,
  acceptance, and completion states
- avoid general direct messaging, contacts, social graphs, presence, voice, and
  video features

See `docs/architecture/group-travel-collaboration.md`.

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
- Workspace navigation should adapt to the current user's authorized role,
  relationship, subscription capabilities, effective period, and Adventure
  context without becoming a confusing feature catalog.
- Light and dark themes should feel deliberately designed rather than treated as
  independent or inverted products.

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

Research, packing, photography planning, reviewed itinerary and reservation
extraction, companion chat, and publication assistance follow after the
proposal boundary is proven.

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
