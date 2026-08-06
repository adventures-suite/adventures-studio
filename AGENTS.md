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
