# Companion API v1 Implementation Baseline

**Status:** Approved for Deterministic Foundation Implementation

**Last Updated:** August 9, 2026

## Purpose

This document resolves the initial implementation choices needed to turn the
approved Companion API architecture and v1 contract into an executable,
fictional-data foundation. It does not authorize production data, Azure API
deployment, SQL queries, protected Resource delivery, or live External ID
activation.

Read `docs/architecture/companion-openapi.md` and
`docs/architecture/companion-api-v1-contract.md` first.

## 1. Project Layout

The initial solution boundary is:

```text
src/
  AdventuresSuite.Api/
  AdventuresSuite.Companion.Contracts/
  AdventuresSuite.Companion.Application/
  AdventuresSuite.Companion.SqlServer/          # created only with real SQL work

tests/
  AdventuresSuite.Api.Tests/
  AdventuresSuite.Companion.Contracts.Tests/
```

Future production MAUI work may add
`AdventuresSuite.Companion.Client` for generated transport code and a separate
hand-written mobile service wrapper. The existing POC does not become a server
dependency.

Responsibilities:

- `AdventuresSuite.Api` owns HTTP composition, bearer authentication, routing,
  OpenAPI generation, safe errors, rate limiting, health, and host telemetry.
- `AdventuresSuite.Companion.Contracts` owns request/response DTOs, API enums,
  wire constants, and the JSON source-generation context.
- `AdventuresSuite.Companion.Application` owns provider-neutral query
  interfaces, authorized projection contracts, explicit DTO mapping, and
  deterministic fictional providers.
- `AdventuresSuite.Companion.SqlServer`, when authorized later, owns Dapper
  records, SQL text, repository implementation, and explicit row-to-projection
  mapping.

The API project may reference Contracts and Application. Application may
reference Contracts and approved server identity/authorization abstractions.
Contracts references no application, domain, persistence, ASP.NET, Azure,
Entra, Dapper, or SQL project. No project cycle is permitted.

Do not create `AdventuresSuite.Common`. Create the smaller
`AdventuresSuite.Contracts` project only when at least two genuine
cross-process contract families need the same stable primitive; do not create
it speculatively in the first increment.

## 2. Companion Presentation Status

The initial Companion status is a presentation projection and does not replace
or write back the Planning lifecycle.

| Planning lifecycle | Companion status | List behavior |
| --- | --- | --- |
| `Idea` | not projected | hidden |
| `Draft` | `planned` | visible only when traveler access and product policy permit drafts |
| `Planned` | `planned` | visible |
| `Upcoming` | `committed` | visible |
| `InProgress` | `inProgress` | visible and prioritized |
| `Completed` | `completed` | visible under completed/history behavior |
| `Archived` | not projected | unavailable from normal Companion reads |

For the deterministic foundation, fixtures use `Planned`, `Upcoming`,
`InProgress`, and `Completed`. Draft visibility remains false. An unknown or
unsupported lifecycle fails closed and is not serialized as a guessed status.
Archived recovery remains a Creator Workspace capability.

## 3. Initial Contract Bounds

These are alpha safety limits and explicit OpenAPI constraints. Tightening a
limit below a previously supported value requires compatibility review.

| Value | Initial limit |
| --- | --- |
| Opaque identity | 1–128 ASCII characters; `^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$` |
| Schema/projection version | 1–64 characters |
| Title | 1–200 Unicode scalar values after normalized validation |
| Subtitle | 0–300 Unicode scalar values |
| Safe summary/description | 0–2,000 Unicode scalar values |
| Alternative text | 1–500 Unicode scalar values when an image requires it |
| Safe notice/action label | 0–300 / 1–100 Unicode scalar values |
| Relative capability/content path | 1–2,048 characters; same-origin HTTPS resolution only |
| Continuation or sync cursor | 1–2,048 opaque ASCII characters |
| Page size | default 20; minimum 1; maximum 100 |
| Adventures returned per page | maximum 100 |
| Destination visits per Adventure projection | maximum 100 |
| Itinerary days per response | maximum 180 |
| Schedule items per day | maximum 250 |
| Playbook sections | maximum 50 |
| Entries per Playbook section | maximum 500 |
| Resource summaries per JSON projection | maximum 500 |
| Validation errors in one problem | maximum 50 properties and 5 codes per property |
| Ordinary JSON response body | maximum 2 MiB before transport compression |
| Server support ID | 1–128 safe ASCII characters |

Binary Resources are not subject to the ordinary JSON limit. Their type-specific
size, range, offline, and retention limits are defined before protected delivery
activation. Input is rejected before unbounded allocation where practical.

## 4. Provisional OAuth and Policy Names

Code and OpenAPI use stable symbolic names while environment-specific Entra
identifiers remain configuration:

| Concept | Initial value |
| --- | --- |
| OpenAPI security scheme | `companionOAuth` |
| Delegated scope name | `Companion.Access` |
| ASP.NET authorization policy | `CompanionApiAccess` |
| API audience configuration key | `Authentication:CompanionApi:Audience` |
| Issuer configuration key | `Authentication:CompanionApi:Issuer` |
| OAuth authorization URL key | `Authentication:CompanionApi:AuthorizationUrl` |
| OAuth token URL key | `Authentication:CompanionApi:TokenUrl` |

The intended Entra application-ID URI direction is
`api://adventures-suite-api`, subject to uniqueness and registration approval.
No code assumes that literal value. Exact issuer, audience, endpoints, tenant,
client identifiers, and redirects are environment configuration validated at
startup.

The deterministic foundation uses a test-only authentication scheme that
cannot activate outside Test. Development may use a separately explicit
development adapter. Neither becomes a production fallback.

## 5. Deterministic Fictional Dataset

The foundation provider uses fixed identities, a fixed `TimeProvider`, and no
real personal or booking data. It includes:

1. **Current Adventure:** a fictional Italy journey in `InProgress` state with
   Rome and Florence, `Europe/Rome`, Today and Next items, an all-day item, a
   timed rail segment, a changed activity, readiness attention, and a structured
   Playbook.
2. **Planned Adventure:** a fictional Spain and trans-Atlantic journey in
   `Planned` state with `Europe/Madrid`, multiple destinations, countdown data,
   a to-be-confirmed activity, and an available hero Resource.
3. **Committed Adventure:** a fictional future domestic journey in `Upcoming`
   state using `America/Phoenix` and `America/Los_Angeles` to prove multi-zone
   behavior and an explicitly cancelled item.
4. **Completed Adventure:** available only to the completed/history fixture
   query so normal prioritization can be tested.
5. **Isolation fixtures:** a second fictional Creator, another traveler, an
   unknown Adventure, a revoked participation, and expired/blocked/revoked
   Resource summaries for negative tests.

Fixture payloads contain no confirmation numbers, credentials, email addresses,
real names, private notes, payment data, passport/medical data, signed URLs, or
precise live locations. Fixture files are test/demo inputs, not production
fallbacks and not authoritative Planning state.

## 6. Generated Client Strategy

OpenAPI 3.1 generated by `AdventuresSuite.Api` is the source for client
generation. The initial .NET generator direction is NSwag's C# client generator,
configured to use `System.Text.Json`; its exact reviewed and centrally locked
package version is selected in the implementation PR.

Generated transport code:

- is deterministic from the retained OpenAPI artifact and locked settings;
- contains no business, authorization, retry, offline, secure-storage, or UI
  logic;
- accepts an injected `HttpClient` and does not embed an environment base URL;
- is wrapped by a hand-written `ICompanionApiClient`-style mobile service;
- never logs tokens or bodies; and
- is regenerated or verified in CI, with unexplained contract differences
  failing the build.

The implementation PR must prove the generated client compiles against the
supported .NET target. A generator limitation must be resolved through an
approved contract-compatible setting or a reviewed generator change, not a
hand-edited generated file. Generated source commit policy is selected in that
PR based on deterministic CI and developer ergonomics; the OpenAPI artifact is
retained regardless.

## 7. JSON Source Generation

`AdventuresSuite.Companion.Contracts` defines an explicit
`CompanionJsonSerializerContext` covering every request, response, nested DTO,
enum, provider-neutral safe-problem DTO, and collection root in v1. The
contracts project does not reference ASP.NET `ProblemDetails`; the API host maps
the safe problem contract to the required HTTP representation. API JSON uses
`System.Text.Json` with:

- camel-case property names;
- strict number handling;
- explicit UTC, `DateOnly`, `TimeOnly`, money, and closed-enum behavior;
- deterministic null omission/inclusion according to the OpenAPI schema;
- bounded maximum depth; and
- no polymorphic type metadata unless a separately approved contract requires
  it.

JSON source generation is serialization metadata, not object mapping. DTOs are
still created through the explicit mapping rule. Tests compare actual JSON,
source-generated metadata, and OpenAPI schemas for names, types, requiredness,
nullability, enum values, and prohibited fields.

## 8. Deterministic API Test Host

The first API host is executable without SQL, Azure, External ID, Key Vault,
Blob Storage, public DNS, or network authentication. Tests compose:

- a test-only deterministic authentication handler;
- fixed human, traveler, Creator, participation, and information-profile facts;
- a fixed `TimeProvider`;
- the fictional projection provider;
- deterministic ETag, cursor, version, and support-ID providers;
- an in-memory required-audit-intent collector where an endpoint classification
  requires it; and
- failure adapters for unavailable, revoked, stale, rate-limited, and malformed
  scenarios.

The test host cannot activate in Production and fails startup if selected by
production configuration. It binds only through the normal ASP.NET test server
for automated tests unless a developer explicitly starts the API locally.

Required foundation tests cover all documented success schemas plus anonymous,
wrong-scope, unsupported status, IDOR, cross-Creator, cross-traveler, revoked,
not-found, ETag/304, bounds, safe-problem, redaction, source-generation,
OpenAPI, Scalar-environment, and prohibited-dependency behavior.

## Foundation Exit Gate

- The project dependency graph follows this document and is acyclic.
- Release build and complete bounded tests pass with zero warnings.
- All seven read operations appear in generated OpenAPI with complete metadata.
- OpenAPI is retained and passes lint, schema, example, and compatibility tests.
- Scalar is available in Development/Test only.
- The deterministic generated client compiles.
- Explicit mapping and JSON source-generation tests pass.
- No API project depends on the Blazor web host or MAUI POC.
- No SQL, Azure, live provider, production identity, or protected bytes are
  required or enabled.
- Production configuration cannot activate deterministic adapters.
