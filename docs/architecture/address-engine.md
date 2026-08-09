# Address Engine

## Purpose

The Address Engine provides stable, permanent public addresses for content managed by AdventuresSuite.

It resolves public slugs and aliases to content without requiring consumers to know the content's internal storage location, route structure, or content type.

The Address Engine separates public identity from internal implementation.

---

## Responsibilities

The Address Engine is responsible for:

- Resolving stable public slugs
- Resolving legacy aliases
- Mapping public addresses to content identity
- Returning canonical target routes
- Preserving published addresses over time
- Enforcing global address uniqueness
- Rejecting unknown or unpublished targets
- Supporting internal and approved external targets
- Validating address registrations
- Supporting future address analytics

The Address Engine does not create content.

The Address Engine does not render content.

The Address Engine does not generate QR code images.

---

## Address Resolution

The Address Engine resolves a stable public address to a target managed by the platform.

```text
Public slug
    ↓
Address Engine
    ↓
Content identity
    ↓
Content Engine
    ↓
Content