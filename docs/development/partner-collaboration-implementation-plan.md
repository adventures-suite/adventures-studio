# Partner Collaboration Implementation Plan

**Status:** Approved for Future Incremental Implementation

**Last Updated:** August 7, 2026

## Objective

Enable travel professionals to collaborate safely on customer-owned Adventure
Plans while preserving Creator ownership, customer control, provider
independence, and existing Planning Engine priorities.

This plan does not interrupt current Planning Engine persistence work.

Read before implementation:

- `AGENTS.md`
- `docs/DECISIONS.md`
- `docs/architecture/partner-collaboration-engine.md`
- `docs/product/travel-professional-partnership.md`
- `docs/architecture/planning-engine.md`
- `docs/architecture/ai-planning-copilot.md`
- `docs/development/planning-engine-implementation-plan.md`
- `docs/architecture/creator-engine.md`

## Working Rules

- Creator remains the content and planning ownership boundary.
- The customer Creator controls each Planning Engagement.
- Agency membership never implies customer access.
- Every operation requires both customer Creator and Adventure Plan scope.
- Proposal-only collaboration is the default.
- Do not add partner columns to current Planning tables speculatively.
- Use forward-only migrations when implementation reaches persistence.
- Keep booking, CRM, GDS, and supplier concepts behind neutral adapters.
- Treat imported data and professional proposals as untrusted until validated.
- Complete one phase and its security gate before beginning the next.

## Phase 0: Architecture and Product Direction

Scope:

- ownership and engagement model
- professional proposal boundary
- booking and provider boundary
- product experience
- phased implementation guidance
- roadmap, decisions, platform, Planning, AI, and agent documentation

Exclusions:

- application code
- Planning schema changes
- authentication
- partner UI
- integrations
- commercial decisions

Exit criteria:

- customer and agency ownership are unambiguous
- engagement scope and revocation are explicit
- the direction does not conflict with Planning persistence
- documentation is reviewed and committed separately

## Phase 1: Identity and Membership Prerequisites

Begins only after the Planning persistence foundation is stable.

Scope:

- authenticated platform users
- Customer and Agency Creator membership
- minimum agency roles
- professional profile identity
- authorization policy vocabulary

Acceptance criteria:

- membership in one Creator grants no access to another
- professional identity is stable and auditable
- disabled users and memberships lose access predictably
- public Creator resolution remains independent

## Phase 2: Planning Engagement Domain

Scope:

- strongly typed engagement identity
- customer and agency Creator identities
- Adventure Plan scope
- invitation, acceptance, decline, expiry, completion, and revocation
- participant and delegated-permission models
- consent and audit metadata
- domain invariants and tests

Exclusions:

- database
- UI
- professional proposals
- booking integrations

Acceptance criteria:

- an engagement cannot be unscoped
- customer and agency Creator identities must differ unless an explicitly
  documented internal collaboration mode is introduced
- only the controlling customer can grant or revoke access
- invalid state transitions fail predictably
- expiry and revocation semantics are deterministic

## Phase 3: Engagement Persistence and Authorization

Scope:

- forward-only engagement migrations
- repository contracts and Dapper adapter
- customer- and agency-scoped indexes
- authorization service
- audit persistence
- optimistic concurrency
- integration and negative security tests

Acceptance criteria:

- all access is constrained to engagement, customer Creator, and plan
- agency queries return only active authorized engagements
- cross-customer and cross-agency attempts fail
- revocation takes effect immediately
- audit history survives engagement completion or revocation
- runtime and migration identities retain least privilege

## Phase 4: Invitation and Customer-Control Experience

Scope:

- professional invitation creation
- customer acceptance or decline
- active-access view
- permission review and modification
- expiry and revocation
- accessible notifications

Acceptance criteria:

- no private data is exposed before acceptance
- invitation links are bounded, expiring, and single-purpose
- the customer can see and revoke every active professional relationship
- authorization is enforced below the UI layer

## Phase 5: Professional Proposal Collaboration

Scope:

- common proposal source model
- travel-professional proposal creation
- customer review, partial acceptance, and rejection
- stale-plan detection
- professional rationale and source attribution
- transaction and audit integration

Acceptance criteria:

- proposals cannot mutate a plan before approval
- professional and AI proposals use common platform operation semantics
- a professional cannot propose an operation outside delegated scope
- accepted operations use normal Planning Engine validation
- revocation blocks pending proposal application unless the customer adopts it
  independently

## Phase 6: Partner Workspace and Co-Branding

Scope:

- agency engagement dashboard
- bounded customer Adventure view
- proposal queue and messages
- agency Resource Engine branding
- customer-approved professional attribution
- Companion and preview attribution

Acceptance criteria:

- customer identity remains primary
- agency resources never cross Creator boundaries
- one agency cannot enumerate another agency's engagements
- co-branding is removable without changing Adventure ownership
- workspace meets accessibility and responsive-design expectations

## Phase 7: Provider-Neutral Imports

Scope:

- itinerary, reservation-summary, document, and customer-provider contracts
- external-reference and synchronization metadata
- protected Resource Engine storage
- import preview and approval
- deterministic fake provider and contract tests

Acceptance criteria:

- provider SDK types do not enter core contracts
- imported data is Creator- and engagement-scoped
- external authority and verification state remain visible
- duplicate and stale imports are handled idempotently
- connectors cannot broaden their own access
- no import claims a booking or payment without authoritative evidence

## Phase 8: Travel and Preserve Integration

Scope:

- approved engagement data in Adventures Companion
- change communication
- journal and photography prompts
- explicit preserve-and-publish selection
- post-engagement customer continuity

Acceptance criteria:

- private professional information does not become public automatically
- customers retain their Adventure after access ends
- agency attribution follows customer configuration
- public publication uses the Content Engine boundary

## Deferred Business Decisions

- partner pricing and subscription tiers
- referrals, commissions, and revenue allocation
- agency onboarding qualification
- support responsibilities
- merchant-of-record or seller-of-travel implications
- partner agreements and data-processing terms
- customer marketing permissions

These decisions may affect later product configuration but must not redefine
Creator ownership or weaken authorization boundaries.

