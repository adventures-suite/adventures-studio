# AdventuresCompanion Implementation Plan

**Status:** Approved Future Incremental Delivery

**Last Updated:** August 8, 2026

## Objective

Deliver the first AdventuresSuite iOS and Android application as an offline-aware
travel Companion using .NET MAUI Blazor Hybrid, without interrupting the current
Identity and Planning implementation sequence.

Read first:

- `AGENTS.md`
- `docs/architecture/adventures-companion.md`
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

## M0: Product, Privacy, and Threat Model

Define supported traveler scenarios, minimum OS versions, accessibility,
offline expectations, data classification, consent language, location abuse
cases, retention, deletion, App Store/Play disclosures, and measurable battery
budgets.

Exit gate: product, architecture, security, privacy, legal, accessibility, and
mobile-store review approve the initial boundary.

## M1: Versioned Mobile API Foundation

Add provider-neutral API contracts, versioning, OAuth-protected mobile access,
server-side Creator/resource authorization, idempotency, safe errors, rate
limits, and API observability. Do not expose database or provider models.

Exit gate: anonymous, token, audience, Creator-isolation, IDOR, replay,
enumeration, revocation, compatibility, and prohibited-data tests pass. A
Planning `Traveler` record, display name, email, device identity, or plan link
cannot establish account binding or authorization.

## M2: MAUI Blazor Hybrid Shell

Create focused mobile, shared Razor-class-library, platform-adapter, and test
projects. Add navigation, design tokens, accessibility foundations, environment
separation, secure configuration, deep-link policy, and deterministic device
fakes.

Exit gate: supported iOS and Android builds pass; shared components remain
host-independent; production cannot activate development or test adapters.

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
essential references. Add explicit conflict and stale-data presentation.

Exit gate: airplane-mode, partial sync, interrupted retry, cross-Creator,
revocation, local clearing, time-zone transition, schema upgrade, and storage
corruption tests pass.

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

Exit gate: unauthorized upload, expired authorization, malicious media,
interrupted upload, duplicate registration, storage isolation, EXIF privacy,
quota, and deletion tests pass.

## M7: Notifications and Travel Readiness

Add provider-neutral push registration and privacy-safe plan-change
notifications. Complete offline readiness, accessibility, performance, battery,
support, incident, store-distribution, signing, and staged-release procedures.

Exit gate: notification payloads contain no private detail; registrations are
revocable and environment-isolated; production runbooks and phased rollout are
approved.

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
