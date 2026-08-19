# Content Engine

## Purpose

The Content Engine is the central platform capability responsible for the complete lifecycle of content within AdventuresSuite.

Every piece of content owned by AdventuresSuite is created, validated, stored, retrieved, related, versioned, and published through the Content Engine.

The Content Engine owns content.

Other platform capabilities consume content.

Reusable Planner ideas are Content Engine records, not private Planning state.
They retain explicit Creator ownership, immutable published versions,
attribution, licensing, freshness, visibility, and editorial lifecycle. The
Planner consumes authorized projections through `ITravelContentService` or a
narrower approved Content Engine contract. See
`docs/architecture/planner-curated-idea-library.md`.

---

## Responsibilities

The Content Engine is responsible for:

- Creating content
- Validating content
- Storing content
- Retrieving content
- Updating content
- Publishing content
- Versioning content
- Relating content
- Managing content identity

The Content Engine does not render content.

The Content Engine does not generate QR codes.

The Content Engine does not perform search.

Those capabilities consume content through the Content Engine.

---

## Consumers

Examples include:

- Rendering Engine
- Address Engine
- QR Engine
- Search Engine
- Navigation Engine
- Discovery Engine
- Planning Engine
- AI Companion

Consumers must not depend on JSON files, databases, or storage implementations.

Consumers depend only on the Content Engine.

---

## Principles

The Content Engine is storage independent.

The Content Engine is rendering independent.

The Content Engine is transport independent.

The Content Engine is responsible for content—not presentation.

Every new AdventuresSuite capability should consume content rather than creating its own storage model.

---

## Destination temporal metadata

Destination records distinguish four different temporal concepts:

- The destination `timeZone` is an IANA geographic time-zone identity.
- Planned arrival and departure are date-only expectations.
- Visited-from and visited-to are date-only historical facts.
- Content audit and publication values are UTC timestamps.

Date-only travel values remain local calendar dates. They are not timestamps
and are never converted through either the destination or Creator time zone.
Presentation formats them with the Creator locale.

Content audit timestamps describe meaningful authored changes. Publication
timestamps describe meaningful public publication. They are optional authored
metadata in the JSON-backed phase and become system-controlled when durable
database-backed publishing exists. A deployment time, file modification time,
build time, or JSON-formatting time is operational metadata and must not be
used as a content or publication timestamp.

The latest-publication timestamp is not itself a notification event. Future
subscription delivery consumes explicit publication domain events rather than
inferring intent from timestamp changes.

### Journey-owned visit schedules

A Destination describes a place and may summarize a planned or completed date
range. A Journey segment owns the typed local schedule for one visit to that
place. The visit schedule contains arrival and departure dates, optional local
arrival and departure times, optional cruise gangway-down and gangway-up times,
and the destination IANA time zone.

Keeping operational timing on the Journey prevents one Adventure's port call
from becoming global Destination metadata. Date values remain `DateOnly` and
clock values remain `TimeOnly`; consumers must not silently convert them to UTC
instants. A future reservation or operations capability may add authoritative
zoned instants without changing these authored local planning semantics.
