# Adventure Map Experience

**Status:** Approved Platform Requirement and Architecture Direction
**Last Updated:** August 9, 2026

## Purpose

The Planning Workspace must let an authorized Creator understand an Adventure
spatially at varying degrees of detail. A user can begin with the whole journey,
then progressively focus on route segments, destination visits, itinerary days,
selected places, and possible points of interest.

The map is an authorized projection of Planning state. It is not an alternate
planning database, a public map, proof of a booking, or a turn-by-turn navigation
system.

## Detail Levels

The experience should support these related views:

1. **Adventure overview** — the complete geographic shape, ordered destinations,
   major transportation legs, date range, and unresolved geographic gaps.
2. **Journey segment** — one movement between places, including mode, origin,
   destination, planned timing, stops, and an approximate or provider-supplied
   route when available.
3. **Destination visit** — the destination boundary or center, accommodation
   area, itinerary days, selected activities, and nearby candidate places.
4. **Day view** — the ordered places and movements relevant to one local
   itinerary day, with time-zone context and schedule conflicts.
5. **Place detail** — one selected or candidate point of interest with source,
   freshness, confidence, accessibility, opening-hours, cost, reservation, and
   planning status where known.

Users may filter layers and zoom without changing authoritative plan state.
Selecting a candidate place for the plan is an explicit Planning mutation or a
reviewable proposal.

## Map Layers and Status

Map features must make semantic differences visible rather than presenting all
markers as equally certain:

- planned destination visits;
- transportation or journey segments;
- confirmed, reserved, proposed, and cancelled itinerary items;
- accommodations and transfer points;
- Creator-selected points of interest;
- AI-, template-, professional-, or research-suggested candidate places;
- warnings such as route gaps, timing conflicts, stale source data, or missing
  coordinates; and
- optional approximate areas when an exact coordinate is unnecessary or too
  sensitive.

Legend, shape, text, and accessible labels must communicate status without
depending on color alone. Clustering and progressive disclosure should keep
dense destinations understandable.

## Domain Boundaries

Planning owns destination visits, itinerary items, transportation segments,
accommodations, selection state, ordering, and schedule. A planning map reads
those records through an authorized projection.

The existing public Content Engine `JourneySegment` is editorial content and is
not automatically the same thing as a private Planning transportation segment.
A future publication operation may deliberately transform approved Planning
facts into public Journey Segments; map rendering must not create that link
implicitly.

Candidate points of interest are not authoritative plan items. They retain a
stable candidate identity, source, retrieval time, freshness, geographic
precision, confidence, and proposal provenance until an authorized user selects
or rejects them.

## Provider-Neutral Spatial Services

Core contracts must not depend on a particular mapping, tile, directions,
geocoding, search, or places provider. Infrastructure adapters may provide:

- geocoding and reverse geocoding;
- map tiles or vector styles;
- place search and details;
- route geometry and travel-time estimates; and
- offline map or place packages where licensing permits.

Provider results are untrusted external data. Store only what contracts,
licensing, freshness, attribution, and product requirements permit. Cache keys
and persisted provider references include Creator and purpose where the data is
private. Provider changes must not alter Planning identities.

## Accuracy and Safety

A straight line, inferred route, estimated travel time, suggested point of
interest, or stale provider result must be labeled accurately. AdventuresSuite
must not imply that a displayed route is safe, accessible, legally available,
open, bookable, or suitable for navigation without authoritative evidence.

Coordinates require a declared source and precision. Approximate destination
coordinates are preferred when exact precision adds no planning value. Route
geometry may be regenerated as a projection; Planning records retain the
authoritative endpoints and schedule rather than treating rendered geometry as
the source of truth.

## Privacy and Authorization

- Every private-map query is authorized for the Creator and Adventure Plan.
- Map viewport, search, cache, export, and background-work boundaries preserve
  Creator isolation.
- Public Destination coordinates do not authorize access to private plan data.
- Traveler GPS breadcrumbs are a separate, opt-in Companion capability and are
  never mixed into planning maps without explicit authorized purpose and
  consent.
- Precise private coordinates, searches, routes, and traveler location do not
  appear in logs, metrics, analytics, notifications, support identifiers, or
  ordinary audit metadata.
- Shareable exports and screenshots use explicit profiles and minimize private
  locations, accommodations, and protected-resource links.

## AI and Professional Collaboration

AI and travel professionals may propose destinations, routes, stops, and points
of interest using the shared proposal boundary. Their suggestions remain
visually distinguishable from accepted plan state. Neither a map interaction
nor a provider result bypasses customer approval, Planning validation, Creator
authorization, or a required plan-scoped professional engagement.

## Companion and Offline Direction

AdventuresCompanion may later consume a minimized, encrypted, revocation-aware
map projection for approved Adventure content. Offline packages record source,
coverage, freshness, license, size, and expiration. Planning-map support does
not authorize background GPS capture or create consent for breadcrumbs.

## Incremental Delivery

1. Define provider-neutral feature, coordinate, viewport, layer, status, source,
   and projection contracts.
2. Render an authorized Adventure overview from existing Planning state using a
   deterministic fake map provider in tests.
3. Add segment, destination, day, and place drill-down with accessible layer
   controls and missing-coordinate handling.
4. Add candidate point-of-interest search and selection through reviewed
   Planning proposals.
5. Add approved geocoding, places, routing, caching, licensing, attribution,
   observability, and cost controls.
6. Add minimized Companion/offline projections after the mobile API and
   synchronization boundaries are proven.

## Definition of Done

- The full Adventure, segments, destinations, days, and places can be explored
  at progressively useful detail.
- Accepted plan state and candidate suggestions are unmistakably different.
- Map operations cannot cross Creator or Adventure boundaries.
- Rendering and provider data cannot silently mutate Planning state.
- Missing, approximate, stale, and inferred geographic data is visible.
- Provider attribution and licensing requirements are enforced.
- Precise private location does not leak through telemetry, caching, exports,
  public routes, or offline packages.
- Keyboard, screen-reader, reduced-motion, contrast, and non-color status tests
  pass.
- Maps degrade to a useful ordered textual itinerary when a provider, network,
  script, or visual display is unavailable.
