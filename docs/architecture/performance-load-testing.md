# AdventuresSuite Performance and Load-Testing Architecture

**Status:** Platform Must-Have

**Last Updated:** August 9, 2026

## Purpose

AdventuresSuite must measure whether its web, API, mobile synchronization,
background processing, persistence, Resource delivery, and notification
boundaries remain responsive, stable, isolated, and recoverable at expected and
unexpected load. Performance is a production-readiness requirement, not an
informal manual check after implementation.

## Governing Rules

> Performance tests prove behavior under a declared workload and environment;
> they do not create a universal capacity promise.

> Throughput never takes priority over Creator isolation, authorization,
> correctness, audit integrity, privacy, or recoverability.

Every result records the application release, infrastructure configuration,
data shape, test script version, concurrency model, duration, region, and
pass/fail criteria. Results from a local machine, deterministic test host, or
different Azure tier are not represented as production capacity evidence.

## Test Taxonomy

| Test | Purpose | Typical execution |
| --- | --- | --- |
| Microbenchmark | Detect regressions in bounded mapping, serialization, validation, or algorithms | Developer/targeted CI |
| Performance smoke | Detect catastrophic latency, allocation, or response-size regressions | Pull request or normal CI |
| Baseline load | Measure expected steady traffic | Pre-production release gate |
| Peak load | Prove approved expected peak and scale behavior | Pre-production release gate |
| Spike | Measure burst handling, rate limiting, queueing, and recovery | Scheduled/pre-release |
| Stress | Find controlled saturation and failure boundaries | Scheduled, explicitly approved |
| Soak | Find leaks, pool exhaustion, backlog, and degradation over time | Scheduled/nightly when justified |
| Scalability | Compare load and capacity across instance/tier changes | Infrastructure review |
| Resilience under load | Prove recovery during dependency delay, restart, scale-out, and transient failure | Pre-production chaos/resilience gate |

Microbenchmarks and in-memory tests are diagnostic evidence. Only tests against
an approved production-like environment provide deployment-capacity evidence.

## Technical Direction

Azure Load Testing is the managed high-scale execution and result boundary. It
supports source-controlled Apache JMeter or Locust workloads and GitHub Actions
integration. AdventuresSuite initially prefers Locust for readable API user
journeys; JMeter remains an approved alternative when protocol, ecosystem, or
tooling needs justify it.

The repository owns:

- load-test source and immutable configuration;
- synthetic-data definitions and setup/cleanup procedures;
- environment and test-plan identifiers;
- workload models and think-time distributions;
- thresholds and automatic-stop criteria;
- expected infrastructure configuration; and
- sanitized result summaries and retained workflow artifacts.

GitHub Actions authenticates to Azure with workload federation where supported,
uses least-privilege access limited to the load-testing resource and approved
environment, and stores no long-lived Azure credential. Package, action, and
test-runner versions are reviewed and pinned according to repository policy.

## Environment Boundary

Authoritative load tests use a dedicated performance environment or an
explicitly approved, isolated pre-production deployment with production-like:

- API and web App Service runtime and scaling configuration;
- Azure SQL schema, indexes, statistics, tier, and connection settings;
- VNet, private endpoints, DNS, Key Vault, Blob Storage, and Managed Identity;
- telemetry sampling and Azure Monitor correlation;
- background workers, queues, rate limits, and caches relevant to the scenario;
  and
- representative synthetic data volume and distribution.

Performance infrastructure, identities, data, DNS, telemetry, budgets, and
results remain separate from production. A cheaper tier may be used to learn
relative behavior, but its results must name the tier and cannot be extrapolated
silently.

Load testing production requires separate written approval for scope, time,
maximum traffic, cost, data handling, monitoring, abort conditions, and incident
ownership. It is prohibited by default.

## Workload Models

Workloads represent complete user behavior rather than repeating only a health
endpoint. Initial API and Companion journeys include:

- application startup followed by Adventure listing;
- current, committed, planned, and completed Adventure selection;
- Adventure detail, Today and Next, itinerary, readiness, and Playbook reads;
- conditional `If-None-Match` requests and `304` responses;
- pagination and maximum allowed itinerary/read-model shapes;
- authorized media metadata and protected Resource delivery as a separate
  binary workload;
- authentication/token validation using approved synthetic identities;
- safe unknown, revoked, cross-Creator, cross-traveler, and rate-limited paths;
- push-driven synchronized refresh without treating push as state;
- offline reconnect bursts and retry/backoff behavior; and
- later device registration, acknowledgment, poll, task, calendar, breadcrumb,
  and upload commands when those capabilities exist.

Web workloads cover public Creator hosts, shared slugs, Adventure and
Destination rendering, static/public Resource delivery, and authenticated
workspace journeys. Planning workloads cover authorized query/mutation mixes,
optimistic concurrency, audit/outbox writes, and archival behavior.

Tests preserve realistic read/write ratios, user pacing, cache state, data
cardinality, and regional latency. A single hot record, pre-warmed cache, or
unrealistic zero-think-time loop is documented as a specialized scenario rather
than ordinary user load.

## Data and Security

- Use fictional synthetic Creators, users, travelers, plans, Resources, tokens,
  and content. Never clone production customer data into a load environment.
- Synthetic identities exercise Creator and traveler isolation; authorization
  is not bypassed for throughput.
- Tokens, cookies, authorization codes, private keys, response bodies,
  confirmation details, signed URLs, precise locations, and protected content
  do not enter scripts, logs, metrics, artifacts, or result names.
- Test data is deterministically seedable, versioned, bounded, and cleaned or
  retained under an explicit policy.
- Stress must not exhaust shared services, external providers, audit storage,
  notification destinations, or paid AI services. External dependencies use
  approved fakes unless exercising them is the explicit reviewed purpose.
- Cross-Creator leakage, lost audit intents, duplicated commands, and corrupted
  projections fail the test regardless of latency or throughput.

## Measurements

Each relevant test records:

- requests, operations, and completed user journeys per second;
- p50, p95, and p99 latency by route template or operation;
- success, safe denial, validation, timeout, cancellation, and server-error
  rates;
- App Service instance count, CPU, memory, restart, scale-out, and recovery;
- Azure SQL CPU/DTU or vCore use, duration, waits, blocking, deadlocks,
  connection-pool pressure, and query-plan evidence;
- dependency, cache, queue, outbox, notification, and Resource-delivery behavior;
- response and transfer sizes, compression, allocations, GC, and thread-pool
  pressure where observable;
- rate-limit, retry, circuit-breaker, and backpressure behavior;
- test and projected infrastructure cost; and
- post-load recovery to the approved healthy baseline.

Metrics use bounded dimensions. Load-test user, Creator, Adventure, Resource,
raw URL, token, and arbitrary test-data values do not become metric dimensions.
Detailed synthetic correlation remains in access-controlled, retention-bounded
diagnostic evidence.

## Initial Alpha Engineering Objectives

These objectives are provisional internal gates, not customer-facing service
levels or contractual SLAs. They apply to ordinary JSON read endpoints under the
approved alpha baseline workload and production-like environment:

- p95 server latency no greater than 750 ms;
- p99 server latency no greater than 1.5 seconds;
- unexpected HTTP 5xx and timeout rate below 1 percent;
- zero unauthorized data disclosures or cross-Creator/traveler results;
- zero lost required audit intents or accepted duplicate mutations;
- no sustained unbounded memory, thread, connection, queue, or retry growth;
- predictable `429` behavior with safe retry guidance after the approved limit;
- recovery to healthy steady state after a spike or transient failure without
  manual process restart; and
- dependency and compute utilization remain below the approved saturation
  threshold for the duration of the baseline and peak test.

Health endpoints, protected downloads, large Playbooks, writes, background jobs,
and external providers receive operation-specific objectives. Final thresholds
are established from measured baselines and approved before limited external
alpha. A threshold change is reviewed and recorded; CI is not weakened merely
to make a regression pass.

## Cadence and Gates

### Pull Requests

Run deterministic contract, bounds, large-shape, serialization, mapping, and
performance-smoke tests. Keep them short and stable. Do not use noisy
machine-dependent wall-clock assertions as ordinary correctness tests.

### Main and Scheduled Runs

Run a controlled low-cost deployed smoke after material API or infrastructure
changes when the performance environment exists. Schedule baseline or soak
tests according to change frequency, cost, and risk rather than on every commit.

### Release and Production Promotion

Before limited external alpha and material performance-sensitive releases:

1. deploy the exact immutable candidate to the approved environment;
2. validate health, release identity, startup, migrations, and synthetic data;
3. run baseline, expected peak, and recovery tests;
4. compare against approved thresholds and the previous accepted release;
5. retain configuration, results, server metrics, and release SHA;
6. investigate regressions rather than averaging them away; and
7. keep promotion and rollback decisions explicit.

Load tests do not replace functional, security, accessibility, resilience,
authorization, or SQL integration gates.

## Cost and Failure Controls

- Every cloud test has a maximum engine count, duration, virtual-user/rate limit,
  budget owner, and automatic-stop policy.
- A high error rate, wrong target, missing authorization, runaway response size,
  or dependency incident stops the test safely.
- Schedules avoid accidental overlapping runs and abandoned environments.
- Results and artifacts have bounded retention.
- Alerts distinguish expected load-test activity from a real attack while
  preserving useful security detection.
- The performance environment can be stopped or removed when unused without
  losing source-controlled definitions and retained evidence.

## Reporting and Review

Each accepted run produces a sanitized report containing release SHA,
environment fingerprint, workload version, data profile, duration, concurrency,
thresholds, results, bottlenecks, cost, deviations, and disposition. Trend
reports compare compatible runs only. A faster result caused by fewer checks,
less data, disabled authorization, missing audit, higher infrastructure cost, or
different cache state is not accepted as an improvement without disclosure.

Review this architecture when adding a public API, production environment,
database tier, cache, queue, external provider, protected Resource path, mobile
sync protocol, background processor, multi-region deployment, or customer-facing
service-level commitment.

## Definition of Done

- Source-controlled test plans represent approved critical journeys.
- Synthetic data proves Creator/traveler isolation and maximum supported shapes.
- CI performance smoke is deterministic and bounded.
- A production-like Azure test captures client and server metrics.
- Initial thresholds, cost caps, abort conditions, and owners are approved.
- GitHub Actions authentication is least-privilege and secretless where
  supported.
- Results are retained, comparable, privacy-safe, and linked to the release.
- Saturation fails safely and the platform recovers predictably.
- Load success never masks correctness, authorization, audit, or privacy failure.

