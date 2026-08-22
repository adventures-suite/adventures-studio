# FootStep Catalog Editorial Baseline

**Status:** Development-preview editorial baseline

**Last reviewed:** August 21, 2026

## Purpose

The first real-world FootStep catalog proves a small, reviewable collection of
Destination, Activity, and Accommodation suggestions against the existing
Planner discovery contract. It remains an authenticated Development preview.
Production continues to fail closed until the reviewed Content Engine import,
publication, visibility, licensing, and authorized `ITravelContentService` (or
approved narrower Content Engine) adapter described in
`docs/architecture/planner-curated-idea-library.md` is implemented.

The catalog is editorial content, not proof that a place is open, available,
bookable, safe, accessible, affordable, or suitable for a traveler.

## Editorial Rules

- Prefer primary sources owned by governments, park authorities, official
  tourism organizations, attractions, transportation operators, and the
  accommodation itself.
- Paraphrase only the minimum planning fact needed for a useful suggestion. Do
  not copy promotional prose.
- Record the source owner, HTTPS URL, retrieval date, review date, and next
  review date for every real-world item.
- Create a new immutable version when a reviewed claim changes. Never revise a
  published version in place in a future production catalog.
- Keep operational details out of summaries when a durable planning suggestion
  works without them. Hours, prices, schedules, closures, entry rules, weather,
  marine conditions, accessibility features, and availability require a close-
  to-travel recheck.
- Describe accommodations as options or stay patterns only. Do not rank,
  endorse, quote availability, claim a guaranteed price, or attach a booking
  operation.
- Add no photograph unless Resource ownership, license, attribution, allowed
  use, and publication state are separately approved.

## Current Contract Fit

The existing provider-neutral Planner contract safely supports stable identity
and version, explicit source Creator ownership, source evidence, context, place,
kind, transportation, category, route and surface, accessibility, pace, season,
equipment, budget band, traveler composition, language, and whole-day duration.
It also supports reviewed Destination and Activity drafts.

The following production contract gaps remain deliberately deferred:

- durable Content Engine lifecycle, immutable publication, visibility,
  licensing, rights, moderation, and idempotent import evidence;
- durable place identities rather than controlled geographic tags;
- sub-day duration and filter ranges for activities;
- a typed Accommodation review/apply draft and exact-source provenance; and
- authorized source links and source-Creator presentation in the Planner UI.

These gaps must not be worked around with Razor conditionals, booking links,
free-form fields, SQL seed rows, or direct Planning mutation.
