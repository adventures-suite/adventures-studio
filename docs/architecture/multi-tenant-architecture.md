# Multi-Tenant Architecture

**Version:** 1.0

**Status:** Approved
**Last Updated:** August 2026

## Purpose

This document defines the tenant-isolation rules for AdventuresSuite.

The Creator Engine specification defines how a Creator is identified and made
available to platform capabilities. This document defines the security boundary
that all those capabilities must preserve.

## Tenancy Boundary

A Creator is the AdventuresSuite tenant and the owner of creator-specific data.

A Creator may represent:

- An individual
- A family
- An author or photographer
- A company
- A school or university
- A church or nonprofit
- A tour operator
- Another organization publishing adventures

The type of Creator does not change the isolation model.

Every Creator receives a stable `CreatorId` that is independent of its display
name, slug, domain, subscription, or storage location.

## Ownership Model

Creator-owned data includes:

- Brand and domains
- Adventures, volumes, journeys, and destinations
- Stories, memories, guides, and experiences
- Media and resources
- Public addresses and aliases
- Publishing configuration
- Feature availability
- Search documents
- Analytics and audience relationships

Users do not become the tenancy boundary. A User receives permissions within a
Creator. An Organization may be modeled as a Creator. Publisher describes a
Creator capability or role.

## Mandatory Isolation Rules

1. Every creator-owned persistent record includes `CreatorId`.
2. Core engine operations require `CreatorId`; creator scope is never optional.
3. Public slugs are unique within a Creator, not globally.
4. The complete public address key is `CreatorId + Slug`.
5. Cache keys include Creator identity.
6. Search indexes preserve Creator identity and publication state.
7. Background operations carry explicit Creator identity.
8. Resource access verifies Creator ownership.
9. Administrative authorization is evaluated within Creator scope.
10. Analytics and telemetry include Creator identity without leaking private
    content.
11. Disabled or unpublished Creators expose no public content.
12. Unknown production hosts fail safely and never select a default Creator.

Tenant isolation must be enforced by APIs and data access—not developer memory.

## Request Isolation

```text
HTTP Request
    ↓
Normalize and validate Host
    ↓
Resolve approved Host to CreatorId
    ↓
Establish scoped Creator Context
    ↓
Resolve address or application route within Creator
    ↓
Load Creator-owned content and resources
    ↓
Render Creator-branded response
```

Creator resolution happens once per request. Downstream capabilities consume
the established context or receive `CreatorId` explicitly.

Development host aliases must be explicitly configured. Production code must
not treat arbitrary hosts as The Simonton Adventures.

## Cache and Background Work

Valid cache keys include Creator identity:

```text
creator:{creatorId}:volume:{volumeSlug}
creator:{creatorId}:destination:{destinationSlug}
creator:{creatorId}:address:{publicSlug}
```

Background messages and scheduled work must serialize a stable `CreatorId`.
They must not depend on ambient HTTP request state.

## Required Isolation Tests

Before multi-tenant behavior is production-ready, automated tests must prove:

- Two Creators may use the same slug without collision.
- A request for Creator A cannot retrieve Creator B content.
- Address resolution requires both Creator and slug.
- Unknown hosts do not resolve to a Creator.
- Cache entries cannot cross Creator boundaries.
- Unpublished and disabled Creators expose no public routes.
- Background operations require explicit Creator identity.
- QR codes use the resolved Creator's approved public domain.
- Canonical URLs use the Creator's primary domain.
- Feature flags and brand values do not bleed between requests.

At least one synthetic second Creator must exist in automated tests before the
platform claims multi-tenant support.

## Initial Deployment Model

Multiple Creators may share one AdventuresSuite deployment:

```text
Creator custom domains
    ↓
Azure App Service
    ↓
Creator Engine host resolution
    ↓
Shared AdventuresSuite application
```

The initial design does not require a separate deployment, database, or storage
account for each Creator. Logical isolation remains mandatory regardless of the
physical hosting model.

## Guiding Rule

There must be no creator-owned query whose Creator scope is optional.
