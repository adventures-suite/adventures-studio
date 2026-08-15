# Published Cruise Itinerary Import

**Status:** Deferred Provider Integration; Approved Architecture Direction

**Last Updated:** August 12, 2026

## Purpose

AdventuresSuite should eventually let an authorized Creator find a published
cruise sailing and use its ordered port schedule as the starting point for a
private Adventure Plan. This is a convenience import, not booking, reservation
lookup, or proof that a sailing will operate as published.

Commercial activation is intentionally deferred until Adventures Studio is an
operating business that can evaluate providers and enter an agreement in an
official capacity. The dormant contracts may be developed and tested before
then, but production adapters, credentials, live data, and supplier branding
must not be enabled without the approval gate in this document.

## Research Summary and Recommendation

There is no dependable, broadly adopted consumer export standard across cruise
lines. Individual cruise-line pages and passenger applications vary, and
maritime tracking or port-call feeds describe vessel movements rather than the
published guest itinerary.

When Adventures Studio is ready to contact vendors, evaluate these lanes in
order:

1. **Traveltek** as the first commercial structured-content candidate. Confirm
   itinerary coverage, permitted persistence, update cadence, consumer-planning
   display rights, attribution, pricing, and termination behavior.
2. **Widgety** as the second commercial candidate, using the same evaluation
   criteria and a representative coverage trial.
3. **Direct cruise-line feeds** only where a line offers a documented partner
   relationship whose terms permit this use.
4. **Protected document or pasted-text ingestion** as the provider-independent
   fallback described by `itinerary-ingestion.md`.

Google's cruise itinerary feed demonstrates a useful industry data shape—one
sailing identity plus ordered calls, dates, local arrival/departure values,
ship, duration, and optional coordinates—but it is a partner feed into Google,
not a public read API for AdventuresSuite.

Do not scrape cruise-line, aggregator, or mapping sites. Do not treat AIS,
vessel-position, or port-schedule data as a substitute for the cruise line's
published consumer itinerary.

## Commercial Activation Gate

No live provider adapter may be registered until an owner-approved review has
recorded:

- the contracting party and authorized provider account;
- permitted search, display, caching, persistence, transformation, and
  customer-facing uses;
- attribution, source-link, trademark, image, and geographic-data obligations;
- update frequency, correction, deletion, export, and agreement-termination
  requirements;
- geographic, cruise-line, ship, and sailing coverage measured against a
  representative test set;
- rate limits, availability, support, cost, and expected free-tier impact;
- privacy and security terms, even though passenger booking details are never
  requested; and
- a safe provider-disable and retained-plan behavior.

Legal permission to access a feed does not by itself prove permission to retain
or present the data inside a consumer planning product.

## Authority Boundary

```text
Creator-scoped sailing search
    -> licensed provider adapter
    -> normalized published sailing snapshot
    -> Journey Stop proposals with provenance and warnings
    -> Creator review and correction
    -> authorized, concurrency-safe Planning mutation plus audit intent
```

Provider results are untrusted proposals. They never directly create or update
an `AdventurePlan`, `DestinationVisit`, `ItineraryDay`, transportation segment,
reservation, public Content Engine `JourneyStop`, map route, or calendar event.

The route Creator identifier is untrusted input. Membership, resource-aware
authorization, and entitlement are evaluated below the UI before searching or
viewing imported details and again before an approved Planning mutation.

## Provider-Neutral Contract

`IPublishedCruiseItineraryProvider` is the narrow application seam. Every
operation begins with explicit `CreatorId` even when the remote catalog is
shared, so caches, quotas, telemetry, and future stored search state cannot
silently become cross-Creator.

The contract supports:

- searching by cruise line, ship, and departure-date window;
- retrieving one opaque provider sailing reference;
- reporting source freshness independently from retrieval time; and
- returning an unavailable result without throwing provider details into the
  user experience.

The normalized `PublishedCruiseSailing` contains only:

- opaque provider and sailing references;
- cruise line, ship, optional voyage and itinerary labels;
- departure and return dates;
- retrieval time, optional source-updated time, and license/attribution
  reference;
- ordered cruise days classified as embarkation, port call, sea day,
  disembarkation, or unknown;
- raw place label, local arrival/departure values, optional coordinates, and
  field-level source and confidence; and
- proposed normalized place and IANA time-zone identifiers, never silently
  accepted values.

Passenger name, reservation number, cabin, loyalty identity, payment data,
booking PIN, ticket code, and private notes are outside this contract.

## Snapshot, Freshness, and Change Semantics

The provider sailing reference is opaque and is never used as a Planning
identity. A normalized snapshot records both `RetrievedAtUtc` and the provider's
optional `SourceUpdatedAtUtc`. A content fingerprint is computed from the
allowlisted normalized fields and contract version, not provider JSON.

Two fetches with the same fingerprint are equivalent observations. A changed
fingerprint creates a new reviewable snapshot; it does not overwrite accepted
Planning state. The review experience explains port, day, or time changes and
requires a new explicit decision.

Provider cache retention follows the approved agreement and data
classification. Without an approved agreement, production retention is zero
because no live calls are permitted. Accepted Planning facts may remain after a
provider is disabled, with their provenance and last-known freshness, unless
the agreement requires deletion of derived data. Raw provider payloads are not
retained by default.

## Adapter and Failure Boundary

Provider SDK, authentication, paging, transport errors, rate limits, and raw
schemas remain in an infrastructure adapter. Core Planning contracts contain
no vendor names or SDK types. Adapters use bounded timeouts and cancellation,
validate every field, and map failures to stable categories without leaking
credentials, raw payloads, Creator data, or supplier internals.

The safe default implementation is unavailable and performs no network call.
It is not registered as a feature switch that implies entitlement or
authorization. A deterministic fake may supply fictional sailings in tests and
design previews only; it must never be selectable in production.

## Intended Workspace Experience

The future flow is deliberately simple:

1. Choose cruise line.
2. Choose ship.
3. Choose sailing date.
4. Compare likely matches with visible source and freshness.
5. Review the ordered day cards and map preview.
6. Correct unresolved ports, dates, times, and time zones.
7. Approve the exact additions to the private Adventure Plan.

The itinerary list remains the accessible source of meaning; the map is a
supporting projection. Dark mode, keyboard use, narrow screens, resizable or
hidden workspace navigation, and clear unavailable/stale states are required.

## Required Tests Before Activation

- contract and adapter validation for malformed, missing, duplicate, and
  out-of-order days;
- sea days, overnights, date-line crossings, absent times, and ambiguous ports;
- exact Creator scoping through search, cache, quota, and retrieval operations;
- unauthorized, forged, revoked, and stale membership behavior;
- timeout, cancellation, throttling, partial response, provider outage, and
  credential failure without protected-data disclosure;
- repeated fetch equivalence and changed-fingerprint review behavior;
- concurrent review attempts against a changed plan version;
- no Planning or audit write before approval, and atomic Planning plus required
  audit write after approval;
- disabled-provider behavior with zero network access; and
- license expiry or adapter removal without loss of authorized private plan
  state beyond an explicit contractual deletion requirement.

## Deferred Work

The current stub does not add a vendor SDK, credentials, HTTP client, database
table, migration, cache, endpoint, UI, entitlement, supplier imagery, or live
data. Those are separate reviewed slices after the business and commercial
activation gates are satisfied.

## Research References

- [Traveltek Cruise API](https://www.traveltek.com/travel-api-provider/cruise-api/)
  describes partner access to multi-line cruise search and detailed itinerary
  data, including a non-bookable market-cache option.
- [Traveltek Cruise Connect schema](https://schema.cruiseconnect.traveltek.net/)
  documents authenticated itinerary item classifications such as port, sea,
  embarkation, disembarkation, and international-date-line crossing.
- [Widgety API](https://widgety.org/product/api/) describes a commercial REST
  API with cruise search, detailed itineraries, and supplier-updated content.
- [Widgety developer documentation](https://developer.widgety.co.uk/) confirms
  that company details and assigned application credentials are required.
- [Google cruise itinerary feed](https://developers.google.com/actions-center/verticals/cruises/itinerary-feed)
  documents a partner-supplied sailing and ordered-stop schema. It is useful as
  evidence of a normalized data shape, not as a public retrieval API.
