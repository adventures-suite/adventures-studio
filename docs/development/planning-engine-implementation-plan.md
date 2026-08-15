# Planning Engine Implementation Plan

**Status:** Approved for Incremental Implementation

**Last Updated:** August 9, 2026

## Objective

Deliver the **Plan an Adventure with AI** vertical slice through small,
independently reviewable and deployable phases. Preserve the existing public
website and Creator isolation while private planning capability is introduced.

Read before implementation:

- `AGENTS.md`
- `docs/DECISIONS.md`
- `docs/architecture/planning-engine.md`
- `docs/architecture/ai-planning-copilot.md`
- `docs/product/creator-planning-workspace.md`
- `docs/architecture/creator-engine.md`
- `docs/architecture/content-engine.md`
- `docs/architecture/resource-engine.md`
- `docs/architecture/adventure-lifecycle.md`
- `docs/architecture/adventure-readiness-and-change-management.md`
- `docs/architecture/partner-collaboration-engine.md`
- `docs/architecture/traveler-profile-and-preference-resolution.md`

## Working Rules

- Complete one phase and its exit gate before beginning the next.
- Keep the application buildable and deployable after every phase.
- Preserve existing public routes and JSON editorial content.
- Require explicit `CreatorId` in planning, persistence, AI, and background
  contracts.
- Treat planning records as private by default.
- Keep domain types independent of Razor, EF Core, AI SDKs, and providers.
- Do not add infrastructure before the owning phase.
- Add isolation, authorization, validation, and concurrency tests before
  exposing a feature.
- Record decisions when implementation differs from this plan.
- Do not combine a phase with unrelated presentation or content work.
- Do not add speculative partner fields or tables during the current Planning
  persistence work.
- Preserve stable identities, provenance, dependencies, traveler visibility,
  verification, freshness, acknowledgment, consent, and action-required hooks
  where the owning phase needs them; do not implement the entire readiness
  capability speculatively.

## Phase 0: Architecture and Documentation

Scope:

- Planning Engine architecture
- AI proposal and authority boundary
- Creator Planning Workspace product definition
- implementation phases and acceptance gates
- roadmap, lifecycle, platform architecture, decisions, and agent guidance

Exclusions:

- application code
- packages
- database
- authentication
- AI integration

Exit criteria:

- terminology is consistent across documents
- planning and public content ownership are distinct
- Creator, privacy, approval, and publication boundaries are explicit
- unresolved choices are recorded rather than silently assumed
- documentation diff is reviewed and committed separately

## Phase 1: Planning Domain Model

Scope:

- strongly typed planning identities
- `AdventurePlan` aggregate
- travelers and preferences at the minimum safe level
- destination visits
- itinerary days and planned activities
- transportation and accommodations
- reservations, notes, tasks, budget items, and packing items
- planning status and domain validation
- date, local-time, IANA time-zone, audit, and version semantics

Exclusions:

- EF Core or database packages
- repositories
- UI and routes
- authentication
- AI
- public publishing

Acceptance criteria:

- every aggregate is Creator-owned
- invalid default identities are rejected
- child identity and aggregate invariants are tested
- reversed or inconsistent date ranges fail predictably
- local date/time values are not silently converted to UTC
- UTC audit timestamps remain distinct from travel dates
- initial travelers do not imply a reusable profile, guardian authority,
  protected booking identity, loyalty storage, or supplier disclosure
- no type depends on JSON, Razor, EF Core, or an AI provider
- the Spain/trans-Atlantic scenario can be represented in tests

Exit gate:

- domain review approved
- Release build, tests, formatting, and diff validation pass

## Phase 2: Persistence Architecture

Scope:

- select and document the database provider
- repository and unit-of-work/application transaction contracts
- EF Core implementation and migrations if approved
- Creator-scoped indexes and constraints
- optimistic concurrency
- UTC audit behavior
- archival or recoverable-deletion policy
- database integration-test infrastructure

Exclusions:

- workspace UI
- authentication screens
- AI
- publishing transformation

Acceptance criteria:

- all reads and writes require Creator identity
- same child identifiers may exist across Creators when permitted
- cross-Creator access and mutation tests fail safely
- stale updates are detected
- transactions preserve aggregate invariants
- migrations can create a clean database and upgrade the previous schema
- domain contracts remain provider-independent

Exit gate:

- persistence and security review approved
- migration, integration, and regression tests pass

## Phase 3: Identity and Authorization

Begin with the architecture and threat-model slice defined in
`docs/development/identity-authorization-implementation-plan.md`. Do not begin
with login screens.

Implementation sequence:

1. architecture decision and threat model;
2. framework-independent identity and authorization contracts;
3. policy and permission vocabulary;
4. authorization test matrix;
5. identity-provider selection;
6. authentication integration;
7. Creator membership persistence;
8. server-side enforcement;
9. UI integration and security testing.

Scope:

- platform user identity
- Creator membership
- minimum initial roles and permissions
- authorization policies
- protected workspace routes
- authenticated audit actor
- future Planning Engagement permission vocabulary without implementing the
  Partner Collaboration Engine

Exclusions:

- broad organization administration
- billing
- social identity features beyond the chosen initial sign-in
- AI

Acceptance criteria:

- anonymous users cannot access planning records
- members cannot access another Creator's records
- authorization is enforced below the UI layer
- role changes have deterministic effects
- logs and errors do not reveal protected data
- public host resolution continues to work independently

Exit gate:

- threat-model and authorization review approved
- negative security tests pass

## Phase 4: Minimal Creator Planning Workspace

Current incremental delivery includes an authorized itinerary board with append
workflows for destinations, days, activities, transportation, accommodations,
and credential-free reservation summaries. Existing planned activities can be
corrected in place without changing their day, identity, or status; broader
item movement, lifecycle transitions, and removal remain later focused slices.

Scope:

- Adventure dashboard
- create and edit an Adventure Plan
- travelers and preferences
- destinations and route sequence
- daily itinerary
- activities, transportation, accommodations, and reservations
- notes, tasks, packing, and budget items
- private read-only preview
- authorized layered Adventure map with overview, segment, destination, day,
  selected-place, and candidate-point-of-interest views
- deterministic private Adventure Travel Playbook preview and first PDF export
- privacy-safe ICS calendar export for selected itinerary items

Exclusions:

- AI
- public publishing
- real-time collaboration
- offline sync
- full Creator content editor

Acceptance criteria:

- the reference Adventure can be planned without JSON or AI
- validation is accessible and understandable
- concurrency conflicts do not silently overwrite work
- private values do not appear in public endpoints or HTML
- critical flows work on desktop and mobile layouts
- map state distinguishes authoritative plan items from candidate suggestions,
  preserves Creator isolation, and has an accessible non-map alternative
- generated output records source plan/template/profile versions and becomes
  visibly stale when an authoritative input changes
- repeated ICS generation preserves stable UIDs and correct destination-local
  time zones without exposing protected values

Exit gate:

- product, accessibility, security, and regression review approved
- Playbook rendering, profile allowlist, calendar duplicate/update/cancellation,
  map privacy/accessibility/provider-failure, and prohibited-data tests pass

## Phase 4A: Adventure Template Foundation

Scope:

- framework-independent template, immutable version, parameter, provenance,
  license-reference, and instantiation contracts
- deterministic creation of a new private customer Creator-owned plan
- privacy and prohibited-data validation
- Creator-scoped SQL persistence and Dapper adapters after contract approval
- private Creator template creation and plan instantiation services

Acceptance criteria:

- a template cannot be confused with an Adventure Plan or booking
- published versions are immutable
- instantiation creates new plan-owned identities and records exact provenance
- template revisions cannot silently mutate existing plans
- traveler, reservation, payment, private-note, protected-Resource, and precise
  location data are rejected from reusable templates
- template ownership, license, and attribution grant no access to the new plan
- authorization, entitlement, audit, and authoritative SQL isolation tests pass

Exit gate:

- architecture, privacy, security, partner-boundary, and regression review
  approved
- `docs/architecture/adventure-templates.md` definition of done is satisfied

## Phase 5: AI Proposal Foundation

Scope:

- provider-neutral AI planning contracts
- durable proposal and operation records
- bounded context assembly
- structured output validation
- fake provider for deterministic tests
- proposal preview
- per-operation accept and reject
- transactional application of approved operations
- stale-plan detection and audit history

Exclusions:

- unrestricted research
- autonomous tools
- booking
- direct AI mutation
- public publishing

Acceptance criteria:

- AI cannot write authoritative plan data directly
- malformed, unknown, cross-Creator, or unauthorized operations are rejected
- stale proposals cannot overwrite a newer plan
- partial acceptance is deterministic and auditable
- the fake provider covers the full workflow without network access
- provider SDK types do not enter domain or application contracts

Exit gate:

- AI safety, privacy, cost, and architecture review approved

## Phase 6: First AI Planning Use Cases

Scope:

1. proposed day-by-day itinerary
2. schedule gap and conflict review
3. unresolved planning-task suggestions

Acceptance criteria:

- each result is structured and reviewable
- suggestions explain relevant constraints
- invalid timing and time-zone assumptions are surfaced
- no result claims a booking or confirmation without evidence
- evaluation scenarios include rejection, partial acceptance, stale plans,
  incomplete input, and provider failure

Exit gate:

- reference scenario meets agreed quality, latency, and cost thresholds

## Phase 6A: Reviewed Itinerary Ingestion

Scope:

- dormant provider-neutral published-cruise search, sailing, freshness, and
  unavailable-provider contracts; no live adapter until commercial approval
- protected itinerary image/PDF upload and pasted-text input
- framework-independent source-evidence, extracted-field, confidence, and
  `JourneyStopProposal` contracts
- OCR/document interpretation, place resolution, and IANA time-zone adapters
- side-by-side review, correction, duplicate/conflict detection, and stale-plan
  handling
- transactional application of accepted proposals to private Planning records

Acceptance criteria:

- supported cruise fixtures capture ordered places, local dates, arrival and
  departure times, sea/overnight status, and proposed IANA time zones
- every extracted value retains source evidence, confidence, and explicit or
  inferred state
- missing or ambiguous values remain unresolved instead of being invented
- OCR, AI, and provider results cannot mutate Planning before Creator approval
- applying approved changes is Creator-scoped, concurrency-safe, atomic, and
  audited
- public Content Engine `JourneyStop` records are never created by ingestion
- malicious-document, prompt-injection, duplicate, cross-Creator, provider-
  failure, accessibility, retention, and prohibited-data tests pass

Exit gate:

- Resource, Planning, AI, security, privacy, accessibility, provider, and
  regression review approved
- `docs/architecture/itinerary-ingestion.md` definition of done is satisfied
- a live published-cruise adapter additionally satisfies the commercial
  activation gate in `docs/architecture/published-cruise-itinerary-import.md`

## Phase 6B: Group Travel Collaboration Foundation

Scope:

- Adventure-scoped traveler invitations and participation independent from
  Creator membership
- subgroup and traveler information-policy contracts
- contextual threads attached to Planning subjects
- structured polls, eligible participants, response privacy, deadlines, and
  explicit planner decisions
- announcements, safe notification projections, and acknowledgments
- moderation, retention, export, audit, and Companion API boundaries

Exclusions:

- general-purpose direct messaging
- contacts, social graphs, presence, voice, or video
- messages or votes that mutate Planning directly
- real-time transport as a prerequisite for the first release

Acceptance criteria:

- every operation is Creator-, Adventure-, participant-, and subject-scoped
- participation never grants Creator membership or professional engagement
- revocation and subgroup visibility changes fail closed immediately
- poll eligibility, privacy, deadline, concurrency, and result integrity tests
  pass
- an authorized planner decision and normal Planning validation are required
  before adopting a result
- sensitive discussion and voting data do not leak through notifications,
  telemetry, AI, exports, attachments, or public routes
- moderation, retention, deletion, accessibility, audit, and cross-Creator
  negative tests pass

Exit gate:

- identity, authorization, Planning, privacy, security, moderation,
  accessibility, notification, audit, and product review approved
- `docs/architecture/group-travel-collaboration.md` definition of done is
  satisfied

## Phase 7: Grounded Research

Scope:

- approved research-provider boundary
- source, claim, citation, retrieval, freshness, and verification records
- time-sensitive result labeling
- research review workflow
- prompt-injection and untrusted-content defenses

Exclusions:

- unrestricted autonomous browsing
- treating research as a reservation or legal guarantee

Acceptance criteria:

- current claims retain provenance and retrieval time
- stale information is visible
- unsupported claims are not promoted to authoritative plan facts
- cross-Creator research indexes and caches are isolated

Exit gate:

- source-quality, safety, privacy, and evaluation review approved

## Phase 8: Adventures Companion

Detailed mobile product, architecture, GPS breadcrumb, privacy, offline, and
delivery gates are defined in `docs/architecture/adventures-companion.md`,
`docs/product/adventures-companion.md`, and
`docs/development/adventures-companion-implementation-plan.md`.

Scope:

- today view
- local-time context
- itinerary and reservation summaries
- tasks and reminders
- maps and essential references
- journaling and photography prompts
- offline-readiness design
- minimized offline Travel Playbook access
- explicit Add to Device Calendar through a platform adapter
- optional traveler-controlled GPS breadcrumbs

Acceptance criteria:

- Companion reads approved Planning Engine state
- sensitive details remain protected
- time-zone transitions are tested
- public and private presentation boundaries remain distinct
- calendar permission denial preserves a useful Companion experience, and no
  traveler can write to another traveler's calendar
- location capture is off by default, explicit, visible, pausable, stoppable,
  retention-bound, and private until separately approved for publication

## Phase 9: Preserve and Publish

Scope:

- explicit selection of planning facts for publication
- protected-field exclusion
- transformation into Content Engine commands or records
- publication preview and approval
- public domain event after successful publication

Acceptance criteria:

- private planning records are never published wholesale
- selected output preserves stable public addresses
- subscriber notification intent is explicit and transactional
- publication can fail without partially exposing data

## Verification for Every Code Phase

Run:

```text
dotnet restore TheSimontonAdventures.slnx
dotnet build TheSimontonAdventures.slnx --configuration Release --no-restore
dotnet test TheSimontonAdventures.slnx --configuration Release --no-build --no-restore
dotnet format TheSimontonAdventures.slnx --verify-no-changes --no-restore
git diff --check
```

Also verify the relevant Creator-isolation, authorization, migration,
concurrency, public-route, startup-validation, and Azure smoke tests.

## Unresolved Decisions

These are decided in their owning phases:

- relational database provider and local-development topology
- initial identity provider
- initial Creator roles and permission vocabulary
- sensitive traveler-data classification and retention
- proposal retention and model-interaction logging
- AI provider and model selection
- research providers and acceptable-source policy
- offline Companion architecture
- exact Playbook template/versioning and generated-artifact retention policy
- connected calendar-provider selection, token custody, and reconciliation
- exact private-to-public publication contract
- travel-professional engagement invitation, permission, expiration, and
  revocation mechanics

None of these decisions should be embedded accidentally in Phase 1 domain
types.
