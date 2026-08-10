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
- Read `docs/architecture/adventure-travel-playbook.md`,
  `docs/architecture/adventure-calendar-integration.md`, and
  `docs/architecture/adventure-readiness-and-change-management.md`, and
  `docs/architecture/adventure-map-experience.md` before
  changing plan exports, calendar behavior, readiness, countdowns, change
  impact, protected travel documents, planning maps, or offline travel packages.
- Treat Playbooks and calendar events as versioned, authorized projections of
  Planning state, never as authoritative plan records or implicit publication.
- Never place ticket codes, booking PINs, private notes, precise breadcrumbs,
  or permanent protected-Resource URLs in a calendar event or shareable output.
- Require each traveler to opt into writes or synchronization with that
  traveler's calendar; membership or plan participation is not consent.
- Derive countdowns from authoritative schedule data. Never persist countdown
  ticks, invent a departure time for a date-only plan, or let a countdown change
  lifecycle state.
- Treat readiness, change impact, traveler views, acknowledgments, and active-
  travel guidance as authorized projections; they never replace Planning state.
- Treat document extraction and AI classification as untrusted proposals with
  provenance and human review before Planning mutation.
- Read `docs/architecture/itinerary-ingestion.md` before changing itinerary
  upload, OCR, cruise-schedule parsing, place resolution, or Journey Stop
  proposal behavior.
- Store uploaded itinerary images and documents as protected Resources. Extract
  into confidence-scored, source-linked proposals; never write Journey Stops,
  Destination Visits, dates, times, or time zones directly from OCR or AI.
- Preserve the boundary between private Planning stop proposals and the public
  Content Engine `JourneyStop`; public records require a later explicit
  publication transformation.
- Read `docs/architecture/group-travel-collaboration.md` before changing group
  invitations, traveler participation, voting, discussion, announcements,
  acknowledgments, or collaborative Planning behavior.
- Treat traveler participation as an Adventure-scoped relationship, not Creator
  membership. Invitations, subgroup access, expiry, removal, and information
  policy must be explicit and enforced below the UI.
- Keep collaboration contextual: threads attach to Planning subjects, polls
  collect advisory preferences, and announcements communicate approved facts.
  Do not build general-purpose chat, presence, contacts, voice, or video.
- Never mutate Planning from a message or vote. Use the boundary: discussion or
  poll, authorized decision, validated Planning mutation, atomic audit intent.
- Treat maps as authorized projections of Planning state. Distinguish planned
  route segments, destination visits, selected places, and untrusted candidate
  points of interest; a marker or route line never proves a booking, exact
  route, availability, safety, accessibility, or navigation suitability.
- Keep map and geocoding providers behind provider-neutral adapters. Preserve
  attribution, licensing, freshness, source, and coordinate precision, and
  never expose private plans or precise traveler location through public maps.
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

## Adventure Templates

- Read `docs/architecture/adventure-templates.md` before changing reusable
  planning blueprints, template catalogs, template licensing, or plan creation
  from a template.
- Treat a template as a versioned blueprint, never as an Adventure Plan,
  booking, live inventory, or guaranteed price.
- Instantiate templates into new private, customer Creator-owned plans. Never
  live-link a plan so later template changes silently mutate customer state.
- Preserve template authorship, version, provenance, attribution, and license
  evidence without granting the template owner access to the resulting plan.
- Exclude traveler identities, reservations, payment data, private notes,
  precise breadcrumbs, protected media, and permanent Resource URLs from
  templates.
- Keep template parameters typed and validated; do not permit executable
  scripts, SQL, prompts, or provider calls inside template content.
- Agency-authored templates support professional partnerships. They do not
  make AdventuresSuite a travel agency, seller, merchant, or booking system.
- Require a separate accepted, active, plan-scoped Planning Engagement for a
  professional to collaborate after template instantiation.

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

## Audit and Reporting

- Read `docs/architecture/audit-reporting.md` and
  `docs/development/audit-reporting-implementation-plan.md` before changing
  audit records, business events, outbox processing, analytics, projections,
  reports, evidence exports, retention, or legal-hold behavior.
- Treat audit and reporting as required platform capabilities for every Engine.
- Keep audit records, business events, analytics, reporting projections, and
  operational telemetry logically distinct.
- Commit required mutation audit intent atomically with authoritative state or
  through a transactional outbox; never substitute logs or traces.
- Scope Creator reports at query, key, index, cache, export, and background-work
  boundaries. Platform-wide reporting requires separate explicit authority.
- Use versioned, minimal, allowlisted schemas. Never place private content,
  secrets, tokens, raw claims, raw AI exchanges, or arbitrary payloads in audit,
  events, analytics, or reports.
- Build reports from authorized, rebuildable projections rather than broad
  cross-domain queries over operational tables.
- Define ownership, purpose, classification, retention, deletion, access,
  recovery, cost, and compatibility tests before enabling a new data product.

## AdventuresCompanion Mobile

- Read `docs/architecture/adventures-companion.md`,
  `docs/architecture/companion-api-sync.md`,
  `docs/architecture/companion-openapi.md`,
  `docs/architecture/companion-api-v1-contract.md`,
  `docs/product/adventures-companion.md`,
  `docs/development/adventures-companion-implementation-plan.md`, and
  `docs/development/companion-api-v1-implementation-baseline.md` before changing
  mobile APIs, MAUI projects, offline synchronization, device storage,
  notifications, media capture, maps, or location behavior.
- Treat AdventuresCompanion as the first iOS and Android application and use
  .NET MAUI Blazor Hybrid with host-independent shared components and
  platform-specific adapters.
- Treat the device as an untrusted, intermittently connected client. Reauthorize
  every API operation with explicit Creator and resource scope.
- Keep only minimized, encrypted, revocation-aware offline projections; never
  replicate the Planning database to a device.
- Companion consumes only versioned JSON API contracts and authorized media or
  document streams. Never expose SQL, Dapper records, persistence models,
  domain aggregates, provider credentials, or permanent protected URLs.
- Build Companion responses as: Dapper persistence records, application query
  projection, authorization and traveler information policy, mobile DTO, JSON.
  Never serialize database rows or domain aggregates directly.
- Map Dapper records to validated application projections and authorized
  projections to Companion DTOs with explicit, hand-written mapping code.
  Reflection-based copying, AutoMapper-style convention mapping, generic
  same-name property copying, and serialization-as-mapping are prohibited
  across persistence, domain/application, and API boundaries. Adding a source
  property must never make it appear in an API response automatically.
- Push notifications signal that authorized state changed; they are not the
  state. Use minimal opaque payloads and require Companion to fetch current
  JSON after server reauthorization.
- Keep GPS breadcrumbs off by default. Only the authenticated traveler on that
  device may explicitly start capture, and capture must be visible, pausable,
  stoppable, retention-bound, and private until separately published.
- Never infer location consent from membership, plan participation, terms,
  notifications, or another user's approval. No actor may remotely enable
  another person's tracking.
- Never place precise location in logs, traces, metrics, analytics,
  notifications, ordinary audit metadata, or public content.
- Mobile uses public-client browser-delegated authorization code with PKCE; it
  does not reuse workspace cookies or embed client secrets or certificates.
- Host mobile APIs in a separately deployed `AdventuresSuite.Api` application,
  never inside or proxied through the Blazor web host. Share approved domain,
  application, authorization, and persistence libraries without duplicating
  business rules, and keep bearer-token and cookie pipelines host-specific.
- Prefer cohesive shared contract and UI libraries over a universal Common or
  Shared project. Companion must not reference server domain, application,
  authorization, persistence, Dapper, SQL, ASP.NET, Azure, or identity-provider
  projects. OpenAPI remains the cross-process compatibility authority even when
  API DTO source is held in a shared Companion contracts assembly.

## Subscription and Notification Engine

- Read `docs/architecture/subscription-notification-engine.md` before changing
  subscribers, device installations, notification events, intents, policies,
  delivery, preferences, push payloads, digests, or acknowledgments.
- Keep public audience notifications and private Companion operational
  notifications as separate policy lanes. Neither relationship grants access
  to the other.
- Commit required notification intent with authoritative state through the
  owning transaction or transactional outbox; deliver asynchronously and
  idempotently through provider-neutral adapters.
- Treat native push as best-effort signaling, never authoritative state or proof
  of identity, authorization, delivery to a human, viewing, acknowledgment,
  acceptance, or completion.
- Put only minimal opaque routing data in push payloads. Companion must fetch
  current authorized JSON before presenting protected detail.
- Enforce category, urgency, traveler preferences, time-zone-aware quiet hours,
  digest, deduplication, supersession, expiry, suppression, rate-limit, safe-
  preview, consent, and environment-isolation policy.

## Platform Billing and Entitlements

- Read `docs/architecture/platform-billing-entitlements.md`,
  `docs/product/pricing-model.md`, and
  `docs/development/platform-billing-entitlements-implementation-plan.md` before
  changing plans, billing accounts, subscriptions, paid capabilities,
  allowances, seats, usage, checkout, provider webhooks, or billing reports.
- Keep identity, Creator membership, authorization, Platform Entitlements,
  feature flags, service availability, and Creator Commerce distinct. Passing
  one gate never satisfies another.
- Use stable `PlatformCapability` vocabulary and immutable plan versions. Never
  branch application behavior on a marketing plan name.
- Evaluate entitlement below the UI with explicit Creator scope. Never trust
  cookies, identity claims, mobile tokens, redirects, feature flags, or provider
  payloads as authoritative entitlement.
- A seat never creates membership, chooses a role, or grants permission. A
  Billing Account that pays for multiple Creators gains no data access.
- Keep `PlatformEntitlement` separate from Creator Commerce
  `CommerceEntitlement`; do not share orders, subscriptions, or payment state.
- Treat provider webhooks as untrusted, signed, replay-protected, idempotent
  input processed through a transactional inbox and reconciliation.
- Never store payment-card data or derive billable usage from operational logs,
  metrics, analytics, or caller-submitted quantities.
- Preserve Creator data, recovery, and approved export through billing failure;
  do not automatically delete or unpublish work.

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
- Read `docs/development/slice-5f-azure-environment.md` and its linked runbooks
  before changing External ID, Azure SQL, private networking, Key Vault, Data
  Protection storage, workload identities, or the migration app.
- Treat Azure as live state, reviewed IaC as the reproducible definition, and
  runbooks as the authority for cross-tenant and data-plane operations.
- Keep application DML and migration DDL identities separate. The application
  never runs migrations or grants itself database access.
- Do not enable public SQL, Key Vault, Blob, or migration-app ingress to make a
  hosted workflow pass. Approve and prove a private execution path.
- Resolve generated principal IDs, private addresses, and object versions from
  deployment outputs; never hardcode them in application behavior.
- Keep the migration app stopped by default and return it to stopped state after
  success or failure with retained artifact and journal evidence.
