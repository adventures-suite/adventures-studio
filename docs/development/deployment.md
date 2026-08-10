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

### Immutable Package Activation Gate

App Service package activation is an independently reviewable operational
boundary. Its workflow correction must remain separate from feature and
architecture commits. A package deployment is complete only when all of the
following are proven:

- upload of the uniquely named immutable ZIP completes successfully before an
  explicit application restart begins;
- `/health` reports the expected commit SHA from that exact package;
- Creator and Resource startup validation both succeed;
- failure diagnostics identify the expected SHA, safely reported SHA, active
  package pointer, Azure deployment record, and safe startup state without
  exposing configuration or Creator content;
- a later deployment repeats the same upload, activation, restart, and
  verification sequence successfully; and
- rollback is exercised or otherwise proven possible by activating a previously
  retained immutable package and verifying its release identity and startup
  validation.

One successful SHA observation is evidence for that release, not proof that the
activation sequence is repeatable. Retained packages are release artifacts and
must remain identifiable by commit and workflow run for diagnosis and rollback.

### Development Rollback Procedure

The dev workflow retains an artifact named
`adventures-suite-<full-sha>-<run-attempt>` containing the uniquely named ZIP
`adventures-suite-<full-sha>-<run-attempt>.zip`. To roll back:

1. select the previously healthy workflow run and record its full commit SHA
   and run attempt;
2. download that exact retained artifact without rebuilding it;
3. verify the artifact and ZIP names match the selected SHA and attempt;
4. upload the retained ZIP with restart disabled;
5. require a new successful Azure deployment record before explicitly
   restarting App Service; and
6. verify `/health` reports the selected rollback SHA plus successful Creator
   and Resource validation.

Rollback never rebuilds an old source revision, selects “latest,” or reuses a
mutable package name. The operator records the source run, rollback run,
deployment record, package pointer, and final health evidence.

Application health, release identity, startup validation, and required smoke
tests are hard promotion gates. Failure to confirm telemetry ingestion because
the destination is unavailable produces a warning and degraded deployment
result rather than failing an otherwise healthy release. Production may use a
stricter explicitly approved promotion policy, but application availability and
rollback remain independent of telemetry export.

### Performance Promotion Evidence

Load testing follows `docs/architecture/performance-load-testing.md`. Ordinary
feature deployments do not run an expensive high-scale test automatically.
Before limited external alpha, a production launch, an infrastructure capacity
change, or a material performance-sensitive release, the exact immutable
candidate must pass the approved production-like baseline, expected-peak, and
post-load recovery gates.

The load result records the candidate SHA, environment fingerprint, workload
and synthetic-data versions, thresholds, cost, and retained evidence. Failure
of an approved performance threshold blocks promotion until disposition or an
explicit reviewed exception. Load-test infrastructure or telemetry failure is
reported separately from an application performance failure and never prevents
rollback.

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

## Slice 5F Private Azure Environment

Authentication-enabled development deployment additionally follows:

- `docs/development/slice-5f-azure-environment.md`;
- `docs/development/external-id-environment-runbook.md`;
- `docs/development/azure-sql-migration-runbook.md`; and
- `docs/development/authentication-key-management-runbook.md`.

Azure live state is not the reproducible source of truth. Reviewed IaC owns
supported Azure control-plane resources, while versioned runbooks govern
External ID, certificate, SQL bootstrap, migration, smoke, rotation, recovery,
and teardown boundaries. Public data-plane access must not be enabled merely to
accommodate a GitHub-hosted runner.
