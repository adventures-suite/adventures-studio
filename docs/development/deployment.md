# AdventuresSuite Deployment Observability

Production and development deployments must be identifiable, observable, and
safe to diagnose without exposing Creator content.

## Required Release Context

Every deployed instance exposes the following through internal telemetry:

- application and service name;
- deployment environment;
- immutable release or commit SHA;
- application version;
- instance identity where operationally useful;
- deployment and startup correlation identity.

Values come from GitHub Environments and Azure configuration. They are not
hardcoded and do not include secrets.

## Deployment Gate

Deployment automation must:

1. produce and deploy one immutable package;
2. confirm process startup;
3. confirm Creator, Content, Resource, and applicable schema validation;
4. execute safe liveness and readiness checks;
5. verify the expected release SHA is serving;
6. validate telemetry configuration for the environment-specific destination;
7. retain enough deployment correlation to diagnose or roll back a failure.

Public health checks return only minimal status. Detailed dependency health and
startup failures remain access-controlled.

Application health, release identity, startup validation, and required smoke
tests are hard promotion gates. Failure to confirm telemetry ingestion because
the destination is unavailable produces a warning and degraded deployment
result rather than failing an otherwise healthy release. Production may use a
stricter explicitly approved promotion policy, but application availability and
rollback remain independent of telemetry export.

## Azure Separation

Development and production use separate Azure Monitor/Application Insights
destinations, retention settings, access controls, alerts, dashboards, and cost
budgets. A deployment must not send one environment's telemetry to another.

Azure settings and connection information are maintained through GitHub
Environments and Azure configuration. Managed Identity is preferred where the
selected Azure integration supports it.

## Failure and Rollback

Telemetry export failure does not make the application unavailable and does not
block rollback. A failed startup, readiness check, release-identity check, or
required post-deployment smoke test prevents promotion and produces a correlated
diagnostic event without private content.

Full conventions and implementation sequencing are defined in
`docs/architecture/observability.md` and
`docs/development/observability-implementation-plan.md`.
