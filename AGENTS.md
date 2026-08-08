# Adventures Studio Development Guide

## Architecture

- Use the existing JSON-driven content engine.
- Do not hardcode destination content.
- Use ITravelContentService.
- Prefer reusable Razor components.
- Keep pages data-driven.
- Treat Adventures Studio as the company and AdventuresSuite as the platform.
- Treat Creator as the tenant and ownership boundary.
- Resolve Creator Context once per request from an explicitly approved host.
- Require Creator identity in core content and address operations.
- Include Creator identity in cache keys, background work, and indexes.
- Never fall back to a default Creator for an unknown production host.
- Evolve toward the Creator Engine incrementally; preserve working behavior.
- Follow docs/architecture/creator-engine.md and
  docs/development/creator-engine-refactoring-plan.md when changing tenancy.

## Planning and AI

- Read docs/architecture/planning-engine.md,
  docs/architecture/ai-planning-copilot.md, and
  docs/development/planning-engine-implementation-plan.md before changing
  planning or AI behavior.
- Treat private AdventurePlan data as distinct from public Content Engine
  records.
- Require Creator identity in every planning, persistence, AI, cache,
  background-work, and indexing operation.
- Keep planning data private unless an explicit publication operation selects
  approved fields for public content.
- Treat AI output as untrusted structured proposals, never as authoritative
  plan state.
- Require Creator review before an AI proposal can mutate a plan.
- Keep domain and application contracts independent of AI providers, model
  names, prompts, EF Core, and Razor components.
- Use date-only values for travel calendar dates, IANA identifiers for local
  time zones, and UTC timestamps for system audit events.
- Implement Planning Engine phases in order and do not combine them into a
  broad rewrite.

## Partner Collaboration

- Read `docs/architecture/partner-collaboration-engine.md`,
  `docs/product/travel-professional-partnership.md`, and
  `docs/development/partner-collaboration-implementation-plan.md` before
  changing professional collaboration behavior.
- Treat travel professionals as partners, not as competitors to replace.
- Keep the customer Creator as owner of the Adventure Plan, memories,
  Resources, and Publications.
- Represent an agency as a Creator for its own brand, staff, templates, and
  Resources; do not add a parallel tenant model.
- Require an explicit, accepted, active, plan-scoped engagement. Agency
  membership alone never grants customer access.
- Default professional changes to proposals and customer approval. Direct-edit
  access requires a stronger explicit permission and complete audit history.
- Keep external agency systems behind provider-neutral adapters.
- Do not add speculative partner fields or tables to the current Planning
  persistence phase.

## Identity and Authorization

- Read `docs/architecture/identity-authorization.md`,
  `docs/architecture/identity-provider.md`,
  `docs/architecture/authentication-integration.md`,
  `docs/architecture/security.md`, and
  `docs/development/identity-authorization-implementation-plan.md` before
  changing authentication, membership, authorization, sessions, or audit.
- Authentication establishes human identity; authorization determines whether
  that user may perform one operation on one Creator-owned resource.
- Keep User, Creator, membership, workload, and future engagement identities
  distinct.
- Enforce authorization below the UI through explicit resource-aware policies.
- Treat public host resolution as independent from private authorization.
- Activate private authentication schemes and endpoints only on the canonical
  workspace host. Public Creator hosts must ignore or reject manually supplied
  workspace cookies.
- Preserve OIDC issuer and subject values exactly. Compare and persist them with
  ordinal, case-sensitive semantics; never lowercase either identity value.
- Require exact workspace-origin validation for every cookie-authenticated
  SignalR transport, including negotiate, WebSockets, Server-Sent Events, and
  long polling.
- Default deny when Creator ownership, membership, or permission cannot be
  proven.
- Agency membership never grants customer-plan access without a future active,
  matching Planning Engagement.
- Keep provider claims and framework authorization types out of core contracts.

## Logging and Observability

- Read `docs/architecture/observability.md` and
  `docs/development/observability-implementation-plan.md` before changing logs,
  metrics, traces, health checks, telemetry export, dashboards, or alerts.
- Use structured `ILogger<T>` message templates, `ActivitySource`, and `Meter`;
  keep vendor SDK types out of core code.
- Propagate correlation context explicitly. Include Creator, actor, or resource
  identifiers only when authorized, operationally necessary, and permitted for
  that signal class; never log private Creator content or sensitive traveler
  data.
- Use route templates and stable event names. Do not log raw URLs, request
  bodies, domain objects, SQL parameters, AI prompts, tokens, or secrets.
- Keep metric dimensions low-cardinality; never dimension metrics by Creator,
  user, plan, hostname, or another unbounded identifier.
- Treat operational telemetry, security telemetry, audit records, business
  events, and product analytics as different signal types.
- Operational telemetry is best-effort. Never use it as the durable audit trail.
- Add redaction, cross-Creator leakage, correlation, and exporter-failure tests
  with new instrumentation.

## Documentation

- XML document all public classes, methods, and properties.
- Include meaningful comments explaining intent.

## Coding Style

- Follow existing naming conventions.
- Favor dependency injection.
- Prefer async methods.
- Keep components small and reusable.

## Deployment

- Use GitHub Environments.
- Never hardcode Azure values.
- Prefer Managed Identity for supported Azure workload-to-service access. Do
  not use it as human identity or assume it can authenticate an External ID
  confidential web client.
