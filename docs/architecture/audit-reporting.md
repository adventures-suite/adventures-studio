# AdventuresSuite Audit and Reporting Architecture

**Version:** 1.0

**Status:** Platform Must-Have

**Last Updated:** August 7, 2026

## Purpose

Audit and reporting are required platform capabilities for every AdventuresSuite
Engine. The platform must preserve trustworthy evidence of protected activity,
drive durable workflows from domain facts, and provide useful Creator and
platform reporting without weakening Creator isolation or turning operational
databases into unrestricted analytics stores.

This architecture governs security audit, business events, product analytics,
and reporting projections. Operational logs, traces, metrics, health, and alerts
remain governed by `observability.md`.

## Governing Rules

> Transactional systems record authoritative facts. Reporting systems consume
> authorized, purpose-built projections.

> Operational telemetry explains behavior; audit records prove protected
> activity; domain events communicate facts; analytics measures approved use.
> One signal must never silently substitute for another.

Audit and reporting are platform requirements, not optional features to add
after an Engine is complete. Every new Engine must classify its protected
actions, durable business events, reporting needs, prohibited data, retention,
and access model before production enablement.

## Signal and Data Products

| Product | Purpose | Reliability |
| --- | --- | --- |
| Security and compliance audit | Prove protected decisions, sensitive reads, mutations, support access, consent, recovery, publication, and financial actions | Durable and append-oriented |
| Domain and business events | Communicate versioned facts such as publication, notification, order, or plan lifecycle changes | Durable, replayable, and idempotent |
| Product analytics | Measure approved, consent-aware adoption, engagement, and workflow outcomes | Purpose-limited and privacy-minimized |
| Reporting projections | Serve Creator dashboards, exports, reconciliation, and controlled platform reports | Derived and rebuildable |
| Operational observability | Diagnose reliability, performance, and security signals | Best-effort under the observability policy |

A fact may legitimately produce more than one record. For example, publishing
an Adventure can create a protected-mutation audit record, a versioned
`AdventurePublished` business event, aggregate analytics, and operational
telemetry. Each record has its own schema, access, retention, and failure
semantics.

## Audit Model

Audit records use provider-neutral contracts and an allowlisted schema. The
shared envelope supports, when applicable:

- immutable audit event identity and schema version;
- UTC occurrence and durable-record timestamps;
- environment and release identity;
- Creator scope;
- actor type, opaque actor identity, and initiating human actor;
- action, outcome, safe reason category, and security severity;
- target resource type and opaque resource identity;
- previous and resulting version for successful mutations;
- authentication method and opaque session identity when permitted;
- server-generated request/support identity for diagnosis; and
- explicitly classified, redacted metadata fields.

Audit metadata is not an arbitrary dictionary. Contracts define allowed fields
and their classification. Audit identity, ownership, authorization, or
uniqueness never depends on caller-supplied trace context.

Required protected mutations commit the mutation and audit intent atomically in
one transaction or through a transactional outbox. Failure to persist required
audit intent rolls back the mutation. Sensitive reads marked audit-required
fail closed unless an approved policy defines a narrow, bounded fallback.
Rejected attempts use bounded, rate-limited security telemetry where durable
per-attempt audit would create a denial-of-service risk.

The initial audit store may use Azure SQL, but it is logically separate from
operational domain tables. Runtime identities receive append and authorized-read
permissions only; they do not update or hard-delete audit records. Access to
sensitive audit evidence is itself audited.

## Domain Events and Transactional Outbox

Engines publish durable facts only after authoritative state changes. Event
contracts define:

- stable event name and schema version;
- immutable event and aggregate identities;
- Creator scope and occurrence time;
- aggregate version or ordering key where ordering is required;
- causation and server-generated correlation identities;
- minimal event payload with explicit classification; and
- producer release identity.

Consumers must be idempotent. Delivery is at least once unless a stronger
guarantee is explicitly proven. Consumers cannot assume global ordering;
ordering is defined only for the smallest required aggregate or partition.
Schema evolution is additive where possible, and incompatible changes use a new
version with a documented migration and replay strategy.

When a state change and its event must agree, the domain change and outbox entry
commit in the same transaction. Dispatch is asynchronous, bounded, observable,
retryable, and dead-lettered after an approved policy. Replay never bypasses
Creator scope, authorization, consent, idempotency, or current data-handling
rules.

Initial event families include Planning lifecycle, publication, subscription
and consent, notification delivery, professional proposals and approvals,
commerce and refunds, photography licensing and fulfillment, and approved AI
proposal lifecycle. Detailed event catalogs are added with their owning Engine,
not invented speculatively in shared contracts.

## Reporting Projections

Reports read purpose-built projections rather than issuing unrestricted joins
across operational schemas. Projections are derived, versioned, rebuildable,
and disposable; their source-of-truth records remain in the owning Engine,
audit store, or event stream.

Creator reports require explicit Creator scope at query, key, index, cache,
export, and background-work boundaries. Platform-wide reports require a
separate permission, documented purpose, and controlled service boundary.
Creator membership does not grant platform reporting access, and support access
does not imply ordinary Creator access.

Initial reporting families may include:

- Creator planning and publication status;
- subscriber consent, delivery, suppression, and aggregate engagement;
- travel-professional proposal and approval activity;
- orders, refunds, licensing, fulfillment, and reconciliation;
- AI proposal acceptance, rejection, safety outcome, model family, and cost
  aggregates without raw prompts or responses; and
- platform security, deployment, reliability, and compliance evidence.

Reports and exports enforce resource authorization below the UI, prevent IDOR
and enumeration, bound date ranges and result sizes, and use asynchronous jobs
for large exports. Sensitive exports are encrypted in transit and at rest,
expire, use protected delivery, and are auditable. Watermarking is required
where the report's classification or commercial rights justify it.

## Product Analytics

Product analytics is opt-in only where consent or law requires it and always
has an approved purpose, owner, minimum fields, retention, and deletion model.
Prefer aggregate or pseudonymous measurements. Analytics identifiers do not
become identity, ownership, authorization, or cross-Creator correlation keys.

Private plans, traveler details, precise location, raw searches, private notes,
AI prompts and responses, unpublished media, and protected Resources are not
analytics payloads. A new analytics destination or tracking technology requires
privacy, security, residency, consent, cost, and vendor review.

## Prohibited Data

Audit, events, projections, analytics, and exports must not contain secrets,
credentials, tokens, cookies, authorization codes, assertions, raw claims,
connection strings, payment card data, passport or medical data, private notes,
raw AI exchanges, signed URLs, or arbitrary request and response bodies.

Names, contact details, addresses, reservation references, precise locations,
financial values, and licensed-media details require an explicit business
purpose, classification, access policy, retention rule, and field-level review.
Use opaque identifiers and allowlisted fields by default.

## Governance and Lifecycle

Every schema has an owner, purpose, version, classification, retention class,
access policy, and compatibility tests. Each environment uses separate stores,
identities, encryption, exports, and administrative access. Managed Identity is
preferred for supported Azure workload access.

Retention is defined by record class and jurisdiction; indefinite retention is
not the default. Legal hold suspends eligible deletion through an authorized,
audited process. Privacy deletion, correction, and export requests distinguish
authoritative audit obligations from derived projections: projections are
rebuilt or deleted, while legally required audit evidence is minimized and
retained only under the approved policy.

Backup, restore, integrity validation, disaster recovery, and evidence-export
procedures are tested. Audit and financial evidence use stable references and
reconciliation controls. Hashing or tamper-evidence may supplement access and
append controls but does not replace them.

## Storage Evolution

Azure SQL is the initial durable store for audit intents, outbox records, and
modest Creator-scoped projections when it meets scale and isolation needs.
Logical boundaries, separate schemas, least-privilege identities, and
provider-neutral contracts preserve future migration options.

A warehouse, lake, or specialized analytical store is introduced only when
volume, query complexity, retention economics, or cross-domain analysis
justifies it. Such a store receives sanitized, governed data through approved
pipelines; it never becomes a shortcut around operational authorization or
Creator isolation.

## Verification

Automated and real-infrastructure tests, as applicable, cover:

- atomic mutation and required-audit behavior;
- append-only permissions and audited evidence access;
- Creator isolation across writes, queries, projections, caches, and exports;
- event schema compatibility, deduplication, ordering scope, retry, replay, and
  dead-letter behavior;
- projection rebuilds and idempotent consumers;
- retention, deletion, legal hold, and environment isolation;
- prohibited-data canaries and export authorization;
- financial reconciliation and consent evidence where applicable; and
- AI lineage and approval evidence without raw private inputs or outputs.

Production readiness requires named owners, runbooks, access reviews, retention
decisions, recovery evidence, and cost controls for each enabled data product.

## Ownership and Review Triggers

Platform architecture owns shared contracts and governance. Each Engine owns
its action classifications, event catalog, projections, and report semantics.
Security and privacy own evidence access, prohibited-data, retention, legal
hold, and privacy-request review. Data and operations owners govern analytical
destinations, pipelines, reconciliation, recovery, cost, and service levels.

Review this architecture when adding an Engine, protected mutation, sensitive
read, background workflow, public API, AI provider, subscriber channel,
professional collaboration, commerce flow, financial report, export, analytics
destination, jurisdiction, or production environment.
