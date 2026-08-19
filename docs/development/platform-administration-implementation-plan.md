# Platform Administration Portal Implementation Plan

**Status:** Approved Direction; Implementation Deferred

**Last Updated:** August 18, 2026

## Objective

Deliver a secure administrative experience without converting platform
responsibility into universal access to private Creator data.

Read first:

- `docs/architecture/platform-administration-portal.md`
- `docs/product/platform-administration-portal.md`
- `docs/architecture/identity-authorization.md`
- `docs/architecture/security.md`
- `docs/architecture/audit-reporting.md`
- `docs/development/audit-reporting-implementation-plan.md`
- `docs/architecture/observability.md`

## Sequencing Rule

Implement administrative capabilities as narrow vertical slices. Begin with a
read-only, non-content operations dashboard. Do not combine Creator
administration, security evidence, support elevation, and platform operations
into one initial release.

## AP1: Authority and Threat Model

Define administrative actor types, stable permissions, host boundary, session
policy, step-up authentication, reason categories, separation of duties,
emergency-access policy, prohibited data, and negative authorization matrix.

Exit gate: no platform permission implies Creator membership or customer-plan
access; forged routes, claims, cookies, and identifiers fail closed below the
UI.

## AP2: Read-Only Platform Operations Foundation

Add the dedicated administrative surface and provider-neutral query contracts
for release identity, service health, schema/migration compatibility,
integration health, failed-work summaries, aggregate counts, and capability
rollout status.

Use minimal purpose-built projections. Do not query private plan content or
expose raw logs, exceptions, URLs, SQL parameters, or payloads.

Exit gate: authorization, environment isolation, redaction, bounded queries,
freshness, safe failure, accessibility, responsive behavior, and dark-mode
tests pass.

## AP3: Creator Administration

Add one Creator-scoped membership or permission workflow at a time. Require
explicit Creator scope, resource-aware policy, optimistic concurrency,
revocation behavior, and atomic audit intent.

Exit gate: cross-Creator, stale membership, forged identity, privilege
escalation, last-owner, session-revocation, and rollback tests pass.

## AP4: Administrative Audit and Access Review

Add authorized searches over purpose-built audit projections using safe
identifiers, bounded time ranges, result limits, and audited evidence access.
Provide administrative-session and permission review before protected-content
support access.

Exit gate: evidence access is itself audited; prohibited data, enumeration,
bulk extraction, retention, and cross-environment tests pass.

## AP5: Security and Compliance Operations

Add narrowly scoped revocation, retention, deletion, legal-hold, and protected
evidence-export workflows. Require stronger permissions, reauthentication,
reason capture, separation of duties where appropriate, and atomic audit.

Exit gate: approval, expiry, export protection, recovery, legal-hold conflict,
and audit-integrity tests pass.

## AP6: Just-in-Time Support Access

Implement only after a separate architecture and threat-model review. Access
must target one approved Creator or resource, state a reason, require the
appropriate approval, expire automatically, expose no broader permission, and
produce complete audit evidence.

Exit gate: no silent impersonation or standing universal customer-data access
exists; expiry and revocation terminate sessions and live connections; every
read and mutation is reauthorized and audited as policy requires.

## Cross-Cutting Requirements

- Use a separate administrative host or explicitly approved route boundary.
- Keep administrative identity and permissions separate from Creator
  membership, entitlements, and support employment.
- Enforce authorization in application services and data access, not Razor.
- Use purpose-built, minimal, provider-neutral projections.
- Keep logs, audit, analytics, reports, and operational telemetry distinct.
- Bound date ranges, result counts, exports, and background work.
- Require XML documentation for every public API.
- Keep stateful Razor behavior in colocated `.razor.cs` files.
- Include light, dark, system, keyboard, screen-reader, mobile, loading, empty,
  denied, expired, stale, partial, and failure states.
- Add no direct database-editing console.

## Required Verification Per Slice

- ordinary Creator users cannot reach or invoke administrative services;
- each administrative permission grants only its defined operation;
- cross-Creator and cross-environment access fails closed;
- audit and reporting projections contain no prohibited content;
- administrative reads and mutations produce required audit evidence;
- required mutations roll back when audit persistence fails;
- sessions, elevation, exports, and approvals expire and revoke correctly;
- queries and exports resist enumeration and unbounded extraction;
- release build, relevant unit/integration tests, formatting, dependency audit,
  and `git diff --check` pass.

## Explicitly Deferred

- implementation before the Alpha Planner workflow is proven;
- a universal super-admin role;
- unrestricted operational-database queries;
- customer-content search in the operations dashboard;
- standing support impersonation;
- arbitrary report builders;
- a warehouse without measured need; and
- administrative features embedded in Planner or Companion.
