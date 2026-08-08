# AdventuresSuite Database Migrations

Planning schema changes use ordered, forward-only SQL scripts executed by the
standalone `AdventuresSuite.DatabaseMigrator` project.

## Conventions

- Name scripts `NNNN_description.sql` with a unique, increasing four-digit number.
- Treat every deployed script as immutable.
- Correct a deployed schema with a new migration; never edit migration history.
- Keep scripts safe for one transactional execution whenever SQL Server permits.
- Do not add automatic destructive down-migrations.
- Keep Creator identity in every Planning table, key, foreign key, and index.
- Do not add Content Engine or AI proposal foreign keys to the Planning schema.
- Never execute migrations from web application startup.

DbUp embeds the scripts in the migrator artifact, executes them in ordinal name
order, and journals successful scripts in `dbo.AdventuresSuiteSchemaVersions`.
Running the migrator again applies only scripts absent from that journal.

## Connection Configuration

The migrator reads `ADVENTURESSUITE_SQL_CONNECTION_STRING`. Supply it through a
local environment variable or protected deployment configuration. Do not commit
credentials or pass secrets as command-line arguments.

The migration identity requires bounded DDL permission for the Planning schema
and migration journal. The web application's Managed Identity uses a separate
runtime principal with `SELECT`, `INSERT`, and `UPDATE` on Planning tables and
`DELETE` only on aggregate child tables needed for transactional replacement.
It receives no schema-management permission, no DbUp-journal write permission,
and no `DELETE` permission on `planning.AdventurePlans`. Exact Azure SQL grants
must be reviewed before deployment integration is enabled.

## Deployment Networking

Deployment integration is intentionally blocked until private runner access, an
Azure-hosted migration job, or a tightly bounded temporary dev firewall rule is
approved and documented. Azure SQL must not remain open to the public internet
for GitHub-hosted migrations.
