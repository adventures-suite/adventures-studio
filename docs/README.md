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
