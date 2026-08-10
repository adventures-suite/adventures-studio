# AdventuresSuite Documentation

This directory is the source of truth for AdventuresSuite product,
architecture, authoring, and development direction.

Version 1.0 is defined by the successful publication of The Simonton
Adventures – Volume I and its companion website. The current strategic
initiative expands the platform into private Adventure planning and
human-approved AI assistance.

## Start Here

- `ROADMAP.md` — product sequence and current strategic initiative
- `DECISIONS.md` — approved architectural decisions
- `principles.md` — platform principles
- `architecture/platform/platform-architecture.md` — long-term platform model
- `architecture/adventure-lifecycle.md` — Dream through Remember lifecycle

## Planning and AI

Read these documents together and in this order:

1. `architecture/planning-engine.md`
2. `architecture/adventure-templates.md`
3. `architecture/adventure-map-experience.md`
4. `architecture/itinerary-ingestion.md`
5. `architecture/group-travel-collaboration.md`
6. `architecture/adventure-travel-playbook.md`
7. `architecture/adventure-calendar-integration.md`
8. `architecture/adventure-readiness-and-change-management.md`
9. `architecture/ai-planning-copilot.md`
10. `product/creator-planning-workspace.md`
11. `development/planning-engine-implementation-plan.md`

The governing principle is:

> AI proposes; the Creator decides; the Planning Engine commits.

> Playbooks and calendar events are authorized projections of the plan. They
> are neither the source of truth nor implicit public content.

> Adventure Templates create independent private plans. Template ownership
> never grants access to a customer's Adventure.

> Planning maps reveal the right level of spatial detail without turning
> candidate places, inferred routes, or private location into authoritative or
> public facts.

> Uploaded itineraries produce reviewable Journey Stop proposals. OCR and AI
> never silently create authoritative or public travel records.

> Group conversation exists to support Adventure decisions. Travelers express
> preferences; authorized planners commit Planning changes.

## Identity and Authorization

1. `architecture/identity-authorization.md`
2. `architecture/identity-provider.md`
3. `architecture/authentication-integration.md`
4. `architecture/security.md`
5. `development/identity-authorization-implementation-plan.md`

> Authentication establishes who the user is. Authorization determines which
> Creator-owned resource they may access for this operation.

### Slice 5F Azure Environment and Runbooks

1. `development/slice-5f-azure-environment.md`
2. `development/external-id-environment-runbook.md`
3. `development/azure-sql-migration-runbook.md`
4. `development/authentication-key-management-runbook.md`
5. `development/deployment.md`

> Azure is the running environment. Infrastructure as code is the reproducible
> definition. Runbooks govern cross-tenant and data-plane operations.

## Logging and Observability

1. `architecture/observability.md`
2. `development/observability-implementation-plan.md`
3. `development/deployment.md`

> Logs explain system behavior. Audit records prove protected actions. Neither
> may expose private Creator content.

## Audit and Reporting

1. `architecture/audit-reporting.md`
2. `development/audit-reporting-implementation-plan.md`
3. `architecture/observability.md`
4. `architecture/security.md`

> Transactional systems record authoritative facts. Reporting systems consume
> authorized, purpose-built projections.

## Travel Professional Partnerships

1. `architecture/partner-collaboration-engine.md`
2. `product/travel-professional-partnership.md`
3. `development/partner-collaboration-implementation-plan.md`

> The customer owns the Adventure. The travel professional improves it.

## AdventuresCompanion Mobile

1. `architecture/adventures-companion.md`
2. `architecture/companion-api-sync.md`
3. `architecture/companion-openapi.md`
4. `product/adventures-companion.md`
5. `development/adventures-companion-implementation-plan.md`

> The traveler controls capture. Private synchronization is not publication.

> Companion receives versioned JSON and authorized media—not SQL or Dapper
> models. Push signals change; the API returns authoritative current state.

## Subscriptions and Notifications

1. `architecture/subscription-notification-engine.md`
2. `architecture/companion-api-sync.md`
3. `architecture/adventure-readiness-and-change-management.md`

> Public audience updates and private traveler operations use separate policy
> lanes. Durable intent drives delivery; provider push never becomes state.

## Platform Billing and Entitlements

1. `architecture/platform-billing-entitlements.md`
2. `product/pricing-model.md`
3. `business/business-model.md`
4. `development/platform-billing-entitlements-implementation-plan.md`
5. `architecture/commerce-engine.md`

> Authorization decides what a user may do. Platform entitlement decides what
> the Creator has purchased or been granted.

## Existing Engine Foundations

- `architecture/creator-engine.md`
- `architecture/content-engine.md`
- `architecture/resource-engine.md`
- `architecture/address-engine.md`
- `architecture/qr-engine.md`

## Working Agreement

Repository agents must read `../AGENTS.md` before implementation. Planning
Engine phases are intentionally gated; do not combine them into a broad rewrite
or introduce later-phase infrastructure early.
