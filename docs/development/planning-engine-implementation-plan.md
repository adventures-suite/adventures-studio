# Planning Engine Implementation Plan

**Status:** Approved for Incremental Implementation

**Last Updated:** August 7, 2026

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
- `docs/architecture/partner-collaboration-engine.md`

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

Scope:

- platform user identity
- Creator membership
- minimum initial roles and permissions
- authorization policies
- protected workspace routes
- authenticated audit actor

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

Scope:

- Adventure dashboard
- create and edit an Adventure Plan
- travelers and preferences
- destinations and route sequence
- daily itinerary
- activities, transportation, accommodations, and reservations
- notes, tasks, packing, and budget items
- private read-only preview

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

Exit gate:

- product, accessibility, security, and regression review approved

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

Scope:

- today view
- local-time context
- itinerary and reservation summaries
- tasks and reminders
- maps and essential references
- journaling and photography prompts
- offline-readiness design

Acceptance criteria:

- Companion reads approved Planning Engine state
- sensitive details remain protected
- time-zone transitions are tested
- public and private presentation boundaries remain distinct

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
- exact private-to-public publication contract
- travel-professional engagement invitation, permission, expiration, and
  revocation mechanics

None of these decisions should be embedded accidentally in Phase 1 domain
types.
