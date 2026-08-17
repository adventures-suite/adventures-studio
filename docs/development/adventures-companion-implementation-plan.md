# AdventuresCompanion Implementation Plan

**Status:** Approved Future Incremental Delivery

**Last Updated:** August 10, 2026

## Objective

Deliver the first AdventuresSuite iOS and Android application as an offline-aware
travel Companion using .NET MAUI Blazor Hybrid, without interrupting the current
Identity and Planning implementation sequence.

Read first:

- `AGENTS.md`
- `docs/architecture/adventures-companion.md`
- `docs/architecture/companion-api-sync.md`
- `docs/architecture/companion-openapi.md`
- `docs/architecture/companion-api-v1-contract.md`
- `docs/development/companion-api-v1-implementation-baseline.md`
- `docs/product/adventures-companion.md`
- `docs/architecture/planning-engine.md`
- `docs/architecture/resource-engine.md`
- `docs/architecture/identity-authorization.md`
- `docs/architecture/security.md`
- `docs/architecture/observability.md`
- `docs/architecture/audit-reporting.md`

## Sequencing Rule

This plan records the direction now. Do not add MAUI projects, mobile identity
registration, location permissions, mobile APIs, notification providers, or
breadcrumb persistence during the current Phase 3 browser-security work.

## Current implementation status

The isolated MAUI POC remains under
`prototypes/AdventuresSuite.Companion.Poc` as a preserved experience reference.
The production transition has begun in
`src/AdventuresSuite.Companion.Mobile`, carrying forward the proven shell,
appearance behavior, Adventure selection, explicit Demo/API composition, and
API-backed Adventure list, detail, Today, and Journey/Itinerary presentation.

The Journey vertical reads the versioned authorized Itinerary contract through
an explicit provider-neutral application query and typed mobile client. Its
deterministic adapter remains fictional and test-only; authoritative SQL,
mobile authentication, offline persistence, and itinerary mutation remain
inactive follow-on work.

The manual TestFlight workflow builds the production mobile project and keeps
the existing application bundle identity. Demo publication remains an explicit
internal-beta configuration; it is not authoritative Planning data or a
fallback for API failures. API publication remains fail closed until the mobile
PKCE and authoritative traveler-access gates are complete.

New Companion product slices should be implemented in the production mobile
project. The POC is not extended or referenced by production projects.

Physical-device acceptance of the initial production shell passed on iPhone.
Follow-up iPad acceptance identified that the first tablet breakpoint retained
the phone-width shell instead of adapting to the available canvas. The focused
tablet layout correction expands the bounded shell and navigation at iPad
widths, preserves readable content limits, introduces a landscape home
composition, and retains the single-column phone experience. Portrait,
landscape, Split View, larger text, light, and dark device checks remain part of
the release acceptance gate.

Android emulator acceptance also identified that the .NET MAUI 10 edge-to-edge
page default placed the WebView header beneath the system status bar, leaving
the visible appearance selector outside the interactive safe area. The
production page now explicitly respects container safe areas so header controls
remain visible and operable without device-specific padding.

## M0: Product, Privacy, and Threat Model

Define supported traveler scenarios, minimum OS versions, accessibility,
offline expectations, data classification, consent language, location abuse
cases, retention, deletion, App Store/Play disclosures, and measurable battery
budgets.

Exit gate: product, architecture, security, privacy, legal, accessibility, and
mobile-store review approve the initial boundary.

## M1: Versioned Mobile API Foundation

Create a separate `AdventuresSuite.Api` ASP.NET Core host and deployable artifact;
do not place Companion endpoints in or proxy them through the Blazor web host.
Give the API its own OAuth bearer pipeline, configuration, health, telemetry,
runtime identity, deployment, rollback, and independent scaling boundary while
reusing approved domain, application, authorization, and persistence libraries.

Create cohesive contract boundaries rather than a universal Common project.
Use a Companion contract project for API DTOs and JSON metadata, server-only
application projects for authorized projections, and a host-independent Razor
class library only for presentation that is genuinely shared by Web and MAUI.
Companion must not reference server application, domain, authorization,
persistence, ASP.NET, Azure, SQL, Dapper, or identity-provider projects.

Add provider-neutral API contracts, versioning, OAuth-protected mobile access,
server-side Creator/resource authorization, idempotency, safe errors, rate
limits, and API observability. Implement the server boundary as Dapper
persistence records to application query projection to authorization and
traveler information policy to purpose-built mobile DTO to JSON. Do not expose
database, Dapper, domain aggregate, or provider models.

Define JSON wire formats, compatibility, `ETag`/conditional requests, opaque
sync cursors, pagination, safe problem categories, short-lived protected-media
delivery, and contract tests independently from SQL migrations.

Follow `docs/architecture/companion-openapi.md` as the authoritative HTTP and
JSON contract direction. Contract design and fictional deterministic clients
may proceed before endpoint activation. Production reads remain gated on an
authoritative user-to-traveler participation relationship, Planning
application-service authorization, mobile OAuth access-token validation, and
protected Resource delivery.

Use ASP.NET Core's built-in OpenAPI 3.1 generation and complete endpoint
metadata for every route. Retain the generated contract as a CI artifact and
use Scalar as the development-only interactive API reference. Do not use the
deprecated `.WithOpenApi()` customization pattern or make an interactive UI the
contract source.

Generate or verify the MAUI client from the retained OpenAPI artifact. A shared
contract assembly may improve compile-time reuse, but it must not bypass
OpenAPI compatibility checks or become a channel for server-only types.

Implement the initial endpoint matrix, DTO fields, safe problems, examples, and
deferrals in `docs/architecture/companion-api-v1-contract.md`. Treat required
v1 field names, types, meanings, and nullability as compatibility commitments;
extend v1 additively and use a new major version for breaking changes.

Follow `docs/development/companion-api-v1-implementation-baseline.md` for the
approved project graph, status projection, contract bounds, provisional OAuth
names, fictional fixtures, client generation, JSON source generation, and
deterministic test-host composition.

Use explicit hand-written mappings from Dapper records to validated application
projections and from authorized projections to Companion DTOs. Contract tests
must prove that adding persistence or domain properties cannot automatically
change serialized JSON or the generated OpenAPI document. Do not introduce
reflection-based or convention-based mapping across these boundaries.

Exit gate: anonymous, token, audience, Creator-isolation, IDOR, replay,
enumeration, revocation, compatibility, and prohibited-data tests pass. A
Planning `Traveler` record, display name, email, device identity, or plan link
cannot establish account binding or authorization.

## M2: MAUI Blazor Hybrid Shell

Create focused mobile, shared Razor-class-library, platform-adapter, and test
projects. Add navigation, design tokens, accessibility foundations, environment
separation, secure configuration, deep-link policy, and deterministic device
fakes.

Implement an injected appearance-preference service with `System`, `Light`, and
`Dark` choices. Default to System, persist only explicit user preference, react
to live OS appearance changes while System is active, and expose one effective
Light/Dark palette to shared and native presentation adapters. Resolve and
apply the effective palette before the first interactive frame. Do not encode
appearance in API DTOs, server profiles, authentication storage, or sync state.

Define shared semantic tokens for surfaces, text, borders, accents, focus,
status, scrims, elevation, maps, and interactive states. Require all pages,
navigation, dialogs, cards, controls, loading/error/empty states, native chrome,
maps, and overlays to consume those roles. Literal palette colors are confined
to the reviewed token definitions and platform bootstrap resources.

Required automated and device acceptance tests prove:

- a first launch uses System and matches both initial OS palettes;
- a live OS Light/Dark change updates every visible shared and native surface
  while System is selected;
- explicit Light and Dark choices survive termination and relaunch and do not
  follow later OS changes;
- returning to System immediately adopts the current OS palette and resumes
  live observation;
- cold start, warm start, resume, navigation, dialog opening, and theme change
  show no wrong-theme flash or mixed native/Blazor palette;
- every enumerated surface and component state uses semantic tokens, including
  maps, overlays, focus, selected, disabled, loading, error, and empty states;
- applicable WCAG contrast thresholds pass for text, meaningful graphics,
  controls, and focus indicators in both effective palettes; and
- missing, corrupt, or unsupported stored preferences fail safely to System.

Exit gate: supported iOS and Android builds pass; shared components remain
host-independent; production cannot activate development or test adapters; and
the appearance, startup-flash, semantic-token, contrast, persistence, and live
OS-change tests above pass on supported platform versions.

## M3: Mobile Authentication and Device Registration

Implement system-browser External ID authorization-code flow with PKCE,
platform secure storage, token refresh/revocation, logout and local-data clearing,
installation identity, and lost-device response. The mobile client contains no
secret or confidential-client certificate.

Exit gate: sign-in, cancellation, replay, malicious deep link, token theft,
revocation, clock, offline, reinstall, backup/restore, and log-redaction tests
pass on both platforms.

## M4: Offline Companion Projection

Implement encrypted, Creator-partitioned local storage and versioned sync for
today/upcoming itinerary, time-zone context, reservations, tasks, maps, and
essential references. Include an authorized, minimized Travel Playbook package
with explicit section/Resource selection, version, retention, expiration, and
stale-data presentation.

Use versioned JSON manifests plus explicitly authorized encrypted media. Add
incremental additions, replacements, deletion/revocation tombstones, full-resync
fallback, integrity checks, schema migration, expiration, and local clearing.

Exit gate: airplane-mode, partial sync, interrupted retry, cross-Creator,
revocation, local clearing, time-zone transition, schema upgrade, and storage
corruption tests pass.

## M4A: Device Calendar Integration

Add explicit traveler-controlled calendar export through iOS and Android
platform adapters. Request OS calendar permission just in time; allow item,
target-calendar, and reminder selection; preserve stable event identity,
destination-local time zones, updates, and cancellation; and retain a useful
reduced-capability experience when permission is denied.

Exit gate: consent, denial, revocation, duplicate retry, stale update,
cancellation, time-zone transition, shared-calendar leakage, prohibited-data,
and cross-traveler tests pass on both platforms. Device-calendar edits cannot
silently mutate authoritative Planning state.

## M5: Traveler-Controlled GPS Breadcrumbs

Add foreground capture first. Implement just-in-time education, OS permission,
platform consent evidence, explicit start/pause/resume/stop, visible capture
state, policy-controlled sampling, encrypted local queue, idempotent sync,
private trail review, eligible deletion, and battery measurement.

Background capture is a separate sub-gate. It requires demonstrated user value,
separate permission and disclosure, persistent visibility where supported,
store-policy review, and measured battery impact.

Exit gate: denial and limited-permission behavior, consent revocation, OS
revocation, spoofed/stale observations, clock changes, offline overflow,
duplicate sync, cross-Creator access, stalking/abuse cases, telemetry leakage,
retention, deletion, and battery budgets pass on physical iOS and Android
devices.

## M6: Memory and Resource Capture

Add protected camera/library staging, rights and accessibility metadata,
short-lived direct Blob upload authorization, resumable upload, Resource Engine
registration, optional breadcrumb association, and EXIF-location review.

Implement the Memories Adventure selector as an accessible shared component
with an explicit transient-overlay state. It closes after an Adventure is
selected, when the selector is activated again, on outside pointer activation,
on Escape where keyboards are supported, and on the applicable platform Back
action. All paths use the same idempotent dismissal operation, set the exposed
expanded state to false, remove overlay hit testing and event/Back handlers,
and restore focus to the selector when appropriate. Selection is committed
before dismissal; every other dismissal preserves the prior selection and any
in-progress memory input. Outside activation must not also activate the covered
page control.

Required component, integration, accessibility, and device tests prove:

- each dismissal input closes an open dropdown and selector reactivation
  toggles it closed;
- selection changes exactly once and the dropdown then closes;
- outside, Escape, and Back dismissal do not change the selection or navigate;
- Escape and Back retain their normal behavior while the dropdown is closed;
- the selector exposes its accessible label, value, expanded state, options,
  selected option, and deterministic focus order;
- focus returns to the selector after dismissal when the platform has a focus
  concept, including keyboard and screen-reader operation;
- rapid repeated dismissal, page navigation, component disposal, rotation,
  app background/resume, and appearance changes leave no overlay, stale event
  subscription, duplicate Back handler, or blocked page hit target; and
- the selector and its scrim, focus, hover/pressed, selected, disabled, loading,
  error, and empty states pass in System, Light, and Dark appearance modes.

Exit gate: unauthorized upload, expired authorization, malicious media,
interrupted upload, duplicate registration, storage isolation, EXIF privacy,
quota, and deletion tests pass; all Memories selector dismissal, accessibility,
focus-restoration, cleanup, and appearance tests above also pass on supported
iOS and Android versions and keyboard-capable targets.

## M7: Notifications and Travel Readiness

Add provider-neutral push registration and privacy-safe plan-change
notifications plus a server-backed in-app notification center. Push signals
that data changed; Companion must fetch the current authorized JSON rather than
treating a payload as state. Complete offline readiness, accessibility,
performance, battery, support, incident, store-distribution, signing, and
staged-release procedures.

Separate critical operational, action-required, informational, collaboration,
audience, and promotional policies. Apply category/channel preferences,
time-zone-aware quiet hours, digesting, deduplication, supersession,
rate-limiting, expiry, safe lock-screen previews, and retry/suppression rules.

Include derived countdowns for Planned, Upcoming, and approved committed
Adventures; date-only and authoritative time-zone semantics; traveler-specific
readiness and Today and Next projections; material-change acknowledgment;
action-required workflows; approved contingencies; bounded smart reminders;
and visible offline freshness and reconciliation state.

Do not persist countdown ticks or treat notification delivery as
acknowledgment. Follow
`docs/architecture/adventure-readiness-and-change-management.md`.

Exit gate: notification payloads contain no private detail; registrations are
revocable and environment-isolated; duplicates, reordering, stale pushes,
provider outage, token rotation, quiet hours, digests, supersession, deep links,
and cross-traveler access are tested; production runbooks and phased rollout
are approved.

## M8: Preserve and Publish Breadcrumbs

Add an explicit Creator workflow that transforms selected private trails into
privacy-reduced publication Resources. Support trimming, simplification,
precision reduction, sensitive-zone removal, time removal, preview, approval,
audit, and withdrawal behavior.

Exit gate: raw trails cannot be published directly; private points remain
protected; publication and notification are transactional and reviewable.

## Explicitly Deferred

- full mobile Creator Workspace parity;
- continuous or covert tracking;
- remotely enabling another traveler's location;
- family surveillance or employee monitoring;
- emergency, rescue, or guaranteed live-location services;
- unrestricted collaborator access to precise location;
- location-based advertising or sale of location data;
- AI control of consent, tracking, sharing, or publication; and
- provider-specific mobile services in core contracts.
# Immediate operational priority: private ephemeral migration runner

Before Companion SQL binding or activation, complete the source-controlled
one-job ephemeral Azure VM runner design review, then obtain separate approvals
for repository implementation, runner infrastructure/cleanup, exact SQL
permissions, and one reviewed migration—in that order. The Container Apps/ACR
path is superseded.
