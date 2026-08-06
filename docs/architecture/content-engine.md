# Content Engine

## Purpose

The Content Engine is the central platform capability responsible for the complete lifecycle of content within AdventuresSuite.

Every piece of content owned by AdventuresSuite is created, validated, stored, retrieved, related, versioned, and published through the Content Engine.

The Content Engine owns content.

Other platform capabilities consume content.

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