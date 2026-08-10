# AdventuresCompanion OpenAPI Contract

**Status:** Approved Design Direction

**Last Updated:** August 9, 2026

## Purpose

This document defines the HTTP and JSON contract boundary between
AdventuresSuite and AdventuresCompanion. OpenAPI describes that boundary; it
does not establish identity, ownership, traveler participation, authorization,
or database structure.

The first contract is read-only and traveler-focused. It lets the mobile team,
server team, tests, and feedback prototypes converge before production API
activation.

## Delivery Boundary

```text
Azure SQL
    -> Dapper persistence record
    -> application query projection
    -> current authorization and traveler information policy
    -> Companion DTO
    -> JSON over HTTPS
```

No SQL, Dapper, domain aggregate, identity-provider model, provider credential,
or permanent protected-Resource URL crosses this boundary. OpenAPI schemas are
designed from mobile use cases and approved information policy, never generated
from tables.

## Independent API Host and Deployment

The Companion API does not run inside the Blazor web application and the web
application does not proxy mobile traffic. It runs in a separate ASP.NET Core
host, initially named `AdventuresSuite.Api`, with its own project, process,
Azure App Service, Managed Identity, configuration, health endpoints,
observability identity, deployment artifact, scaling policy, and rollback.

```text
AdventuresCompanion
    -> api-dev.adventuressuite.com
    -> AdventuresSuite.Api
       -> shared application contracts and services
       -> authorization and traveler information policy
       -> Planning and Resource persistence adapters

Creator Workspace browser
    -> workspace host
    -> TheSimontonAdventures.Web
       -> the same approved application contracts and services
```

The hostname shown is the intended naming direction; DNS and production names
require deployment approval. Mobile clients use the approved AdventuresSuite
API origin and never depend permanently on an Azure-generated hostname.

Separation is a deployment boundary, not permission to duplicate business
logic. Domain, application-service, authorization, DTO, and persistence
contracts live in appropriately bounded reusable projects. Both hosts compose
those dependencies, while controllers, Minimal API route definitions, browser
components, cookies, and mobile bearer-token middleware remain host-specific.

The API host:

- accepts OAuth bearer access tokens and never workspace cookies;
- has no Razor, Blazor circuit, public Creator-host, or interactive workspace
  routes;
- receives a least-privilege runtime identity independent from the web host;
- reaches SQL, private Blob Storage, Key Vault, and other dependencies through
  approved private networking;
- scales, deploys, rolls back, rate-limits, and fails independently from the
  web experience;
- exposes separate liveness, readiness, release identity, and safe dependency
  diagnostics; and
- cannot execute migrations or grant itself infrastructure permissions.

This decision does not require one microservice per Engine. The first API host
is a modular application boundary. Further service extraction requires measured
scale, isolation, availability, ownership, or deployment evidence rather than
speculative decomposition.

## Activation Gates

Contract design and deterministic contract tests may begin immediately.
Production endpoint activation requires all of the following:

- a server-authoritative relationship between the authenticated user and the
  Adventure traveler or participant;
- Planning application-service authorization for the requested resource;
- current Creator ownership and traveler information-policy evaluation;
- OAuth access tokens issued for the Companion API with exact issuer, audience,
  lifetime, and scope validation; and
- Resource Engine delivery for every protected document or media operation.

Until those gates exist, sample servers and the POC use fictional,
non-sensitive data and cannot become production authentication fallbacks.

## Version and Discovery

- The first base path is `/api/v1/companion`.
- A breaking wire-contract change requires a new major path.
- Additive optional fields may ship within a major version when older clients
  can ignore them safely.
- Security-sensitive enums are closed. An unknown value fails closed rather
  than granting authority or presenting unsafe instructions.
- The generated contract artifact is retained in CI for every release.
- The proposed document route is `/openapi/companion/v1.json`. Production
  publication is an explicit security decision; interactive API documentation
  is authenticated and non-production by default.

The initial media type is `application/json`. Errors use
`application/problem+json`. Vendor media types are deferred until compatibility
needs justify them.

## .NET OpenAPI and Interactive Documentation

The server uses ASP.NET Core's built-in `Microsoft.AspNetCore.OpenApi` document
generation and emits OpenAPI 3.1. The generated document is the authoritative
machine-readable contract. Scalar is the preferred interactive API reference
for developers because it consumes that standard document without becoming a
second contract source. Swagger UI or another standards-compatible viewer may
be substituted later without changing endpoint contracts.

Every Companion endpoint supplies complete OpenAPI metadata as part of its
route definition:

- stable, unique operation ID;
- Companion tag and concise summary;
- purpose, authorization, freshness, and failure description;
- route, query, header, and body parameter descriptions and constraints;
- request media type and schema for commands;
- success response type, media type, and description;
- every supported problem response and its description; and
- OAuth bearer security requirements and required scopes.

Minimal API endpoints use typed request and response DTOs plus endpoint metadata
such as `WithName`, `WithSummary`, `WithDescription`, `WithTags`, `Accepts`, and
`Produces`. Endpoint-specific operation transformers are used only when normal
metadata cannot express an approved contract detail. The deprecated
`.WithOpenApi()` customization pattern is prohibited on .NET 10.

Illustrative composition—not executable implementation—is:

```csharp
builder.Services.AddOpenApi("companion-v1", options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
});

var companion = app.MapGroup("/api/v1/companion")
    .WithTags("AdventuresCompanion")
    .RequireAuthorization("CompanionApi");

companion.MapGet("/adventures", GetAdventuresAsync)
    .WithName("ListCompanionAdventures")
    .WithSummary("Lists Adventures available to the current traveler")
    .Produces<CompanionAdventureCollectionDto>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status304NotModified)
    .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
    .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests);
```

The exact framework APIs and reviewed package versions are selected in the
implementation increment and locked centrally. XML documentation may enrich
the generated descriptions, but public contract accuracy cannot depend on
comments alone.

The JSON document is generated during build and retained as an immutable CI
artifact. Development may serve it at `/openapi/companion-v1.json` and serve
Scalar at an associated developer route. Interactive documentation is disabled
in production by default. If production contract publication is approved, the
document is read-only and deliberately exposed; interactive execution remains
disabled or separately authenticated and authorized. Scalar never receives
production client secrets, certificates, refresh tokens, or reusable sample
credentials.

## Initial Read Contract

The first implementation increment contains only the smallest traveler-ready
read model:

```text
GET /api/v1/companion/adventures
GET /api/v1/companion/adventures/{adventureId}
GET /api/v1/companion/adventures/{adventureId}/today
GET /api/v1/companion/adventures/{adventureId}/itinerary
GET /api/v1/companion/adventures/{adventureId}/readiness
GET /api/v1/companion/adventures/{adventureId}/playbook
GET /api/v1/companion/resources/{resourceId}/content
```

The list response contains current, committed, and planned Adventures visible
to the authenticated traveler, including safe countdown inputs. Detail, Today,
itinerary, readiness, and Playbook projections are separate because they have
different minimization, cache, freshness, and audit requirements.

Map, offline-package, notification-center, device-registration, poll,
acknowledgment, task-completion, calendar, breadcrumb, and media-capture
operations are later additive increments. Their illustrative routes in
`companion-api-sync.md` do not authorize implementation before their owning
domain and security gates pass.

## Common Response Metadata

Every top-level projection declares:

- `schemaVersion`: wire-schema version understood by the client;
- `projectionVersion`: opaque version of the authorized server projection;
- `generatedAtUtc`: RFC 3339 UTC timestamp ending in `Z`;
- `freshUntilUtc`: time after which the client must visibly treat the
  projection as stale;
- `syncCursor`: optional opaque continuation or synchronization cursor; and
- `supportId`: server-generated identifier used only for support correlation.

HTTP `ETag` represents the authorized response variant. Clients send
`If-None-Match`; a `304` response avoids retransmitting an unchanged projection.
An ETag, cursor, cached Creator identity, route identifier, or device identity
never proves current access.

Responses containing protected or traveler-specific data use private cache
directives appropriate to their classification. Shared intermediary caching is
disabled unless a separately reviewed public contract permits it.

## Wire Formats

| Concept | JSON representation |
| --- | --- |
| Opaque identity | bounded case-sensitive string; clients do not parse it |
| Travel date | `YYYY-MM-DD` |
| Local time | `HH:mm:ss` |
| Time zone | IANA identifier such as `Europe/Rome` |
| Authoritative instant | RFC 3339 UTC timestamp ending in `Z` |
| Duration | integer seconds unless a field states another unit |
| Money | decimal string plus ISO 4217 currency code |
| Distance | decimal number plus explicit unit |
| Enum | documented string value, never an integer ordinal |
| Optional value | omitted or explicitly nullable as defined by its schema |

Travel dates and local times are not silently converted through the device time
zone. Countdown responses provide authoritative date/time-zone inputs and a
server evaluation time; the client may animate display ticks without persisting
them as facts.

## Collection and Synchronization Rules

- Collections have deterministic ordering.
- Potentially large collections use a bounded `limit` and opaque
  `continuationToken`; clients cannot construct or interpret the token.
- Timestamp-based `updatedSince` synchronization is not used as an authority or
  ordering mechanism.
- Expired, malformed, replayed for another user, or scope-mismatched cursors
  return a safe resynchronization outcome.
- Incremental synchronization represents additions, replacements, deletions,
  and revocation tombstones explicitly.
- A full resync is always available when schema, authorization, or cursor state
  makes incremental recovery unsafe.

## Authentication and Authorization

Companion uses browser-delegated OAuth authorization-code flow with PKCE and
sends access tokens as bearer credentials. The mobile app contains no client
secret or certificate. Workspace cookies are not accepted by the Companion API.

For every operation, the server:

1. validates the token issuer, audience, signature, lifetime, and required API
   scope;
2. maps the external identity to the current platform `UserId`;
3. loads current user, participation, Creator, resource, and revocation facts;
4. verifies authoritative Creator ownership below the endpoint;
5. evaluates the operation-specific policy and traveler information profile;
6. constructs only the allowed DTO; and
7. records required audit intent or bounded security telemetry.

The API does not accept a Creator ID as proof of tenancy. Inaccessible resource
identifiers return an enumeration-safe not-found response where disclosure
matters. A valid token lacking a general API capability may receive `403`, but
the response never confirms another Creator's resource, title, status, or
traveler relationship.

## Safe Errors

Problems contain only allowlisted fields:

```json
{
  "type": "https://errors.adventuressuite.example/problems/resource-unavailable",
  "title": "The requested resource is unavailable.",
  "status": 404,
  "code": "resource_unavailable",
  "supportId": "req_opaque",
  "retryable": false
}
```

The production error namespace is selected before implementation. Problem
responses never include exception text, stack traces, SQL details, tokens,
claims, raw URLs, query strings, Creator content, or protected identifiers.
Expected categories cover invalid input, unauthenticated access, denied or
unavailable resources, concurrency conflicts, expired projections, rate
limits, and temporary dependency unavailability.

## Commands and Concurrency

Later mutation endpoints use explicit commands rather than generic JSON Patch.
Retryable commands require a bounded idempotency key scoped to actor, Creator,
resource, and operation. Updates require an expected resource version through
`If-Match` or an explicit version field. A protected mutation and required audit
intent commit atomically.

Command bodies never submit ownership, membership, entitlement, approval, or
authorization facts. The server derives those facts authoritatively.

## Media, Documents, and Offline Packages

Ordinary JSON never embeds large binary data as base64. A response contains an
opaque Resource identity, safe display metadata, checksum when appropriate,
classification-aware availability, and an API delivery operation. The delivery
operation reauthorizes the current user and enforces malware state, rights,
expiry, range, retention, and audit policy.

If a provider URL is ever returned, it is HTTPS, narrowly scoped, short-lived,
non-authoritative, excluded from logs, and unsuitable for permanent storage.
Offline packages use an integrity-protected manifest and encrypted local cache;
they are not database replicas and cannot update Planning state.

## Notifications

Push payloads contain only an opaque notification identity, safe category, and
allowlisted deep-link intent. They do not contain itinerary details, names,
confirmation references, precise location, credentials, authorization claims,
or protected URLs. The app retrieves the current authorized JSON after
activation. Delayed, duplicated, reordered, stale, or forged pushes cannot
change authoritative state.

## Observability and Audit

Metrics use route templates, status categories, and bounded operation names.
They never use User, Creator, Adventure, traveler, or Resource identities as
metric dimensions. Logs and traces do not record bearer tokens, headers,
request or response bodies, query values, signed URLs, itinerary content,
documents, private notes, or precise location.

The response `supportId` is generated by the server. Inbound trace context is
untrusted diagnostic input and never becomes identity, authorization,
idempotency, uniqueness, or audit evidence. Protected reads, downloads,
exports, device registration, and mutations follow the audit classifications
of their owning capability.

## OpenAPI Quality Gates

The OpenAPI artifact must pass:

- deterministic generation and linting;
- unique stable operation identifiers;
- schema and example validation;
- breaking-change comparison against the previous released contract;
- generated-client compile tests;
- consumer contract tests for the MAUI client;
- response projection tests proving prohibited fields cannot serialize;
- anonymous, wrong-issuer, wrong-audience, expired-token, and revoked-user
  tests;
- cross-Creator, cross-traveler, IDOR, enumeration, and stale-authorization
  tests;
- malformed identity, cursor, pagination, content-type, and oversized-input
  tests;
- ETag, `304`, expiry, tombstone, conflict, retry, and full-resync tests; and
- telemetry, audit, error, URL, and protected-media leakage tests.

Examples use fictional data and never real booking references, traveler data,
tokens, signed URLs, or private Adventures.

## Incremental Delivery

1. **Contract vocabulary:** approve schemas, errors, formats, operation IDs,
   examples, and compatibility policy.
2. **Read-only contract:** generate the OpenAPI artifact and deterministic
   client against fictional projections.
3. **Authorized server projection:** implement application queries after
   traveler participation and Planning authorization gates pass.
4. **Protected delivery and offline sync:** add Resource operations, manifests,
   tombstones, integrity, and encrypted-device behavior.
5. **Commands:** add device registration, acknowledgments, polls, and task
   completion with idempotency, concurrency, authorization, and atomic audit.
6. **Notifications and advanced mobile capabilities:** add push, maps,
   calendars, breadcrumbs, and capture only through their approved boundaries.

Each increment is separately reviewable, testable, deployable, and reversible.
