# Observability Implementation Plan

**Status:** Approved for Incremental Implementation

**Last Updated:** August 7, 2026

## Objective

Introduce secure, provider-neutral observability without interrupting current
Planning or Identity work. Each slice must be independently testable and must
not place private Creator content in telemetry.

Read first:

- `AGENTS.md`
- `docs/architecture/observability.md`
- `docs/architecture/security.md`
- `docs/architecture/identity-authorization.md`
- `docs/development/deployment.md`

## Working Rules

- Use `ILogger<T>`, `ActivitySource`, and `Meter` in application code.
- Keep Azure and exporter types in infrastructure and composition roots.
- Use structured properties, never string concatenation or serialized domain
  objects.
- Propagate trace and Creator context explicitly across asynchronous boundaries.
- Keep metrics low-cardinality.
- Treat audit, security telemetry, analytics, and business events as distinct.
- Apply privacy rules in every environment.
- Complete redaction and failure-behavior tests before production export.

## Slice 0: Architecture and Inventory

Inventory existing log statements, health checks, failure handling, Azure
configuration, background boundaries, and sensitive data. Approve the telemetry
taxonomy, standard context, prohibited-data rules, and initial service-level
indicators.

Exit gate: governing documentation is consistent and `git diff --check` passes.

## Slice 1: Platform Observability Contracts

Add a small shared observability vocabulary for stable event names, property
names, safe failure categories, and correlation context. Introduce platform
`ActivitySource` and `Meter` ownership without exporter packages.

Acceptance criteria:

- core domains contain no Azure or exporter dependencies;
- identifiers are typed and rendered only through approved enrichers;
- no domain object is accepted as an arbitrary log property;
- unit tests enforce property names and prohibited-data behavior;
- existing startup logs adopt the common conventions.

## Slice 2: Request, Exception, and Dependency Instrumentation

Instrument ASP.NET Core request handling, centralized exception classification,
outbound HTTP, and SQL operations. Propagate W3C trace context and return a safe
server-generated support identifier for unexpected failures. Treat inbound
trace context as untrusted diagnostic input and do not propagate arbitrary W3C
baggage.

Acceptance criteria:

- route templates replace raw URLs;
- expected denials and validation failures are not logged as errors;
- an exception is not duplicated across layers;
- SQL text and parameters containing customer data are not collected;
- cross-Creator and redaction canary tests pass.

## Slice 3: OpenTelemetry and Local Verification

Configure OpenTelemetry through dependency injection. Enable only reviewed
instrumentation and use console or in-memory export for local development and
tests.

Acceptance criteria:

- exporter failure does not fail customer operations;
- buffers, timeouts, and shutdown flushing are bounded;
- metrics meet the dimension allowlist;
- tests remain deterministic and require no external telemetry service.

## Slice 4: Azure Monitor Export

Provision separate development and production observability resources through
infrastructure as code. Configure Azure Monitor/Application Insights export,
release metadata, access control, retention, sampling, and ingestion budgets
through GitHub Environments and Azure settings.

Acceptance criteria:

- no connection secret is committed;
- Managed Identity is used where supported by the selected integration;
- development cannot export into the production destination;
- release SHA and environment are queryable;
- production access follows least privilege.

## Slice 5: Dashboards, Service Levels, Alerts, and Runbooks

Create dashboards for availability, latency, errors, startup, SQL dependencies,
and deployment health. Define measurable service-level objectives after a
baseline exists. Add actionable alerts with owners and versioned runbooks.

Acceptance criteria:

- each alert describes customer impact and a first diagnostic action;
- alerts use sustained windows and deduplication;
- alert routing is tested without generating a production incident;
- ingestion and cost anomalies are visible.

## Slice 6: Identity and Security Telemetry

Add bounded authentication and authorization telemetry when those capabilities
are implemented. Correlate human or system actor type, Creator scope, permission,
and outcome without recording tokens, claims, emails, or private resources.

Acceptance criteria:

- repeated denials cannot create unbounded cost or storage use;
- account responses do not enable enumeration;
- security events correlate with, but do not replace, durable audit records;
- sensitive audited reads and protected mutations follow approved audit failure
  semantics.

## Slice 7: Background Work, AI, and Future Engines

Extend trace propagation, metrics, and safe events to background processing,
Planning proposals, AI operations, publication, subscriptions, notifications,
and commerce only as those capabilities are implemented.

Each Engine defines meaningful operations, latency and failure measures, privacy
review, and runbook ownership before production enablement.

## Release Gate

- release build and full relevant test suite pass;
- formatting and `git diff --check` pass;
- prohibited-data and cross-Creator leakage tests pass;
- health responses reveal no protected details;
- Azure smoke test validates telemetry configuration and attempts to confirm
  correlated ingestion and release identity; destination unavailability yields
  a degraded warning under the default policy rather than blocking a healthy
  release;
- dashboards and alerts are operational;
- retention, sampling, access, and budget decisions are recorded;
- rollback does not depend on the telemetry destination being available.

## Explicitly Deferred

- a vendor-specific logging abstraction in core code;
- storing audit records only in Application Insights;
- capturing raw request bodies, SQL parameters, AI prompts, or model responses;
- Creator-configurable production debug logging;
- product analytics without a separate consent and privacy decision;
- exact production SLO targets before operational baselines exist.
