# Planner Curated Idea Library

**Status:** Approved Product and Architecture Direction

**Last Updated:** August 18, 2026

## Purpose

AdventuresSuite should launch with a useful, trustworthy collection of travel
ideas rather than waiting for a community marketplace to develop. The curated
Idea Library supplies reusable inspiration to the Planner Ideas rail while
preserving content ownership, Creator isolation, licensing, provenance,
freshness, and the Planning review boundary.

The governing principle is:

> Prepopulate excellent reusable content, never unexplained or unlicensed plan
> data.

## Ownership Boundary

An Idea Library entry is reusable content owned by the Content Engine. It is not
an `AdventurePlan`, plan child, booking, live inventory record, AI proposal, or
Adventure Template. Planning consumes authorized idea projections and creates
new plan-owned records only after a separate reviewed Planning operation.

Every idea has an explicit owning `CreatorId`. Adventures Studio-authored launch
content uses a named Adventures Studio Creator identity; it does not use a
default, missing, or implicitly privileged Creator. Creator attribution,
platform curation, catalog visibility, and authorization remain separate facts.

Publishing an idea does not grant its owner access to a consuming Creator's
plan. Adding an idea to a plan does not transfer ownership of the source or keep
the plan live-linked to future revisions.

## Launch Library

The initial library may include:

- Adventures Studio-authored destination ideas, activities, route patterns,
  sample-day structures, and planning guidance;
- approved existing Destination, Journey, and story content deliberately
  transformed into reusable ideas;
- commissioned or licensed contributions from Creators and travel
  professionals; and
- reviewed open or provider data only when its license permits the intended
  storage, transformation, display, and reuse.

The initial target should favor a smaller collection of excellent, visually
cohesive, well-tagged ideas over broad but shallow coverage. Launch quantity is
a product decision supported by measured destination coverage and editorial
capacity, not an architectural constant.

The production library must not be populated by copying arbitrary websites,
social posts, descriptions, photographs, or itineraries. AI-assisted drafting
does not remove the need for human editorial review, source verification,
rights evidence, and appropriate attribution.

## Core Content Model

A provider-neutral idea definition and immutable published version should
support, when applicable:

- stable idea and version identities;
- owning Creator and credited author or organization;
- idea type such as destination, activity, route pattern, sample day, planning
  guidance, or Journey inspiration;
- title, concise description, editorial body reference, and optional Resource
  references;
- destination or place identities rather than name-only matching;
- applicable seasons, date windows, duration, pace, interests, transportation
  preferences, accessibility considerations, and audience guidance;
- source class, source reference, transformation lineage, and review evidence;
- attribution, copyright, license, permitted uses, territory, effective period,
  and required display terms;
- freshness class, reviewed-at date, next-review date, and safe stale behavior;
- visibility, catalog status, stable Platform Capability, and optional
  time-bounded promotional availability;
- lifecycle, version, supersession, and retirement evidence; and
- allowlisted search and matching metadata.

An idea contains reusable guidance, not customer or traveler facts. It excludes
traveler identities, reservations, confirmation references, ticket codes,
payment information, private notes, protected documents, precise breadcrumbs,
raw research captures, raw AI exchanges, and permanent protected-Resource URLs.

## Context Hierarchy

The Ideas rail is a contextual catalog, not one static menu. Its authorized
projection changes according to the currently selected Planning subject:

| Selected context | Primary idea types |
| --- | --- |
| Whole Adventure | Journeys, destination combinations, templates, and route patterns |
| Empty route position | Destinations, nearby additions, and logical next stops |
| Destination visit | Sample days, activities, neighborhoods, stays, and local transportation |
| Itinerary day | Activities, meal rhythm, pacing, nearby places, and flexible time |
| Transportation segment | Modes, transfer patterns, stops, and timing considerations |
| Accommodation | Neighborhood guidance, nearby activities, and transfer considerations |
| Planned activity | Alternatives, nearby complements, and scheduling guidance |

No selection defaults to the whole Adventure when an authorized plan is open.
The rail names its current context, provides an accessible return to the whole
Adventure, and exposes only relevant idea-type filters. A context change resets
transient filters without mutating or persisting plan state.

Plan dates, duration, existing schedule, approved traveler-preference
projections, pace, transportation preferences, accessibility requirements, and
budget may refine results only when authorized and appropriate for the source.
Every match remains explainable. Personalization never broadens source access or
turns a suggestion into an authoritative plan record.

Each idea level maps to a different future review contract. An activity may
propose one plan child; a Destination proposes a destination visit; a sample day
may propose a day and activities; and a Journey may require configurable review
of multiple destinations, dates, time zones, days, and transportation patterns.
The rail never flattens a Journey into an unsafe one-click mutation.

## Editorial Lifecycle

The initial lifecycle is:

```text
Draft -> InReview -> Published -> Retired or Superseded
```

Published versions are immutable. Corrections create a new reviewed version.
Retirement prevents new selection according to policy but does not rewrite
existing plans or erase the provenance of prior reviewed uses.

Editorial review verifies at least:

- accuracy and useful scope;
- destination identity and temporal applicability;
- claims about price, schedule, opening, safety, suitability, and accessibility;
- source quality and freshness;
- copyright, license, attribution, and Resource usage rights;
- prohibited private or sensitive data;
- Creator and professional attribution;
- language quality and AdventuresSuite trust standards; and
- whether the entry is appropriate for discovery, subscription packaging, or a
  limited promotion.

Platform-wide publication, moderation, takedown, rights correction, and legal
hold require explicit platform authority. Ordinary Creator membership does not
grant platform editorial administration.

## Visibility and Entitlements

Visibility and permission are explicit. Candidate visibility classes include:

- private to the owning Creator;
- shared with specifically licensed Creators;
- available to an approved catalog audience;
- platform-wide public catalog content; and
- withdrawn, expired, or unavailable.

Platform entitlement determines whether a Creator may discover or use a
capability or licensed collection. It does not authorize the human actor,
publish the source, or mutate a plan. Free, Explorer, Navigator, and promotional
names never appear in application authorization logic; stable Platform
Capabilities and time-bounded grants do.

The query boundary evaluates source visibility, actor authority, Creator scope,
license, entitlement, rollout, freshness, and service availability. A denied
private source is normally omitted rather than presented as a commercial lock.
An approved catalog item may be shown as an upgrade only when product policy
permits its existence to be disclosed.

## Creator, Professional, and Traveler Contributions

Creator and professional submissions may later enter a review queue with exact
authorship, ownership, license, attribution, and commercial terms. Agency
membership and idea authorship never grant customer-plan access. Continued
professional collaboration requires the separate plan-scoped engagement.

Traveler suggestions inside one Adventure remain private, Adventure-scoped
collaboration data. They do not enter the reusable library automatically.
Reusing a traveler contribution requires an explicit submission, contributor
rights and consent, privacy review, editorial transformation, and publication
decision. Completing or publishing an Adventure likewise does not silently
convert its plan into reusable content.

## Content, Resource, and Planning Integration

Library storage and retrieval evolve through the Content Engine. Consumers use
`ITravelContentService` or an approved narrower Content Engine contract; Razor
components never read JSON, SQL, provider responses, or seed manifests
directly.

Images and media use Creator-scoped Resource identities with alternative text,
attribution, copyright, usage rights, publication state, and safe delivery.
Idea content never duplicates permanent storage URLs or assumes that public
visibility authorizes a private Resource.

The Planner Ideas query produces a minimal `PlannerIdeaProjection` with explicit
Creator, plan, actor, context, paging, source, and entitlement inputs. When a
later **Add to plan** workflow is confirmed, provenance records the exact idea
and version, source Creator, applicable license or use decision, and bounded
operation inputs. The resulting plan record is independently owned by the
customer Creator and is not silently updated by the library.

## Prepopulation and Environment Separation

Production launch content is delivered through a reviewed, versioned,
idempotent content-import or publication process. It is data, not Razor markup,
application conditionals, SQL migration inserts, or a compiled destination
catalog. Repeating the same import is an exact no-op; divergent identity or
version collisions fail safely and produce reviewable diagnostics.

Seed packages record source checksums, schema version, authoring owner,
environment, release identity, and imported idea versions. Production,
development, test, and demonstration catalogs remain separate. Fictional local
Alpha ideas are clearly labeled and can never be promoted automatically into
the production library.

Rollback retires or supersedes affected published versions through the normal
lifecycle. It does not delete plan records created from an earlier valid
version or rewrite their provenance.

## Freshness and Trust

Each idea declares how quickly it can become misleading. Evergreen pacing
guidance may need infrequent review; prices, schedules, entry rules, opening
hours, seasonal access, and advisories need short review windows or live
provider verification.

Stale or unverified claims are hidden, downgraded to appropriately qualified
inspiration, or marked unavailable according to policy. An editorial review
date is not proof of live availability. Ideas never state or imply that
something is booked, purchasable, safe, accessible, open, or currently priced
without authoritative attributable evidence.

## Audit, Reporting, and Moderation

Protected audit coverage includes submission, review, publication, rejection,
retirement, restoration, takedown, rights changes, license decisions, platform
moderation, seed import, and sensitive administrative access. Audit metadata is
minimal and allowlisted; it does not copy idea bodies, private plans, raw
sources, AI prompts, or protected media URLs.

Reporting uses authorized, rebuildable projections. Candidate measures include
catalog coverage, freshness, discovery, preview, reviewed use, retirement,
rights exceptions, and aggregate outcomes. Creator reports remain
Creator-scoped. Platform catalog administration and cross-Creator reporting
require separate explicit authority.

Ordinary operational telemetry and product analytics do not become content
ownership, license evidence, a usage ledger, or the audit trail. Analytics must
not contain private plan context, traveler details, raw searches, or precise
locations.

## Incremental Delivery

1. Approve the idea, immutable version, ownership, lifecycle, attribution,
   license, Resource, freshness, visibility, and provenance contracts.
2. Define the reviewed, idempotent launch-content package and deterministic
   importer without adding destination content to application code.
3. Add Content Engine storage and an authorized query contract consumed through
   `ITravelContentService` or a narrower approved abstraction.
4. Add an internal editorial workflow for draft, review, publication,
   supersession, retirement, rights correction, and moderation.
5. Populate a small launch collection and verify content, imagery, attribution,
   destination matching, freshness, entitlements, and environment isolation.
6. Connect the Planner rail to the authorized library query.
7. Add reviewed **Add to plan** provenance and then accessible drag and drop.
8. Add Creator, professional, and traveler submission workflows only after
   ownership, consent, licensing, moderation, payment, and reporting policies
   are approved.

## Definition of Done

- AdventuresSuite can launch with useful curated ideas without hardcoding
  production destination content.
- Every idea and published version has explicit Creator ownership, provenance,
  attribution, rights, lifecycle, freshness, and visibility.
- Published versions are immutable and retirement cannot rewrite customer
  plans.
- Private Creator or traveler material cannot enter another Creator's catalog
  or the platform library implicitly.
- Content, Resource, entitlement, Planning, audit, and reporting boundaries are
  enforced below the UI.
- Seed import is reviewed, versioned, idempotent, environment-specific, and
  recoverable.
- The rail remains useful and honest when images, ideas, licenses, entitlements,
  providers, or freshness checks are unavailable.
- Creator-isolation, authorization, entitlement, licensing, prohibited-data,
  freshness, versioning, import, audit, reporting, accessibility, and failure
  tests pass.
