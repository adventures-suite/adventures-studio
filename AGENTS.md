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
- Prefer Managed Identity.
