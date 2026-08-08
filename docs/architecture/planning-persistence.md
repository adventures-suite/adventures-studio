# Planning Engine Persistence Architecture

**Version:** 1.0

**Status:** Approved for Phase 2

**Last Updated:** August 7, 2026

## Provider Decision

Hosted Planning Engine data uses Azure SQL Database. The infrastructure adapter
uses Dapper with `Microsoft.Data.SqlClient`. These types remain outside Planning
domain and application contracts.

Azure App Service authenticates to Azure SQL with Managed Identity. Connection
configuration is environment-owned and no database credential belongs in source,
global `appsettings.json`, a Creator manifest, or Planning data.

## Local and CI Topology

Local database development uses a disposable SQL Server container. Integration
tests create an isolated database, apply committed migrations, execute the test,
and remove the database afterward. GitHub Actions uses a SQL Server service
container from the same engine family.

SQLite and in-memory repositories are not substitutes for required database
integration tests because they do not prove SQL Server migrations, filtered and
composite indexes, transactions, or concurrency behavior. Small domain and
application tests may continue to use purpose-built fakes without Dapper.

Container passwords and test connection strings are supplied through local
environment variables or CI secrets. A later implementation change will provide
the exact repeatable container command and CI service configuration when the
infrastructure adapter is introduced.

Dapper does not manage schema migrations. A standalone DbUp console application
executes ordered, forward-only SQL scripts embedded in the migrator artifact.
DbUp journals each applied script in `dbo.AdventuresSuiteSchemaVersions` and
uses one transaction per script when SQL Server permits it. Deployment and tests
run the exact same scripts. The web application never migrates during startup.

## Application Contracts

All reads, writes, and transaction creation receive explicit `CreatorId`.
Repository lookups use the logical key:

```text
CreatorId + AdventurePlanId
```

The same `AdventurePlanId` may exist for different Creators. A repository must
not first query by plan identity and check Creator ownership afterward; Creator
identity belongs in the database predicate and relevant key or index.

Updates require the expected plan version. A stale version fails with the
provider-neutral Planning concurrency exception and never silently overwrites a
newer plan. Transactions commit only complete, valid aggregates.

## Initial Schema Direction

The relational model will store the Adventure Plan aggregate and its child
records with:

- Creator identity on the aggregate and every independently queried child row
- stable strongly typed identities converted at the infrastructure boundary
- `date` columns for `DateOnly` travel values
- `time` columns for `TimeOnly` local schedule values
- IANA time-zone identifiers as validated text
- UTC audit timestamps
- a positive concurrency version
- Creator-scoped keys, foreign keys, uniqueness, and indexes

Protected reservation references, notes, budgets, and traveler planning details
remain private and must not enter diagnostics or public Content Engine queries.

## Migration and Deployment Direction

Migrations are committed and reviewed. Integration tests must prove that they
create a clean database and upgrade the previously released schema. Application
startup will not gain broad schema-owner permissions merely to apply migrations.
The deployment mechanism and least-privilege migration identity will be finalized
with the infrastructure implementation.

## Deployment Network Prerequisite

GitHub-hosted runners must not require Azure SQL to remain publicly reachable.
Before migration is added to the deployment workflow, Adventures Studio must
approve one of these bounded approaches:

1. a self-hosted or managed runner with private-network access;
2. an Azure-hosted migration job inside the approved network; or
3. for early development only, a temporary firewall rule restricted to the
   current runner and removed in an unconditional cleanup step.

No option is selected silently in application code. The migration identity gets
the required DDL permission; the App Service runtime Managed Identity receives
only required DML permission. Administrator credentials never enter web
application configuration.

## Phase 2 Exit Evidence

Phase 2 is not complete until the SQL Server adapter and migrations prove:

- Creator-scoped reads and writes
- duplicate plan identities across different Creators without leakage
- stale-update rejection
- transactional aggregate persistence
- UTC audit and date/time round trips
- clean creation and upgrade from the previous migration
- recoverable archival behavior
