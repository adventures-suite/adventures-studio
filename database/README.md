# AdventuresSuite Database Migrations

Planning, authentication, Creator-membership, and audit schema changes use
ordered, forward-only SQL scripts executed by the standalone
`AdventuresSuite.DatabaseMigrator` project.

## Conventions

- Name scripts `NNNN_description.sql` with a unique, increasing four-digit number.
- Treat every deployed script as immutable.
- Correct a deployed schema with a new migration; never edit migration history.
- Keep scripts safe for one transactional execution whenever SQL Server permits.
- Do not add automatic destructive down-migrations.
- Keep Creator identity in every Creator-owned table, key, foreign key, and
  index.
- Do not add Content Engine or AI proposal foreign keys to the Planning schema.
- Never execute migrations from web application startup.

DbUp embeds the scripts in the migrator artifact, executes them in ordinal name
order, and journals successful scripts in `dbo.AdventuresSuiteSchemaVersions`.
Running the migrator again applies only scripts absent from that journal.

## Connection Configuration

The migrator reads `ADVENTURESSUITE_SQL_CONNECTION_STRING`. Supply it through a
local environment variable or protected deployment configuration. Do not commit
credentials or pass secrets as command-line arguments.

The migration identity requires bounded DDL permission for application schemas
and the migration journal. The web application's Managed Identity uses a
separate runtime principal with the minimum DML roles required by each adapter.
Creator membership rows cannot be hard-deleted; role and permission assignment
rows may be replaced transactionally. Required membership audit evidence is
insert-only for runtime mutations. The runtime identity receives no
schema-management permission and no DbUp-journal write permission. Exact Azure
SQL grants must be reviewed before deployment integration is enabled.

## Deployment Networking

Deployment integration is intentionally blocked until the separately reviewed
one-job ephemeral Azure VM runner exists in the development VNet. It must use
the attested self-contained package, migration managed identity, private DNS,
and the Azure SQL private endpoint, then be independently deleted. Public SQL,
temporary firewall rules, persistent runners, and GitHub-hosted runners
connecting directly to private SQL are prohibited.
