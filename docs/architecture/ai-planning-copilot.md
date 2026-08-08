# AdventuresSuite AI Planning Copilot

**Version:** 1.0

**Status:** Approved Direction

**Last Updated:** August 7, 2026

## Purpose

The AI Planning Copilot helps a Creator turn travel intent into a safer, more
complete Adventure Plan. It assists with structure, sequencing, research,
conflict detection, and unresolved decisions while preserving human control.

This capability specializes the broader AI Engine vision defined in
`ai-travel-advisor.md`.

## Core Principle

> AI proposes; the Creator decides; the Planning Engine commits.

## Authority Boundary

The AI Copilot is not the system of record. It must not directly create,
update, delete, confirm, publish, purchase, or book authoritative records.

The safe flow is:

```text
Authorized Creator request
    ↓
Approved Creator-scoped context
    ↓
AI produces a structured proposal
    ↓
Platform validates proposal shape and permissions
    ↓
Creator reviews a change preview
    ↓
Creator accepts or rejects operations
    ↓
Planning Engine applies approved operations transactionally
```

AI output is untrusted input even when it conforms to a schema.

## Initial Use Cases

The first production use cases are deliberately narrow:

1. Propose a day-by-day itinerary from dates, destinations, travelers, and
   preferences.
2. Identify schedule gaps, overlaps, unrealistic transfers, and unresolved
   dependencies.
3. Suggest planning tasks and questions that still require a human decision.

Later capabilities may include packing, photography planning, research,
reservation summarization, journal prompts, and travel-companion assistance.

## Proposal Model

An AI proposal should be a durable, Creator-owned record containing:

- stable proposal identity
- `CreatorId` and `AdventurePlanId`
- requesting user identity
- proposal purpose and status
- plan version used as input
- structured operations
- human-readable rationale
- source references when research was used
- model/provider execution metadata suitable for operations
- creation, review, acceptance, rejection, and expiry timestamps
- validation results and failure information

Candidate operations include:

- add or update a destination visit
- add, update, move, or remove an itinerary item
- suggest transportation or accommodation research
- add a planning task
- flag a timing conflict
- flag missing information
- suggest a packing item

The operation vocabulary is owned by AdventuresSuite. It must not mirror a
provider's tool-call or response format.

## Review and Commit Rules

- Each operation is previewable before approval.
- A Creator may accept or reject operations individually.
- Rejected proposals do not alter the plan.
- Acceptance verifies the current plan version to prevent stale overwrites.
- Conflicting proposals require regeneration or explicit reconciliation.
- Applied operations use normal Planning Engine validation and authorization.
- Proposal and approval history is retained for auditability.
- A model explanation never overrides a domain invariant.

## Provider Independence

Core contracts use platform concepts such as planning request, proposal,
operation, citation, and usage record. Model names, SDK request objects, tool
formats, and provider-specific response types remain inside adapters.

The proposal boundary is intentionally reusable. AI, travel professionals,
customers, and future collaborators may author proposals, while source identity
and authorization remain distinct. Only the Planning Engine applies an
authorized, approved proposal to authoritative plan state.

The platform should support replacing or combining AI providers without
changing the Planning domain. Provider selection is configuration, not Creator
content.

Prompts are implementation assets, not authoritative business rules. Important
rules must exist in code and documentation outside prompts.

## Context and Tenant Isolation

Every AI operation receives explicit `CreatorId`, `AdventurePlanId`, authorized
user identity, and a bounded context assembled by the platform. The model never
chooses the tenant or broadens its own access.

Context retrieval must enforce:

- Creator membership and permissions
- minimum necessary data
- resource rights
- private/public classifications
- logging and retention policy
- protection against cross-Creator cache or vector-index leakage

Sensitive traveler and reservation fields are excluded by default. Adding them
requires a documented need, classification, consent, retention policy, and
provider-data-handling review.

## Structured Output and Safety

- Require machine-validated structured output for proposed mutations.
- Reject unknown operation types and identifiers.
- Validate dates, local times, IANA time zones, dependencies, and ownership.
- Apply deterministic size and operation-count limits.
- Treat retrieved documents and web content as untrusted data, not
  instructions.
- Defend tools and prompts against injection and unauthorized data access.
- Never expose secrets, confirmation numbers, private documents, or another
  Creator's information in model context or diagnostics.
- Provide cancellation, timeout, retry, and rate-limit handling.
- Do not automatically retry a non-idempotent commit operation.

## Grounded Research

Research is a separate, attributable capability. Each research claim or source
record should retain:

- source URL and title
- publisher or provider when available
- retrieval timestamp
- applicable destination and travel dates
- claim or summary supported by the source
- freshness or expiry guidance
- verification status
- reviewing user when approved

Time-sensitive information such as opening hours, entry requirements, prices,
weather, schedules, and advisories must display source and freshness. The
platform must not present stale research as an authoritative current fact.

AI may recommend research; it may not claim that a reservation, purchase,
visa, insurance policy, or provider confirmation exists without authoritative
evidence.

## Observability and Evaluation

Record enough operational metadata to diagnose quality, cost, latency, and
failures without storing unnecessary private prompt content.

Evaluation should cover:

- schema validity
- tenant isolation
- domain-rule compliance
- unsupported claims
- citation correctness
- schedule feasibility
- stale-proposal behavior
- approval enforcement
- prompt-injection resistance
- latency and cost budgets

Deterministic tests use a fake or recorded provider. Live-model evaluations are
separate from the required build and unit-test path.

## Initial Non-Goals

- autonomous reservations, purchases, cancellations, or itinerary commits
- unrestricted web browsing
- an open-ended agent with broad platform tools
- replacing Creator judgment
- publishing AI output without review
- retaining all prompts indefinitely
- using conversation history as the authoritative plan
- personalized recommendations based on data without explicit authorization

## Definition of Done for the First AI Slice

- A provider-neutral contract exists.
- The fake provider supports deterministic end-to-end tests.
- Output uses the platform proposal schema.
- Invalid or unauthorized operations are rejected.
- A proposal cannot modify a plan before approval.
- Stale-plan concurrency is detected.
- Accepted operations use Planning Engine transactions and validation.
- Rejections and partial approvals are auditable.
- The three initial use cases work for the Spain/trans-Atlantic scenario.
