# AdventuresSuite Logging and Observability Architecture

**Version:** 1.0

**Status:** Approved Direction

**Last Updated:** August 7, 2026

## Purpose

AdventuresSuite must explain what the platform is doing, detect degradation
before customers report it, and support secure incident investigation without
turning telemetry into another store of private Creator content.

## Governing Rule

> Logs explain system behavior. Audit records prove protected actions. Neither
> may expose private Creator content.

Observability is a platform capability. Features use shared conventions and
instrumentation rather than selecting their own vendors, schemas, or privacy
rules.

## Telemetry Taxonomy

These records have different purposes and must not be conflated:

| Signal | Purpose | Examples |
| --- | --- | --- |
| Operational logs | Explain discrete application behavior | startup validation, dependency failure |
| Distributed traces | Follow one operation across boundaries | web request to Planning repository |
| Metrics | Measure aggregate health and capacity | request latency, failure rate, queue depth |
| Security telemetry | Detect suspicious behavior | repeated denials, invalid callbacks |
| Audit records | Prove protected decisions and mutations | membership change, plan restoration |
| Business events | Represent durable domain facts | plan archived, publication completed |
| Product analytics | Understand opted-in feature use | workspace adoption, funnel completion |

Operational telemetry is not an authoritative audit trail or business-event
store. Audit and durable business events use their own persistence and delivery
guarantees. Product analytics requires an approved purpose, consent model, and
data-minimization review. Platform-wide audit, event, projection, reporting,
retention, and analytics governance is defined in `audit-reporting.md`.

## Technical Direction

Application code uses standard .NET abstractions:

- `ILogger<T>` with structured message templates for logs;
- `ActivitySource` and W3C Trace Context for traces;
- `Meter` for metrics;
- framework-independent audit and business-event contracts.

OpenTelemetry is the instrumentation and export boundary. Azure Monitor and
Application Insights are the initial Azure observability destination, connected
through infrastructure configuration and exporters. Core domain and application
contracts do not reference Azure Monitor, Application Insights, exporter SDKs,
or workspace-specific types.

Automatic ASP.NET Core, HTTP client, and SQL instrumentation may supplement
explicit business instrumentation, but automatic collection must pass privacy,
cardinality, cost, and performance review before production enablement.

## Correlation Model

- W3C `traceparent` is the distributed tracing standard, but inbound trace
  context is untrusted diagnostic input. It never establishes identity,
  authorization, uniqueness, idempotency, ownership, or audit integrity.
- Validate inbound trace context and discard malformed or unsupported values.
  Every inbound operation also receives a server-generated request/support
  identity that the caller cannot select or reuse.
- Outbound HTTP, background-work envelopes, and asynchronous messages propagate
  validated trace context using approved standards. Arbitrary inbound W3C
  baggage is not accepted or propagated; baggage keys require an explicit
  allowlist, classification, and propagation purpose.
- A background operation creates a new processing span and links to the
  initiating trace when direct parentage would misrepresent elapsed time.
- Audit records may reference the server-generated request/support identity and
  trace context for diagnosis, but neither value is the audit event identity or
  integrity boundary. Audit does not depend on telemetry retention.
- Public responses may expose only the server-generated support identifier,
  never a caller-selected trace identifier, internal exception, database
  identifier, token, or secret.

## Standard Telemetry Context

Use consistent, low-cardinality names. When relevant, telemetry includes:

- service name, component, version, deployment environment, and release SHA;
- event name or stable event identifier and severity;
- trace, span, and correlation identifiers;
- operation name and route template, never a raw URL with user values;
- outcome, status category, duration, and retry count;
- dependency type and logical dependency name;
- opaque `CreatorId` for authorized internal diagnosis;
- actor type and an opaque actor identifier only when operationally necessary;
- resource type and opaque resource identifier only when necessary;
- background job type and Creator-scoped idempotency identity;
- exception type and safe failure category.

Property names are centrally defined and reused. Do not dynamically create
property names, metric names, or dimensions from Creator content.

Creator identity is diagnostic context, not a license to emit Creator data.
Creator-scoped dashboards and queries must still enforce access controls.

## Data Classification and Redaction

Never record:

- passwords, secrets, keys, connection strings, cookies, or bearer tokens;
- authentication assertions, authorization codes, refresh tokens, or raw claims;
- full request or response bodies by default;
- reservation confirmation codes, payment data, passport data, medical data,
  precise live location, or private traveler notes;
- raw AI prompts, model responses, uploaded documents, or private plan content;
- email addresses, phone numbers, postal addresses, or names unless a separately
  approved operational requirement defines protection and retention;
- signed URLs, query strings, headers, or exception messages that may contain
  protected values.

Prefer allowlisted structured properties over attempting to redact arbitrary
payloads after collection. Redaction occurs before export. Exception logging
captures the exception type, safe message, stack trace where access-controlled,
and correlation context; custom exceptions must not embed protected values.

Telemetry enrichment must be tested for cross-Creator leakage. Diagnostic modes
cannot disable privacy rules in shared or production environments.

## Log-Level Policy

- `Trace`: temporary, targeted local diagnosis; disabled in normal shared
  environments.
- `Debug`: local development details without private payloads; normally disabled
  in production.
- `Information`: lifecycle milestones and meaningful completed operations, not
  every method entry or successful record read.
- `Warning`: recoverable abnormal state requiring attention if persistent.
- `Error`: an operation failed and requires investigation or remediation.
- `Critical`: platform availability, integrity, or security is at immediate risk.

Expected validation and authorization denials are not application errors.
Record them as bounded security telemetry with an appropriate outcome category.
Avoid duplicate logging as an exception crosses layers; log once at the boundary
that has enough context to classify the failure.

## Metrics and Cardinality

Initial platform metrics include:

- request rate, error rate, and duration by route template and status category;
- availability and health-check success;
- startup duration and Creator, Content, Resource, and migration validation;
- SQL dependency duration, failures, transient retries, and pool pressure;
- authorization decisions by permission and outcome, without resource IDs;
- background-work throughput, failures, retries, age, and dead-letter count;
- AI request latency, failure category, token/cost units, and proposal validity;
- publication and notification pipeline health when those capabilities exist.

Metrics never use `CreatorId`, `UserId`, `AdventurePlanId`, hostname, raw route,
exception message, or other unbounded value as a dimension. High-cardinality
investigation belongs in access-controlled logs and traces.

## Sampling, Retention, and Cost

- Metrics are retained as aggregates appropriate to operational trends.
- Traces may use head or tail sampling, with rates configured per environment.
- Errors and diagnostically important traces receive preferential retention.
- Audit records, security evidence, and durable business events are never made
  reliable by trace sampling.
- Retention periods are defined by signal class, environment, privacy purpose,
  incident needs, and cost; indefinite retention is prohibited by default.
- Daily ingestion budgets, sampling changes, and unexpected-cardinality alerts
  protect the platform from telemetry-driven cost incidents.
- Production telemetry access is least-privilege and itself auditable where
  sensitive operational context is available.

Exact retention periods and budgets are deployment decisions and must be
recorded before production data is collected.

## Reliability and Failure Behavior

Operational telemetry is normally best-effort and must not make a customer
operation fail. Export occurs asynchronously with bounded buffers, timeouts, and
backpressure. The platform must not retry telemetry indefinitely or exhaust
memory when the destination is unavailable.

Deployment validates telemetry configuration before promotion. A telemetry
destination outage produces a warning and degraded deployment result but does
not make an otherwise healthy application unavailable or prevent rollback.
Application health, release identity, startup validation, and required smoke
tests remain hard gates. Production may adopt a stricter explicitly approved
promotion policy without coupling application availability or rollback to the
telemetry destination.

Required audit intent follows the Identity and Authorization architecture: a
protected mutation and its audit intent commit atomically in one transaction or
through a transactional outbox. Sensitive reads marked as audit-required fail
closed according to policy. Operational logs are never substituted for audit.

## Health, Service Levels, and Alerting

Health endpoints distinguish:

- liveness: the process can run;
- readiness: the instance can safely receive traffic;
- dependency health: diagnostic information for authorized operations only.

Public health responses disclose no configuration, Creator identity, dependency
address, exception, or secret.

Initial service-level indicators are availability, server error rate, request
latency, startup success, and critical dependency success. Objectives and alert
thresholds are environment-specific and must be measurable before production
commitments are made.

Alerts must be actionable and link to a runbook. Initial alert families cover:

- sustained availability or latency degradation;
- repeated startup or deployment validation failure;
- elevated HTTP 5xx or unhandled exceptions;
- SQL connectivity, timeout, or migration failure;
- abnormal authorization denials or authentication failures;
- background-work backlog or poison messages;
- telemetry ingestion loss, quota pressure, or cost anomaly.

Avoid paging on a single transient failure. Severity, evaluation window,
deduplication, ownership, and escalation are explicit.

## Environment Behavior

Finite migration Jobs emit a bounded structured start record and exactly one
completion envelope. Operation ID and release metadata are high-cardinality log
properties used only for access-controlled evidence, never metric dimensions.
Migration classification, execution status category, and mode are bounded
dimensions. Logs exclude SQL text, connection strings, tokens, environment
dumps, application rows, and arbitrary exception messages. Azure terminal Job
status and the envelope must agree; missing evidence fails the operation closed.

- Local development uses readable console output and optional local trace
  inspection. Privacy rules remain active.
- Tests use deterministic in-memory collectors or fakes and do not require a
  network exporter.
- CI validates instrumentation contracts, redaction, and configuration without
  sending routine test telemetry to production workspaces.
- Development and production use separate Azure resources, connection settings,
  access controls, retention, dashboards, alerts, and budgets.
- Configuration uses GitHub Environments and Azure settings. Secrets are never
  committed; Managed Identity is preferred where supported.

## Verification

Automated tests must cover:

- standard context and correlation propagation;
- no cross-Creator context reuse;
- route templates rather than raw paths;
- redaction and prohibited-value canaries;
- bounded exception and denial telemetry;
- metric dimension allowlists and cardinality limits;
- background-work trace links and Creator scope;
- exporter failure without customer-operation failure;
- atomic audit behavior at protected mutation boundaries;
- safe health and error responses.

Production smoke tests confirm release identity, startup validation, health,
trace ingestion, dependency correlation, dashboards, and alert routing without
emitting private content.

## Ownership and Review

The platform architecture owns semantic conventions and privacy rules.
Individual Engines own meaningful instrumentation for their operations. Azure
infrastructure owns exporters, workspaces, access, retention, budgets, alerts,
and dashboards. Security owns sensitive-data and audit review.

Review this architecture when a new telemetry destination, public API,
background processor, AI provider, protected data class, analytics capability,
or production environment is introduced.

Performance, load, stress, spike, soak, scalability, and recovery testing follow
`performance-load-testing.md`. Load-test telemetry uses the same privacy,
cardinality, environment-isolation, retention, and cost rules defined here.
