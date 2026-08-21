# Adventure Templates

Status: Approved platform requirement; durable instantiation foundation implemented for review
Last updated: August 19, 2026

## Purpose

Adventure Templates, presented to customers as Journey FootSteps, let Creators
begin with a professionally designed, pre-planned Adventure and adapt it to real
travelers, dates, time zones, budgets, preferences, and constraints.

Templates must strengthen AdventuresSuite's partnership with travel
professionals. They are not a mechanism for AdventuresSuite to replace travel
agents, sell travel, guarantee prices or availability, or become the merchant
of record.

## Core Boundary

A template is a versioned planning blueprint. It is not:

- an active or customer-owned `AdventurePlan`;
- a reservation, ticket, booking, quote, or availability guarantee;
- live inventory or a supplier contract;
- professional advice from AdventuresSuite; or
- authorization to view or edit the plan created from it.

Using a template creates a new, private Adventure Plan owned by the customer's
Creator. The plan receives new identities for all plan-owned records. It keeps
immutable provenance pointing to the template and exact published version used,
but it is not live-linked to the template.

Later template revisions must never silently change an existing plan. A future
"compare with a newer version" operation may create an explainable proposal;
only an authorized human may approve resulting plan mutations.

## Ownership Classes

The catalog may eventually contain:

- AdventuresSuite-curated templates;
- private Creator-owned templates;
- agency Creator-owned templates;
- templates licensed to specific Creators or subscription tiers; and
- privacy-reviewed templates derived from completed Adventures.

An agency is represented as a Creator for its own brand, staff, intellectual
property, templates, and Resources. The customer Creator owns the instantiated
Adventure Plan, traveler information, reservations, private Resources,
memories, and publications.

Template authorship, attribution, licensing, or purchase never grants the
author access to an instantiated customer plan. Continued professional
collaboration requires a separate, accepted, active, plan-scoped
`PlanningEngagement` governed by the Partner Collaboration Engine.

## Template Content

A template may define:

- a title, description, intended audience, accessibility notes, and attribution;
- relative itinerary days and recommended duration;
- destinations, route patterns, seasonal windows, and time-zone-aware guidance;
- activities, alternatives, dependencies, and recommended pacing;
- transportation and accommodation patterns rather than customer bookings;
- budget categories, estimates, ranges, currencies, assumptions, and freshness;
- readiness tasks, packing guidance, and decision points;
- Playbook and calendar projection rules; and
- optional professional handoff or assistance information.

A template must not contain:

- traveler identities or personal profiles;
- reservation confirmations, ticket codes, PINs, QR codes, or payment data;
- private notes or customer communications;
- precise GPS breadcrumbs or private location history;
- private customer media or protected documents; or
- permanent URLs to protected Resources.

Reusable media must be independently owned or licensed for template use and
represented through Resource Engine references when that capability exists.

## Parameterization

Instantiation may accept typed, validated parameters such as:

- start date, duration, origin, and included or excluded destinations;
- traveler count, preferences, accessibility needs, and pace;
- budget target, currency, and tolerance;
- lodging and transportation preferences; and
- fixed commitments that the generated plan must preserve.

Parameters and adjustment rules are data contracts, not executable extensions.
Templates may not contain arbitrary code, scripts, SQL, prompts, expressions,
or direct provider calls. Unsupported or contradictory inputs fail safely and
produce validation feedback rather than a partially trusted plan.

## Lifecycle and Versioning

The initial lifecycle is:

`Draft -> Published -> Retired or Superseded`

Published versions are immutable. Editing creates a new draft and then a new
published version. Retiring a version prevents new use unless an explicit
license or recovery rule permits it; it does not invalidate plans already
created from that version.

Every instantiation records at least:

- template identity and exact version;
- template owner and authorship/attribution facts;
- license or entitlement decision reference;
- initiating actor and UTC timestamp;
- a bounded, non-sensitive parameter summary; and
- the new customer Creator and Adventure Plan identities.

## Travel-Professional Partnership

Agency templates are a first-class partnership capability. They let a travel
professional encode expertise, demonstrate value, accelerate customer
planning, and offer ongoing help without surrendering the customer relationship
or forcing AdventuresSuite into the role of travel agency.

The broader FootSteps product may eventually let professional Creators author,
publish, license, and receive compensation for approved reusable expertise.
Journey FootSteps are the complete-plan form backed by immutable Adventure
Template versions; smaller Destination, itinerary, activity, transportation,
stay, and guidance FootSteps remain distinct catalog content with their own
review and application contracts. Commercial terms and payout mechanics are
intentionally deferred, and none grants access to an instantiated customer
plan.

Product language should favor actions such as "Use this template" and
"Request professional help." AdventuresSuite should not present itself as the
travel seller through platform-owned "Book now" behavior. Booking and
fulfillment remain with the travel professional, supplier, or a future neutral
provider adapter under an explicitly approved commercial model.

Professional proposed changes default to customer review. Direct editing, if
ever enabled, requires stronger explicit permission and complete audit history.

## AI Boundary

The AI Planning Copilot may recommend a template, explain why it fits, and
propose parameter values or adaptations. AI output remains untrusted structured
proposal data. It may not directly mutate a plan, assert live availability,
guarantee a price, create a booking, or fabricate professional attribution.

## Authorization, Entitlements, and Licensing

Authorization answers whether an actor may publish, discover, license, use, or
retire a specific template. Platform entitlements answer whether a Creator's
subscription or commercial agreement includes that capability. Licensing
records describe permitted use and attribution. These are separate decisions.

All catalog queries, cache keys, indexes, exports, and background operations
must preserve Creator and visibility boundaries. Private templates are not
discoverable across Creators. Public catalog visibility does not imply the
right to instantiate or redistribute a template.

## Audit and Reporting

Required audit coverage includes template publication, retirement,
instantiation, license decisions, attribution changes, and sensitive
administrative actions. Audit records use minimal allowlisted metadata and do
not copy template bodies, traveler inputs, private plan content, or AI prompts.

Reporting may measure aggregate discovery, adoption, completion, professional
engagement, and commercial outcomes through authorized projections. Creators
and agencies receive only reports within their permitted scope; platform-wide
reporting requires separate authority and privacy controls.

## Incremental Delivery

1. Define framework-independent template, version, parameter, provenance, and
   instantiation contracts.
2. Add validation and tests proving privacy exclusions, immutable versions,
   deterministic instantiation, and independent customer ownership.
3. Add Creator-scoped SQL persistence and Dapper adapters with forward-only
   migrations and authoritative SQL tests.
4. Add private Creator template creation and plan instantiation services.
5. Add agency-owned templates and Planning Engagement handoff without granting
   implicit customer access.
6. Add catalog discovery, licensing, entitlement, attribution, and reporting
   projections.
7. Add AI-assisted recommendation and adaptation through reviewed proposals.

No template database, marketplace, booking integration, or UI should be added
before its preceding domain, authorization, privacy, and audit boundaries are
implemented and tested.

The first durable implementation deliberately stops at that boundary. It
accepts only an already authorized immutable blueprint from a provider-neutral
resolver, creates fresh identities for the complete private plan aggregate,
and atomically persists the plan, exact source template/version provenance,
Creator-scoped idempotency result, and required audit intent. The production
catalog/licensing adapter and the user-facing “Use this Journey” workflow are
the next consumers of this boundary, not alternative authorization or
persistence paths.

The first user-facing consumer keeps discovery and use separate. An authorized
catalog query may show a complete preview, but pressing “Create my private
Journey” performs a fresh exact-version use decision and the atomic
instantiation operation. The customer selects only supported parameters; the
interface must not show controls that are silently ignored. Local Alpha uses a
fictional JSON catalog. The hosted Azure development environment may expose the
same reviewed preview catalog only through an explicit, exact development
deployment classification independent from its real authentication provider.
Production remains empty and fail-closed until its catalog, entitlement,
license, retention, and reporting adapters are approved.

## Definition of Done

Adventure Templates are production-ready only when:

- template and plan ownership cannot be confused;
- published versions are immutable and instantiation is reproducible;
- every created plan is an independent private customer-owned aggregate;
- private or customer-specific data cannot enter a reusable template;
- authorship and license evidence survive without conveying plan access;
- agency collaboration requires a separate plan-scoped engagement;
- AI and template updates cannot silently mutate plans;
- authorization, entitlement, audit, reporting, and retention tests pass; and
- product language and workflows preserve the travel-professional partnership
  boundary.
