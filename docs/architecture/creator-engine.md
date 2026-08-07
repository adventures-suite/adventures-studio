# Creator Engine

**Version:** 1.0

**Status:** Approved
**Last Updated:** August 2026

## Purpose

The Creator Engine identifies the Creator associated with an AdventuresSuite
operation and establishes the tenancy context consumed by other platform
capabilities.

Adventures Studio is the company that owns and operates AdventuresSuite.
The Simonton Adventures is the first Creator and flagship implementation.

The first implementation remains JSON-backed and supports one real Creator. It
must nevertheless establish the same explicit boundaries required for many
Creators.

## Core Principle

> Everything presented through AdventuresSuite belongs to a Creator and is
> delivered through reusable platform capabilities.

Creator identity is stable and must not depend on mutable public properties.

Do not use a display name, slug, domain, subscription plan, or storage location
as the internal Creator identity.

## Responsibilities

The Creator Engine owns:

- Stable Creator identity
- Creator lifecycle status
- Creator manifests and retrieval
- Approved domain registration
- Host-to-Creator resolution
- Creator Context creation
- Creator brand configuration
- Creator locale and time zone
- Creator-scoped feature configuration

The Creator Engine does not own:

- Travel content lifecycle
- Public address targets
- QR image generation
- Page rendering
- User authentication
- Creator authorization policies
- Media storage

Those responsibilities belong to other engines, which consume Creator identity
and context.

## Initial Domain Types

The initial implementation should introduce types equivalent to:

```csharp
public readonly record struct CreatorId(string Value);

public sealed class Creator
{
    public required CreatorId Id { get; init; }
    public required string Slug { get; init; }
    public required string DisplayName { get; init; }
    public required string PrimaryDomain { get; init; }
    public IReadOnlyList<string> Domains { get; init; } = [];
    public CreatorStatus Status { get; init; } = CreatorStatus.Draft;
    public CreatorBrand Brand { get; init; } = new();
    public CreatorFeatures Features { get; init; } = new();
    public string Locale { get; init; } = "en-US";
    public string TimeZone { get; init; } = "UTC";
}
```

The exact property set may evolve, but stable identity, status, approved domains,
and brand configuration are required boundaries.

Prefer a strongly typed `CreatorId` throughout engine contracts so it cannot be
confused with a slug or domain.

The default value of `CreatorId`, an empty value, or a whitespace-only value is
invalid. Manifest loading and public construction boundaries must reject these
values rather than allowing an unscoped identity to enter an engine contract.

## Initial JSON Manifest

The first Creator manifest should be stored as content rather than hard-coded:

```text
Content/
└── Creators/
    └── the-simonton-adventures/
        └── creator.json
```

Example logical structure:

```json
{
  "id": "creator_tsa_01",
  "slug": "the-simonton-adventures",
  "displayName": "The Simonton Adventures",
  "status": "Active",
  "primaryDomain": "thesimontonadventures.com",
  "domains": [
    "thesimontonadventures.com",
    "www.thesimontonadventures.com"
  ],
  "locale": "en-US",
  "timeZone": "America/Phoenix",
  "contentRoot": "Content/Volumes",
  "brand": {},
  "features": {}
}
```

Development aliases such as `localhost` belong in environment-specific
configuration or an explicitly development-only manifest override.

A manifest may set `developmentOnly` for permanent local fixtures. Such a
Creator remains available to startup validation but must be rejected by both
registered-domain and alias resolution outside the Development environment.

The initial `contentRoot` may point to the existing volume directory. Physical
content reorganization is deliberately deferred until logical Creator scoping
is working and tested.

`contentRoot` is a storage locator and therefore part of the tenant-isolation
boundary. The initial JSON implementation must require a normalized, relative
application-content path. It must reject rooted paths, parent-directory
traversal, and any resolved path outside the approved application content root.

## Contracts

The first implementation should provide contracts with these responsibilities:

```csharp
public interface ICreatorService
{
    Task<Creator?> GetByIdAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default);

    Task<Creator?> GetByHostAsync(
        string host,
        CancellationToken cancellationToken = default);
}

public interface ICreatorResolver
{
    Task<CreatorContext?> ResolveAsync(
        HostString host,
        CancellationToken cancellationToken = default);
}

public interface ICreatorContextAccessor
{
    CreatorContext Current { get; }
}
```

Responsibilities remain separate:

- `ICreatorService` retrieves Creator records.
- `ICreatorResolver` normalizes and resolves an incoming host.
- `ICreatorContextAccessor` exposes context already established for a request.
- Creator middleware establishes the context exactly once.

The context accessor must be scoped to the request. Creator records and an
immutable manifest index may be singleton services.

## Creator Context

Creator Context is a request-scoped, immutable view containing the information
downstream capabilities need, such as:

- Creator identity
- Creator slug and display name
- Requested host
- Primary domain
- Brand configuration
- Locale and time zone
- Feature availability
- Content-root identity during the JSON transition

Context must never be mutable shared state. A singleton holding the current
Creator would leak data between concurrent requests.

## Host Resolution

Host resolution must:

1. Normalize case and remove a valid port.
2. Reject empty, malformed, or unapproved hosts.
3. Match an explicit approved-domain registration.
4. Reject inactive Creators.
5. Return one unambiguous Creator Context.

Production must not fall back to the flagship Creator when a host is unknown.

Reverse-proxy forwarded-host handling may be enabled only through ASP.NET Core's
trusted forwarded-header configuration. Do not trust arbitrary forwarded-host
headers.

Azure App Service supplies its environment-specific default host through the
trusted `WEBSITE_HOSTNAME` process setting. Creator resolution maps that host to
the explicitly configured `CreatorResolution:AzureDefaultCreatorId`; Azure app
names and hostnames are never embedded in source. Custom domains remain owned by
Creator manifests.

## Content Engine Integration

The current `ITravelContentService` implicitly accesses one global collection.
Core content operations must evolve to require Creator identity:

```csharp
Task<Volume?> GetVolumeAsync(
    CreatorId creatorId,
    string volumeSlug,
    CancellationToken cancellationToken = default);
```

Explicit Creator identity is preferred in engine contracts because it remains
safe in HTTP requests, tests, APIs, and background work.

Request-facing facades may read `ICreatorContextAccessor`, but the underlying
engine operation must still receive `CreatorId` explicitly.

During migration, an adapter may supply the single flagship Creator identity to
existing consumers. That adapter must be temporary, clearly named, and never
used as an unknown-host fallback in production.

## Address Engine Integration

The Address Engine lookup key becomes:

```text
CreatorId + public slug
```

Its core resolution contract should evolve toward:

```csharp
Task<AddressableContentRoute?> ResolveAsync(
    CreatorId creatorId,
    string slug,
    CancellationToken cancellationToken = default);
```

Two Creators may both own `/go/acropolis`. The incoming host identifies the
Creator; the slug identifies the target within that Creator.

Aliases, redirects, canonical URLs, publication checks, and QR assets must be
resolved within the same Creator boundary.

## Branding and Configuration

Creator-owned configuration includes:

- Display name and logos
- Favicon
- Structured color and typography tokens
- Copyright and social links
- Default SEO metadata
- Locale and time zone
- Creator-specific feature availability

Global infrastructure settings remain in platform configuration. Creator brand,
domains, and feature availability must not remain in `PlatformOptions` once the
Creator Engine owns them.

Prefer structured brand values over arbitrary Creator-supplied CSS.

The JSON-backed implementation exposes default SEO text, validated hexadecimal
primary and accent colors, and an approved typography token through
`CreatorBrand`. Shared presentation must apply safe fallbacks to Creator display
identity when optional brand text is absent. Locale controls document language,
and time zone controls Creator-local presentation such as copyright year.

## Indexing and Startup Validation

The JSON-backed Creator service builds one immutable deployed-manifest registry
per application process. Creator identity and normalized approved domains are
indexed during the same load that enforces global registration uniqueness.

Before the application accepts requests, every registered Creator's content is
validated within that Creator boundary. Errors prevent startup; warnings keep
intentional draft and planned placeholders observable without blocking the
flagship site. Content response caching remains deferred until profiling shows
a concrete need. Future cache keys must begin with stable Creator identity.

## Storage Evolution

The desired long-term logical layout is:

```text
Content/Creators/{creator}/
├── creator.json
└── Adventures/{adventure}/
    ├── adventure.json
    ├── volumes/
    ├── journeys/
    └── destinations/
```

This physical migration is not the first step. Establish Creator identity and
creator-scoped contracts before moving working content.

Future JSON, database, Blob Storage, search, and API implementations must expose
the same Creator-scoped behavior.

## Azure Model

The initial hosting model remains one AdventuresSuite application:

```text
Multiple Creator domains
    ↓
One Azure App Service
    ↓
Creator Engine
    ↓
Creator-scoped platform capabilities
```

The Creator Engine does not require a database or separate deployment per
Creator. Future Azure services should use Managed Identity and preserve
Creator identity in data access, telemetry, and background messages.

## Non-Goals for the First Iteration

The first Creator Engine iteration does not include:

- User accounts or authentication
- Creator administration UI
- Creator self-service onboarding
- Billing or subscriptions
- Database migration
- Separate deployment per Creator
- Full physical content reorganization
- Arbitrary themes or custom code

## Definition of Done

The Creator Engine foundation is complete when:

- The Simonton Adventures exists as JSON-backed Creator data.
- An approved host resolves to one active Creator.
- Unknown production hosts fail safely.
- Creator Context is immutable and request-scoped.
- Content and Address Engine core operations require Creator identity.
- Hard-coded flagship brand and route assumptions are removed from shared UI.
- Cache keys and future background contracts include Creator identity.
- A synthetic second Creator proves isolation and duplicate-slug behavior.
- Build, tests, and the dev deployment succeed.

## Guiding Principle

Resolve the Creator once. Require Creator identity everywhere ownership matters.
