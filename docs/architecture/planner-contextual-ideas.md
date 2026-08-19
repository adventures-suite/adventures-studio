# Planner Contextual FootSteps

**Status:** Approved Product and Architecture Direction

**Last Updated:** August 19, 2026

## Purpose

The Planner should make a believable Adventure faster to assemble without
making the user surrender control. The primary workspace keeps the
authoritative plan visible as a planning canvas and presents a compact,
destination-aware FootSteps rail beside it. The rail may offer relevant Journey
FootSteps, Destination FootSteps, itinerary FootSteps, activities, route
patterns, and later reviewed
research or AI suggestions.

The governing principle is:

> FootSteps are reusable inspiration and blueprints. The plan is authoritative.
> Applying a FootStep is an explicit, reviewable Planning operation.

`FootSteps` is the customer-facing AdventuresSuite product term. Internal
contracts may continue to use neutral terms such as template, catalog item,
suggestion, projection, and proposal where those names describe distinct
technical responsibilities. The brand term does not collapse those security,
ownership, lifecycle, or persistence boundaries into one record type.

## Experience Model

On a wide screen, the Planner uses three coordinated regions:

1. the existing collapsible workspace navigation on the left;
2. the selected Adventure Plan and itinerary board in the main canvas; and
3. a compact contextual FootSteps rail on the right.

The canvas remains the visual and keyboard focus. Selecting a destination visit,
itinerary day, or empty insertion point changes the FootSteps rail context
without changing plan data. Cards use strong imagery when licensed media is
available, short useful labels, source and freshness indicators, and an honest
explanation of why the FootStep is relevant.

An open plan begins at whole-Adventure context, emphasizing Journeys,
Destinations, templates, and route patterns. Destination selection emphasizes
sample days, activities, stays, and local movement. Day selection emphasizes
activities, meals, pacing, and nearby possibilities. Future segment,
accommodation, activity, and route-position contexts follow the hierarchy in
`docs/architecture/planner-curated-idea-library.md`. Context and filters are
transient presentation state; they never change the plan.

The rail is collapsible and resizable within bounded limits. On narrow screens
it becomes an explicitly opened drawer or sheet rather than permanently
compressing the itinerary. Hiding or resizing the rail is a user preference,
not an authorization decision.

## Interaction Contract

Pointer users may drag a FootStep toward an eligible plan destination, day, or
insertion point. Dragging means **copy this FootStep into a proposed plan
change**; it never moves or edits the source item. A drop opens a concise
preview with the destination, date or day, proposed fields, assumptions, and
conflicts. The user confirms or cancels before the normal Planning command runs.

Drag and drop is an enhancement, not the only workflow. Every card also offers
an accessible action such as **Add to plan**. Keyboard and assistive-technology
users can choose the target destination or day through ordinary labeled
controls and receive the same preview and validation. Touch uses the explicit
action rather than requiring precision dragging.

The first implementation should not support free-form reordering, automatic
schedule placement, multi-item dragging, or direct mutation on drop. Those
behaviors require separate domain operations and usability evidence.

## Source Classes

The rail may combine several source classes, but they remain visibly and
structurally distinct:

- published Creator or platform travel content obtained through
  `ITravelContentService`;
- immutable Adventure Template material the current Creator is entitled and
  authorized to use;
- private Creator-authored reusable ideas;
- provider-backed place or route candidates with attribution and freshness;
- grounded research with source evidence; and
- AI or professional proposals awaiting review.

AdventuresSuite should prepopulate a reviewed launch collection through the
Content Engine rather than waiting for community supply. Its ownership,
immutable versions, editorial lifecycle, attribution, licensing, freshness,
visibility, entitlement, Resource, moderation, import, and environment rules
are defined in `docs/architecture/planner-curated-idea-library.md`.

Published editorial records, templates, provider candidates, and AI proposals
do not become Planning records merely because they appear beside a plan. The UI
must distinguish curated, suggested, stale, unavailable, already added, and
authoritative plan states without depending on color alone.

## Provider-Neutral Contracts

The presentation layer consumes an authorized `PlannerIdeaProjection`; it does
not query providers, template storage, or public JSON directly. A future
provider-neutral application contract should accept explicit Creator, plan,
actor, selected-context, paging, and filter values and return only allowlisted
card data.

Candidate concepts include:

- `PlannerIdeaId` and stable source reference;
- source class and source Creator when disclosure is authorized;
- target kinds supported by the idea;
- title, short summary, optional licensed thumbnail Resource reference, and
  attribution;
- applicable destination, date window, duration, and time-zone assumptions;
- freshness, confidence, availability, and entitlement state;
- proposal provenance and a minimal reason-for-match; and
- a typed operation draft used to construct a review preview.

The contract must not expose provider SDK types, prompts, arbitrary payloads,
permanent protected-Resource URLs, private content from another Creator, live
inventory claims, or booking credentials. Cache keys and background work carry
Creator, plan, source, purpose, and applicable entitlement context.

## Context and Ranking

Context is derived from authorized Planning projections. The user may select an
Adventure, destination visit, itinerary day, or insertion point. Matching can
consider approved plan dates, destination identity, local time zone, existing
schedule, traveler preference projections, pace, accessibility constraints,
transportation preference, and budget range only when each datum is authorized
and appropriate for the source.

The first slice should use deterministic filtering and ordering. Later ranking
may use research or AI, but it must remain explainable, bounded, testable, and
separate from mutation. A destination name string is not a durable place
identity; ambiguous place matching stays unresolved until reviewed.

## Faceted Discovery and Filtering

Filtering is a near-term product slice, not a future marketplace convenience.
The Ideas experience must remain useful as the curated library grows beyond a
small Alpha collection. Users can deliberately narrow the authorized result set
by combining facets such as:

- country, region, or multiple destinations;
- idea kind, including Journey, Destination, itinerary item, activity,
  transportation, and stay;
- trip-duration range and available days;
- transportation modes;
- route style, terrain or surface, daily travel-distance or duration range, and
  vehicle or equipment needs;
- travel categories and styles such as culture, food, history, outdoors,
  cruise, road trip, relaxation, and family travel;
- pace, season or preferred month, accessibility characteristics, budget range
  and currency, and traveler composition; and
- source class, language, and permitted commercial availability such as free,
  included, or separately licensed.

Context supplies explainable initial facets rather than silently constraining
discovery. Selecting a Portugal destination may initially scope results to
Portugal and relevant item kinds, while the user can remove that facet or add
Spain or Western Europe. Active filters appear as removable chips with an
unambiguous result count and **Clear all** action. Filters persist while paging;
the user-selected page size may be retained as a presentation preference.

Facet values use stable structured identifiers and controlled vocabularies,
not comparisons against localized display text. Display labels are localized
at the presentation boundary. Dates, duration, money, currency, transportation
modes, countries, categories, and accessibility values remain culture-neutral
in application contracts. The query contract carries requested locale and uses
an explicit fallback from requested locale to source locale to an identified
platform default.

On narrow screens, filters open in a focused drawer or sheet with Apply, Clear,
and Cancel behavior. Focus returns to the invoking control, selected values are
announced, and closing the filter surface does not discard applied filters.
Filtering never implies availability, price, safety, accessibility suitability,
booking status, or endorsement.

The server applies Creator visibility, source authorization, entitlement,
licensing, lifecycle, and publication rules before returning facet counts or
cards. An unavailable or unauthorized source cannot be inferred through counts,
labels, empty groups, timing, cache entries, or pagination. Filter state is
transient and never mutates the authoritative plan.

The facet vocabulary must be tested beyond conventional vacations. Motorcycle
touring is the first proving scenario and must support discovery by motorcycle,
scenic or direct route style, paved/gravel/off-road surface, daily riding
distance or time, route duration, countries, ferry use, accommodation needs,
and relevant categories. The same composable vocabulary must extend without a
new top-level tenant or plan model to RV, cycling, trekking, sailing, rail,
cruise, overland, winter, diving, pilgrimage, accessible, event, and other
Adventure modes.

## Mutation Boundary

Selecting a FootStep follows the same safe sequence regardless of input method:

```text
Authorized context selection
    -> authorized idea projection
    -> typed operation draft
    -> validation and change preview
    -> explicit Creator confirmation
    -> existing Planning application service
    -> optimistic concurrency and atomic audit intent
    -> refreshed authoritative plan
```

The server reauthorizes the actor, Creator, plan instance, source use, target,
entitlement, lifecycle, and expected plan version. Client visibility, a drag
payload, card identity, or source entitlement never authorizes a mutation.
Unknown fields and stale or mismatched context fail closed. Exact replay follows
the owning Planning command's idempotency rules.

## Privacy, Trust, and Commercial Boundaries

- Private Planning data is minimized before matching against any external
  service.
- A public Destination does not reveal that a Creator is considering or visiting
  it.
- Recommendations never imply current price, availability, suitability,
  accessibility, safety, reservation, or professional endorsement without
  attributable evidence.
- Subscription capability and time-bounded promotional access are evaluated
  below the UI with stable Platform Capability values, not marketing plan
  names.
- Locked cards may be discoverable only when product policy permits; an
  authorization-denied source is not disclosed.
- Adding a template or professional idea grants no continuing access to the
  customer plan.
- Analytics use approved low-sensitivity events and never copy plan content,
  traveler details, searches, prompts, or precise private locations.

## Accessibility and Responsive Requirements

- The canvas, rail, cards, targets, preview, and resulting status have semantic
  names and predictable focus order.
- Keyboard users can add every idea without simulating pointer dragging.
- Drag state, eligible targets, rejection, validation, and completion are
  announced without relying on motion or color.
- The rail and preview meet WCAG 2.2 AA in light, dark, and system themes.
- Long labels, missing images, reduced motion, zoom, 320 CSS-pixel layouts, and
  provider failure retain a useful experience.
- When the rail is unavailable or empty, manual planning remains fully usable.

## Incremental Delivery

1. Add a presentation-only rail shell with fictional deterministic projections,
   selection context, empty/loading/unavailable states, and no mutation.
2. Add an authorized provider-neutral query contract backed first by existing
   published content through `ITravelContentService`; do not hardcode
   destination content.
3. Add deterministic faceted filtering, active-filter chips, result counts,
   paging integration, and the accessible mobile filter surface. This is the
   next Ideas priority after the query boundary and must precede ranking.
4. Establish the reviewed curated Idea Library foundation and a small
   environment-specific launch collection.
5. Add the explicit **Add to plan** preview for one existing Planning command
   and prove authorization, concurrency, audit, idempotency, and source
   provenance.
6. Add accessible drag and drop as an equivalent enhancement to that proven
   action.
7. Add template, private reusable idea, grounded research, map/place, and AI or
   professional sources only after each owning architecture and permission
   boundary is implemented.
8. Add measured ranking and personalization only after consent, classification,
   evaluation, reporting, retention, and cost policies are approved.

## Definition of Done

- The authoritative plan remains unmistakable from every suggestion source.
- A user can discover and add a relevant idea with pointer, keyboard, touch, or
  assistive technology.
- No drag, selection, recommendation, or client-side state directly mutates the
  plan.
- Creator, plan, source, target, entitlement, lifecycle, and version checks are
  enforced below the UI.
- Source, attribution, freshness, confidence, and assumptions survive into the
  review preview and appropriate provenance evidence.
- Manual planning remains complete when suggestions, providers, images, AI, or
  network access are unavailable.
- Dark mode, narrow layouts, empty and denied states, and realistic content are
  verified in a real browser.
- Combined facets, context defaults, clearing, paging, localized labels, and
  mobile filter interaction are deterministic, accessible, and leakage-safe.
