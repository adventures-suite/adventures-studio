# AdventuresSuite Partner Collaboration Engine

**Version:** 1.0

**Status:** Approved Direction

**Last Updated:** August 7, 2026

## Purpose

The Partner Collaboration Engine enables travel professionals to use
AdventuresSuite as a value-added planning, travel, preservation, and publishing
experience for their customers.

AdventuresSuite is not intended to replace travel agencies, professional
advice, supplier relationships, booking systems, or customer service. It gives
travel professionals and their customers a secure place to collaborate on the
complete Adventure lifecycle.

## Core Principle

> The customer owns the Adventure. The travel professional improves it.
> AdventuresSuite makes the complete experience possible.

## Terms

**Travel professional** is the inclusive platform term for an authorized travel
advisor, travel agent, agency employee, independent contractor, trip designer,
or another approved professional collaborator.

**Agency Creator** is a Creator that owns an agency's AdventuresSuite brand,
staff membership, reusable templates, professional resources, and partner
configuration.

**Customer Creator** is the Creator that owns the customer's private Adventure
Plan, resources, stories, publications, and audience relationships. A family or
individual may be represented by a Customer Creator.

**Planning Engagement** is the explicit, customer-controlled relationship that
grants one or more travel professionals bounded access to one Adventure Plan.

## Creator Remains the Ownership Boundary

Partner collaboration does not introduce a parallel tenant or content owner.
An agency may be represented by a Creator for information it owns. The customer
Creator continues to own its Adventure Plan and resulting content.

```text
Agency Creator
    owns agency brand, staff, templates, and professional resources
        ↓ participates through
Planning Engagement
        ↓ grants bounded access to
Customer Creator
    owns Adventure Plan, memories, resources, and publications
```

A Planning Engagement is a platform authorization relationship rather than
shared ownership. It carries both stable Creator identities and the specific
`AdventurePlanId`. The customer Creator is the controlling and revoking party.
No query may treat the relationship as permission to enumerate unrelated
customer records.

## Ownership Rules

- The customer Creator owns the Adventure Plan.
- The customer Creator owns journals, photographs, stories, and publications
  derived from the Adventure unless a separate legal agreement says otherwise.
- The agency Creator owns its brand, templates, internal methods, reusable
  recommendations, and professional resources.
- TechTock, LLC owns and operates AdventuresSuite.
- A travel professional receives only permissions explicitly granted through
  an accepted, active engagement.
- Ending an engagement revokes future access without erasing required audit
  history.
- One engagement never grants access to another Adventure Plan.
- One agency cannot see another agency's customers or engagements.
- A customer may work with different professionals on different Adventures.
- Engagement status never changes public publication state.

Agency-owned Adventure Templates provide a separate route for sharing reusable
expertise. Instantiating one creates an independent customer-owned plan and
records versioned provenance and license evidence. Template ownership or
attribution grants no customer-plan access; ongoing help still requires an
accepted, active, plan-scoped Planning Engagement. See
`docs/architecture/adventure-templates.md`.

## Planning Engagement

A future `PlanningEngagement` should include concepts equivalent to:

- stable engagement identity
- controlling customer `CreatorId`
- participating agency `CreatorId`
- `AdventurePlanId`
- inviting and accepting user identities
- engagement status
- start, expiry, revocation, and audit timestamps
- delegated permission set
- participating professional identities
- customer consent and policy version
- optional customer-visible agency attribution configuration

Candidate states include:

```text
Invited → Active → Completed
             ↓
          Revoked

Invited → Declined
Invited or Active → Expired
```

State transitions and permissions must be explicit, audited, and checked at
the application and persistence boundaries.

## Authorization Model

Agency membership alone does not grant access to customer data. Authorization
requires all of the following:

1. An authenticated user.
2. Active membership in the participating agency Creator.
3. An accepted and active Planning Engagement.
4. Permission for the requested operation.
5. A matching customer `CreatorId` and `AdventurePlanId`.

Suggested permission capabilities include:

- view approved planning fields
- submit a proposal
- comment or message within the engagement
- add or update customer-approved planning resources
- view reservation summaries when explicitly permitted
- direct-edit selected planning areas when explicitly granted
- prepare a customer-visible itinerary preview

Proposal-only access should be the default. Direct edit is a stronger,
separately granted permission and must still pass Planning Engine validation,
concurrency, and audit rules.

## Professional Proposal Boundary

Travel-professional recommendations should reuse the same platform-owned
proposal and approval concepts used by the AI Planning Copilot.

```text
Travel professional recommendation
        ↓
Structured planning proposal
        ↓
Customer review
        ↓
Accept, reject, or partially accept
        ↓
Planning Engine commits approved operations
```

The proposal records its source as a travel professional rather than AI. The
operation vocabulary, stale-version checks, validation, approval, transaction,
and audit behavior remain consistent.

AI, travel professionals, customers, and future collaborators may all produce
proposals, but none bypasses the Planning Engine:

```text
AI proposal
Professional proposal
Customer or family proposal
        ↓
Common proposal review boundary
        ↓
Planning Engine
```

## Booking and Reservation Boundary

AdventuresSuite does not initially book travel or become the authoritative
booking, inventory, commission, ticketing, or payment system.

Travel professionals continue to use their existing agency, CRM, consortium,
GDS, cruise, airline, hotel, tour, and supplier systems. AdventuresSuite may
store a customer-facing reservation summary and external reference when needed
for the Adventure experience.

An imported or advisor-entered reservation summary does not prove that a
booking, payment, cancellation, refund, ticket, insurance policy, or supplier
confirmation exists. The authoritative external source and verification state
must remain visible.

The approved long-term direction is a booking-companion model: AdventuresSuite
supports planning, supplier or professional handoff, consent-based confirmation
import, and the complete experience around externally fulfilled travel. Direct
selling, ticketing, payment, and merchant-of-record responsibility remain
explicitly deferred. See `docs/architecture/travel-booking-companion.md`.

## Provider-Neutral Integrations

Future integrations should use provider-neutral capabilities such as:

```text
IItineraryImportProvider
IReservationImportProvider
ITravelDocumentProvider
IPartnerCustomerProvider
```

Core contracts must not be named after a host agency, consortium, GDS, CRM,
booking vendor, or supplier.

Imported records should retain:

- provider identity
- external reference
- owning customer Creator
- applicable engagement
- import and last-synchronization timestamps
- verification and synchronization state
- source authority
- data classification
- customer or professional approval when required

Imports are untrusted until validated. A connector never selects its own
Creator scope or engagement permissions.

## Co-Branding and Attribution

The customer remains the primary Adventure identity. A customer-visible
experience may acknowledge the professional relationship through approved
patterns such as:

```text
Customer Adventure
Planned with [Travel Professional or Agency]
Powered by AdventuresSuite
```

Co-branding is configuration, not ownership. It must distinguish:

- customer Creator identity
- travel-professional attribution
- agency Creator brand
- AdventuresSuite platform attribution
- The Simonton Adventures flagship brand

The agency cannot replace the customer's identity or imply ownership of the
customer's story. Brand assets resolve through the owning Creator and Resource
Engine boundaries.

## Privacy, Consent, and Audit

Partner collaboration requires:

- explicit invitation and acceptance
- least-privilege, engagement-scoped authorization
- visible access scope and expiry
- customer-controlled revocation
- consent evidence and policy version
- complete access and mutation audit history
- protected-resource delivery
- data minimization and field classification
- secure support and operational diagnostics
- retention rules after completion or revocation

Confirmation references, traveler details, costs, private notes, documents,
and precise itinerary information must not enter public pages, logs, analytics,
AI prompts, or unrelated agency views.

## Engine Relationships

- Creator Engine owns customer and agency Creator identities.
- Identity and Authorization own user authentication, membership, and policy
  evaluation.
- Partner Collaboration Engine owns engagements, delegated permissions, and
  professional participation.
- Planning Engine owns authoritative Adventure Plans and applies approved
  operations.
- Resource Engine owns protected documents and media.
- AI Engine may assist either party within the same authorization boundary.
- Content Engine owns explicitly published editorial material.
- Notification Engine may deliver engagement messages and approved public
  events under separate policies.
- External booking and agency systems remain authoritative through adapters.

## Initial Non-Goals

- replacing travel agents or agency CRMs
- becoming a GDS or supplier inventory system
- autonomous booking, cancellation, payment, or refund
- commission calculation or settlement
- merchant-of-record responsibility for travel
- broad agency access to a customer Creator
- public sharing of a live private itinerary
- embedding partner fields prematurely in the Phase 2 Planning schema
- deciding partner pricing or business terms

## Implementation Timing

This direction must influence identity, authorization, proposal, and audit
design, but it does not interrupt current Planning persistence work. The
Partner Collaboration Engine begins only after Planning persistence and core
identity foundations are stable.

The current Planning schema should remain focused on customer-owned planning
records. Engagement tables and permissions arrive through later forward-only
migrations.

## Definition of Done for the Foundation

- Customer Creator ownership remains unambiguous.
- Agency Creator ownership remains isolated.
- An engagement grants access to only one intended Adventure Plan.
- Invitation, acceptance, expiry, completion, and revocation are auditable.
- Proposal-only collaboration works without direct plan mutation.
- Direct edits require explicit stronger permission.
- Cross-customer and cross-agency negative tests pass.
- Booking systems remain authoritative outside AdventuresSuite.
- Co-branding preserves customer identity and resource ownership.
- Revocation blocks access immediately while retaining required audit history.
