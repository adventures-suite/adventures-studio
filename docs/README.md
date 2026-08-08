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
2. `architecture/ai-planning-copilot.md`
3. `product/creator-planning-workspace.md`
4. `development/planning-engine-implementation-plan.md`

The governing principle is:

> AI proposes; the Creator decides; the Planning Engine commits.

## Identity and Authorization

1. `architecture/identity-authorization.md`
2. `architecture/identity-provider.md`
3. `architecture/authentication-integration.md`
4. `architecture/security.md`
5. `development/identity-authorization-implementation-plan.md`

> Authentication establishes who the user is. Authorization determines which
> Creator-owned resource they may access for this operation.

## Logging and Observability

1. `architecture/observability.md`
2. `development/observability-implementation-plan.md`
3. `development/deployment.md`

> Logs explain system behavior. Audit records prove protected actions. Neither
> may expose private Creator content.

## Travel Professional Partnerships

1. `architecture/partner-collaboration-engine.md`
2. `product/travel-professional-partnership.md`
3. `development/partner-collaboration-implementation-plan.md`

> The customer owns the Adventure. The travel professional improves it.

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
