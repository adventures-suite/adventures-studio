# Audit and Reporting Implementation Plan

**Status:** Required Platform Capability; Incremental Delivery

**Last Updated:** August 7, 2026

## Objective

Build trustworthy audit evidence, durable business-event delivery, and secure
Creator-scoped reporting without coupling current Authentication Slice 5A to a
database, event broker, analytics vendor, or reporting UI.

Read first:

- `AGENTS.md`
- `docs/architecture/audit-reporting.md`
- `docs/architecture/observability.md`
- `docs/architecture/identity-authorization.md`
- `docs/architecture/security.md`

## Sequencing Rule

This track is mandatory but incremental. Slice 5A may define only the minimum
provider-neutral audit vocabulary and authentication action classifications it
needs. It must not add audit persistence, analytics infrastructure, broad
shared event contracts, dashboards, or reporting UI.

## AR1: Taxonomy and Shared Contracts

Approve the signal taxonomy, audit envelope, safe classifications, prohibited
data, ownership, retention classes, and schema-versioning rules. Add small
provider-neutral contracts only when an implemented use case requires them.

Exit gate: contracts contain no ASP.NET Core, Entra, Dapper, SQL, Azure Monitor,
analytics-vendor, or UI types; allowlists and negative tests prevent arbitrary
metadata and protected payloads.

## AR2: Append-Oriented Audit and Transactional Guarantees

Add forward-only migrations, least-privilege runtime access, Dapper adapters,
and atomic audit-intent persistence for the first protected use cases. Add a
transactional outbox where synchronous persistence cannot safely complete the
required delivery boundary.

Exit gate: real SQL tests prove atomic success and rollback, append-only runtime
permissions, Creator isolation, concurrency, evidence access auditing, and
recovery behavior.

## AR3: Authentication, Authorization, and Membership Evidence

Produce durable audit records for approved authentication, session, recovery,
membership, authorization, support, archive, and restoration actions. Keep
bounded rejected-attempt security telemetry distinct from durable audit.

Exit gate: the security and audit matrices pass without tokens, claims, contact
data, private resources, or caller-controlled correlation identities.

## AR4: Domain Events and Outbox Dispatch

Introduce versioned Engine-owned events and idempotent asynchronous dispatch
only for implemented workflows such as publication and subscriber notification.
Add retries, poison handling, replay controls, and operational instrumentation.

Exit gate: state and outbox commit atomically; consumers prove deduplication,
Creator scope, ordering assumptions, replay safety, and schema compatibility.

## AR5: Creator-Scoped Reporting Projections

Build the first authorized, rebuildable read models for concrete Creator needs.
Bound queries and exports; keep source-of-truth writes in owning Engines.

Exit gate: IDOR, enumeration, cross-Creator, stale-projection, rebuild,
large-export, expiration, and prohibited-data tests pass.

## AR6: Compliance, Financial, AI, and Platform Reporting

Add narrowly authorized evidence exports, consent and delivery reports,
financial reconciliation, AI lineage and approval summaries, and platform
security reports as their source capabilities become real.

Exit gate: access to evidence is audited; retention, legal hold, privacy
requests, reconciliation, export protection, and recovery are operationally
tested.

## AR7: Analytical Platform When Justified

Evaluate a warehouse, lake, or specialized analytical store only after measured
volume, query complexity, retention economics, or cross-domain needs exceed the
approved SQL projections. Define sanitization, residency, consent, lineage,
access, deletion, cost, and vendor portability before ingestion.

Exit gate: the analytical platform cannot bypass Creator authorization, does
not receive prohibited data, and has tested lifecycle and cost controls.

## Cross-Cutting Release Gate

Every slice requires release build and relevant tests, formatting and diff
validation, schema compatibility checks, Creator-isolation and prohibited-data
tests, documented owners and retention, deployment evidence, and a tested
rollback or rebuild procedure appropriate to the data product.

## Explicitly Deferred

- speculative events for Engines not yet implemented;
- a warehouse or lake without measured need;
- cross-Creator self-service reporting;
- arbitrary report builders over operational tables;
- product analytics without approved purpose and consent review; and
- audit storage in Application Insights or ordinary application logs.
