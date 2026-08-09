# AdventuresSuite Roadmap

**Version:** 1.4

**Status:** Living Document

**Last Updated:** August 9, 2026

---

# Purpose

This roadmap defines the long-term evolution of AdventuresSuite.

It is intended to be a living document.

Priorities may change.

Ideas may evolve.

The vision remains constant.

Every completed milestone moves AdventuresSuite closer to becoming the premier platform for preserving, publishing, and sharing meaningful adventures.

---

# Product Philosophy

AdventuresSuite should evolve in small, high-quality iterations.

We prefer:

- beautiful experiences
- thoughtful design
- reusable architecture
- maintainable software

over rushing features.

Every release should noticeably improve the customer experience.

---

# Current Strategic Initiative

## Plan an Adventure with AI

AdventuresSuite will now expand beyond public web presentation into private,
structured planning. The first reference scenario is The Simonton Adventures'
2027 Spain and trans-Atlantic Adventure.

The implementation proceeds through gated increments:

1. Planning and AI architecture
2. Planning domain model
3. Database-backed, Creator-scoped persistence
4. Identity and authorization
5. Minimal Creator Planning Workspace
6. Adventure Travel Playbook and universal ICS calendar output
7. AI proposal and approval foundation
8. Proposed itinerary, conflict review, and planning-task assistance
9. Grounded travel research
10. Adventures Companion, offline Playbook, and device-calendar integration
11. Travel readiness, countdowns, change impact, and document intake
12. Traveler-specific active-travel guidance and acknowledgments
13. Explicit preserve-and-publish transformation

The governing principle is:

> AI proposes; the Creator decides; the Planning Engine commits.

AdventuresCompanion is the first approved iOS and Android application. It will
use .NET MAUI Blazor Hybrid and begin with the active traveler experience:
offline itinerary access, local-time context, essential references, memory and
media capture, notifications, and optional traveler-controlled GPS breadcrumbs.
Location capture is off by default and private until a separate Creator-reviewed
publication step. See `docs/architecture/adventures-companion.md`.

The Adventure Travel Playbook is a first-class private Planning output. It
generates a traveler-ready, versioned guide comparable in usefulness to the
`ITALY_MASTER.docx` reference, while keeping Planning authoritative and
protected tickets and vouchers in the Resource Engine. Adventure Calendar
Integration begins with privacy-safe ICS output and later adds explicit
traveler-controlled device and provider synchronization. See:

- `docs/architecture/adventure-travel-playbook.md`
- `docs/architecture/adventure-calendar-integration.md`

## Travel Readiness and Change Management Requirement

AdventuresSuite will make upcoming travel operationally understandable through
countdowns, an explainable readiness dashboard, change-impact analysis, a
protected Travel Document Inbox, traveler-specific information policies,
acknowledgments, required actions, Today and Next, contingencies, offline places,
smart reminders, decision history, professional handoff, multi-currency
budgets, deadline tracking, and safe templates.

Every Planned, Upcoming, or otherwise approved committed Adventure will show a
countdown in the Planning Workspace and AdventuresCompanion. It is derived from
authoritative date/time-zone data, remains day-level when no time is known, and
never becomes a second lifecycle clock or a stream of persisted ticks.

These capabilities are approved requirements but will be delivered through
bounded increments after the current authentication gate. See
`docs/architecture/adventure-readiness-and-change-management.md`.

## Travel Professional Partnership Direction

AdventuresSuite will partner with travel professionals rather than replace
them. An agency may use a Creator boundary for its brand, staff, templates, and
Resources, while the customer Creator continues to own each Adventure Plan and
everything derived from it.

A future plan-scoped `PlanningEngagement` will authorize specific professionals
to collaborate with least privilege, expiration, revocation, and audit history.
Professional recommendations default to the shared proposal and approval
boundary: professional proposal, customer review, Planning Engine commit.

This direction does not interrupt the current Planning persistence phase and
does not authorize speculative partner schema. See:

- `docs/architecture/partner-collaboration-engine.md`
- `docs/product/travel-professional-partnership.md`
- `docs/development/partner-collaboration-implementation-plan.md`

Planning data is private by default. It does not become public merely because
an Adventure is Planned, Upcoming, or Current. Public content requires an
explicit Creator-approved publication operation.

Detailed scope and phase gates are defined in:

- `docs/architecture/planning-engine.md`
- `docs/architecture/ai-planning-copilot.md`
- `docs/product/creator-planning-workspace.md`
- `docs/development/planning-engine-implementation-plan.md`

## Platform Audit and Reporting Requirement

Audit and reporting are mandatory foundations across the roadmap, not a later
dashboard feature. Every Engine must identify its protected actions, durable
events, reporting projections, prohibited data, retention, and access rules as
it is implemented.

Delivery proceeds incrementally: shared taxonomy and contracts; append-oriented
audit persistence and transactional guarantees; identity and authorization
evidence; versioned business events and outbox delivery; Creator-scoped
projections; compliance, financial, AI, and platform reports; and a separate
analytical platform only when measured needs justify it.

This requirement does not broaden Authentication Slice 5A. That slice may add
only the minimum provider-neutral audit vocabulary and classifications it needs.
See `docs/architecture/audit-reporting.md` and
`docs/development/audit-reporting-implementation-plan.md`.

## Platform Billing and Entitlements Requirement

AdventuresSuite will support versioned membership levels, add-ons, seats, and
usage allowances through a provider-neutral Platform Billing and Entitlements
capability. Plans bundle stable platform capabilities; they are not user roles.

Feature access will compose user authorization, Creator entitlement, rollout,
and service availability. Platform Billing remains separate from Creator
Commerce, audience subscriptions, and Creator memberships. Current development
remains unmetered until the relevant product, legal, tax, accounting, support,
security, and implementation gates are approved.

See `docs/architecture/platform-billing-entitlements.md`,
`docs/product/pricing-model.md`, and
`docs/development/platform-billing-entitlements-implementation-plan.md`.

---

# Version 1.0

## Public Launch

### Core Platform

- [x] Adventure architecture
- [x] JSON content engine
- [x] Multi-volume support
- [x] Adventure lifecycle
- [x] Homepage
- [x] Adventure landing pages
- [x] Destination engine
- [x] Journey timelines
- [x] Featured destinations
- [x] Reflections
- [x] Continue the Journey
- [x] Planning adventures
- [x] AdventuresSuite branding

### Remaining

- [ ] Photography lightbox
- [ ] Full mobile optimization
- [ ] Footer
- [ ] Contact page
- [ ] About page
- [ ] Search
- [ ] Performance optimization
- [ ] SEO
- [ ] Accessibility review
- [ ] Analytics

---

# Version 1.1

## Publishing Experience

Adventure Publisher expands.

Goals

- Generate PDF books
- Generate EPUB
- Print-ready exports
- Better gallery layouts
- Chapter navigation
- Automatic table of contents
- QR code integration
- Continue the Journey improvements

---

# Version 1.2

## Photography Experience

Adventure Photos grows.

Goals

- Full-screen lightbox
- Slideshow
- Keyboard navigation
- Mobile gestures
- Better galleries
- Panorama support
- Before / after comparisons
- Photo metadata
- AI caption suggestions

---

# Version 1.5

## Discovery

Adventure Search launches.

Goals

- Full-text search
- Semantic search
- Search journals
- Search destinations
- Search resources
- Search books
- AI-assisted search

---

# Version 2.0

## AdventuresSuite Creator Workspace

The editor becomes the product.

Goals

- Browser-based editor
- Adventure dashboard
- Destination editor
- Journey editor
- Photography manager
- Reflection editor
- Publishing center
- Live preview

No more JSON editing.

---

# Version 2.5

## Subscription and Notification Engine

Creators build permission-based relationships with subscribers who choose to
follow their public work.

Foundation

- Publication domain events
- Creator-scoped Subscriber and Subscription records
- Verified consent and one-click unsubscribe
- Notification preferences and policies
- Transactional outbox and durable asynchronous delivery
- Delivery history, retry, suppression, and deduplication

Initial experience

- Subscribe to a Creator by email
- Creator-controlled publish-and-notify or silent publication
- New Adventure and meaningful publication notifications
- Subscriber preference management
- Creator-scoped aggregate audience insights

Next

- Follow a specific Adventure
- Lifecycle notifications from planning through publication
- Digest delivery
- In-application and mobile notifications

Implementation depends on identity, database-backed publication, security, and
consent foundations. The architecture is defined before Creator Workspace so
publication workflows emit the correct durable events from the beginning.

---

# Version 2.6

## Adventure Advisor

AI becomes deeply integrated.

Capabilities

Travel planning

Story assistance

Writing

Photography

Publishing

Adventure Builder

Packing

Research

Recommendations

The AI becomes a travel companion.

---

# Version 3.0

## Organizations

Support:

Families

Churches

Schools

Study Abroad

Travel Companies

Mission Organizations

Multiple users

Permissions

Shared Adventures

Collaboration

---

# Version 3.5

## Marketplace

The Commerce Engine begins as focused Creator storefronts before expanding into
a broader marketplace.

Commerce foundation

- Publication and Edition contracts
- Creator-scoped Product catalogs and Offers
- Merchant-of-record decision
- Orders, refunds, and commercial audit history
- Customer Entitlements and protected digital delivery
- Provider-neutral payment and fulfillment adapters

Initial products

- EPUB and PDF editions
- Printed books
- Curated photography prints
- Creator-branded storefront presentation

Photography expansion

- Resource rights and release records
- Print-readiness validation and approved derivatives
- Professional-laboratory fulfillment pilot
- Neutral or Creator-branded packaging where partners support it
- Physical samples and quality approval
- Standardized photography licensing after legal review

Potential later additions

Themes

Adventure templates

Photography presets

Premium destination guides

Professional layouts

Partner integrations

Travel planning packages

The initial photography fulfillment workflow may be manual while quality,
packaging, support, margin, and demand are validated. Automated integration must
use a provider-neutral boundary rather than binding AdventuresSuite directly to
Bay Photo or another laboratory.

---

# Version 4.0

## Mobile

Native applications.

iPhone

iPad

Android

Offline journals

Offline photography

Offline maps

Automatic synchronization

---

# Version 5.0

## Adventure Ecosystem

AdventuresSuite becomes the operating system for adventure storytelling.

Products

AdventuresSuite Creator Workspace

Adventure Advisor

Adventure Publisher

Adventure Web

Adventure Search

Adventure Maps

Future products

Adventure Photos

Adventure Teams

Adventure Insights

Adventure Marketplace

---

# Long-Term Vision

AdventuresSuite becomes the standard platform for preserving meaningful adventures.

Families use it for vacations.

Authors use it for books.

Photographers use it for portfolios.

Organizations use it for missions.

Schools use it for study abroad.

Travel companies use it for customer experiences.

The platform grows far beyond travel while remaining true to its purpose.

---

# Success

AdventuresSuite succeeds when people say:

"I never want to lose another adventure."

Not because of the software.

Because the software helped preserve something meaningful.

---

# Development Philosophy

Every feature must satisfy three questions.

1. Does it help tell a better story?

2. Does it make preserving memories easier?

3. Could every customer benefit from it?

If the answer is no...

It should probably not be built.

---

# Motto

Create.

Preserve.

Publish.

Share.

Remember.
