# Travel Professional Partnership Experience

**Version:** 1.0

**Status:** Product Direction

**Last Updated:** August 7, 2026

## Product Outcome

AdventuresSuite helps travel professionals deliver a differentiated customer
experience before, during, and after travel. It complements their expertise and
existing booking systems rather than competing with them.

Customers receive everything AdventuresSuite offers: private planning,
professional collaboration, AI assistance, travel Companion experiences,
memory preservation, public storytelling, and future books and publications.

Commercial terms, partner tiers, commissions, and pricing are intentionally
deferred. This document defines the product and technology experience.

AdventuresSuite may support booking handoffs and confirmation import while the
approved supplier or professional remains the seller, merchant, and servicing
authority. See `docs/architecture/travel-booking-companion.md`.

## Value for the Customer

- one private Adventure Plan shared on the customer's terms
- a professionally prepared and reviewable itinerary
- clear proposals and change history
- customer-facing reservation and transportation summaries
- destination context, maps, tasks, and packing support
- Adventures Companion during travel
- photography and journaling prompts
- preservation of what actually happened
- public Adventure experiences when intentionally published
- future PDF, EPUB, printed book, and QR-enhanced outputs

## Value for the Travel Professional

- a premium digital experience to accompany professional service
- structured collaboration instead of fragmented email and documents
- reusable agency-owned templates and resources
- clearer customer approvals and unresolved decisions
- customer-visible itinerary and Companion experiences
- fewer ambiguous changes through versioned proposals
- professional attribution and approved co-branding
- a lasting post-trip relationship through preservation and publishing

### Agency Adventure Templates

Travel professionals may publish pre-planned, agency-branded Adventure
Templates that customers adapt for dates, duration, destinations, pace,
accessibility, traveler preferences, and budget. This lets agencies productize
expertise and begin customer conversations from a strong plan.

Using a template creates a new private plan owned by the customer Creator. The
agency retains template intellectual property, attribution, and license
evidence, but receives no implicit access to that plan. The customer may
separately request professional help through a plan-scoped engagement.

AdventuresSuite presents templates and collaboration; it does not claim live
availability, guarantee prices, sell travel, or displace the professional's
booking and fulfillment role.

## Primary Experiences

### Agency Onboarding

- establish an agency Creator and brand
- invite staff through Creator membership
- establish professional profiles and permissions
- configure customer-visible attribution
- accept platform privacy and collaboration policies

### Customer Invitation

- select or identify one customer Creator
- select one Adventure Plan
- send a bounded engagement invitation
- show the customer exactly who is requesting access and why
- require explicit acceptance before any private data is visible

### Engagement Workspace

- shared Adventure overview limited to delegated fields
- proposal and approval queue
- customer-visible professional messages
- itinerary, activity, transportation, accommodation, and reservation-summary
  collaboration
- unresolved questions and tasks
- visible access scope, participants, expiry, and audit history

### Professional Proposals

- propose structured changes against a known plan version
- explain rationale and relevant source authority
- show before/after effects
- allow customer acceptance, rejection, or partial acceptance
- detect stale proposals
- preserve professional and customer audit history

### Customer Control

- inspect active professional access
- change allowed permissions
- revoke access
- choose co-branding and attribution
- retain the Adventure after the engagement ends
- decide what, if anything, becomes public

### Travel and Post-Trip Experience

- provide approved plan information to Adventures Companion
- preserve authoritative booking-source attribution
- capture journals, photographs, and reflections
- transform selected information into public stories or publications
- continue the customer relationship without locking the Adventure to the
  agency

## Proposal-First Experience

The default professional role submits proposals rather than directly editing
the customer plan. This makes professional expertise visible while preserving
customer control.

Direct editing may be offered later when a customer grants an explicit,
narrower capability. Direct editing never bypasses Planning Engine validation,
optimistic concurrency, or audit history.

## Branding

The customer Adventure remains primary. Agency presentation is secondary and
customer-approved.

Example:

```text
The Smith Family Adventure
Planned with Northstar Travel
Powered by AdventuresSuite
```

The interface must not imply that the agency owns the customer's content,
photographs, audience, or publication rights.

## Trust and Transparency

Customers must be able to answer:

- Which professional can see this Adventure?
- Which information can they see or change?
- When does access expire?
- What did they propose or modify?
- Which system is authoritative for a reservation?
- How do I revoke access?
- Who owns my Adventure and resulting stories?

These answers must be visible in the product rather than buried in support
documentation.

## Success Measures

- A professional can collaborate without learning raw JSON.
- A customer can understand and control delegated access.
- No professional accesses an unrelated customer or Adventure.
- Proposals reduce ambiguity and preserve approval history.
- Existing agency systems remain useful and authoritative.
- Customers retain their Adventure after an engagement concludes.
- Professional attribution adds value without displacing customer identity.
- The same plan supports Companion and preservation without duplicate entry.

## Explicitly Deferred

- partner pricing and commercial contracts
- travel commissions
- supplier inventory
- booking and ticket issuance
- payment and refund processing
- agency CRM replacement
- broad customer marketing access
- automated customer enrollment without consent
- native mobile partner applications
