# Azure SQL Bootstrap and Migration Runbook

**Status:** Slice 5F Operational Runbook

**Last Updated:** August 8, 2026

## Scope

Bootstrap least-privilege contained users and execute AdventuresSuite DbUp
migrations against the private development Azure SQL database without granting
the web application DDL authority.

## Target

- Server: `adventures-suite-dev-sql`
- Database: `AdventuresSuiteDevelopment`
- Authentication: Microsoft Entra only
- Public network access: disabled
- Private DNS: `privatelink.database.windows.net`

Use the standard logical server hostname. Never configure `10.40.1.4` directly.

## Required Identities

- Application principal/object ID:
  `43f88b68-e853-4ece-9379-bd2079af8ec0`
- Migration principal/object ID:
  `ce76a652-2741-4324-8a1c-18f25409dee0`
- One-time operator: approved SQL Microsoft Entra administrator

Generated IDs are verified against live Azure resource identities immediately
before bootstrap. Names and object IDs must agree; scripts do not trust copied
IDs alone.

## Private Execution Path Gate

No bootstrap or migration begins until an approved execution environment can:

- resolve the SQL logical hostname to the VNet private endpoint;
- authenticate the intended human or workload through Microsoft Entra;
- retrieve the immutable migration package without a shared key or public Blob;
- emit access-controlled logs and exit evidence; and
- be disabled or removed after the operation.

A normal GitHub-hosted runner is not assumed to meet this gate. Select and
document a private self-hosted/ephemeral runner or another reviewed Azure-native
execution mechanism. Do not temporarily open SQL public networking as an
undocumented shortcut.

## One-Time Contained-User Bootstrap

The approved Entra administrator connects through the private path and:

1. confirms the target server and database;
2. creates contained users for the current application and migration Managed
   Identities using their external provider identities;
3. grants application `CONNECT` and only documented schema-scoped runtime DML;
4. grants migration `CONNECT`, approved development `db_ddladmin`, and required
   migration-journal/data permissions;
5. denies or omits `db_owner`, user/role administration, server-level roles,
   and cross-database authority;
6. verifies effective permissions by impersonation or separate workload tests;
7. records sanitized grants and current principal IDs; and
8. removes any temporary operator or execution-path elevation.

Bootstrap SQL is source-controlled, parameterized by resolved identity, reviewed,
idempotent where safe, and never contains a password or access token.

## Migration Artifact

Use the exact published `AdventuresSuite.DatabaseMigrator` artifact produced for
the intended full commit SHA. Do not rebuild an old revision, use `latest`, or
copy loose migration scripts manually.

Before execution verify:

- artifact name, full SHA, workflow run, and checksum;
- ordered embedded migration catalog;
- expected previous and target journal state;
- database and environment identity;
- migration app Managed Identity;
- package retention and rollback/recovery evidence; and
- no secrets or connection passwords in the package.

## Migration App Procedure

1. Confirm the migration app is stopped.
2. Confirm its public ingress remains disabled and VNet/DNS resolution passes.
3. Deploy the exact immutable migrator artifact through the approved private or
   Azure-native package path.
4. Configure only non-secret target server/database and release identity.
5. Start the migration app through the Azure control plane.
6. Acquire an Azure SQL token using the migration app's own Managed Identity.
7. Acquire an application lock so only one migrator executes for the database.
8. Run DbUp once using per-script transaction and
   `dbo.AdventuresSuiteSchemaVersions` journal behavior.
9. Record script identifiers, safe outcome, duration, target version, release
   SHA, and support identity without SQL text containing data or credentials.
10. Run the approved post-migration validation.
11. Stop the migration app even when migration or validation fails.
12. Verify stopped state, revoke temporary package access, and retain evidence.

The migration app must not host a customer endpoint or remain continuously
running.

## Failure and Recovery

- Failure before script commit leaves the per-script transaction rolled back.
- A successful forward-only script is not automatically reversed.
- Recovery uses backup/restore, point-in-time recovery, or an approved corrective
  forward migration; never edit the DbUp journal to pretend success.
- Do not run the web application against a schema outside its supported range.
- Stop promotion and application enablement when journal, schema validation,
  permissions, or migration evidence is ambiguous.
- Preserve the failed artifact, journal snapshot, safe diagnostics, operator,
  workload identity, and timestamps for investigation.

## Verification Matrix

- application identity can perform approved repository DML;
- application identity cannot create/alter/drop schema objects or modify the
  DbUp journal;
- migration identity can execute the ordered catalog and journal it;
- migration identity is not `db_owner` and has no application runtime role;
- clean migration, repeated migration, and upgrade from the previous schema
  pass;
- exact case-sensitive external identity constraints pass;
- transaction, concurrency, rollback, archive/session, and permission tests
  pass;
- public SQL networking remains disabled; and
- migration app returns to stopped-by-default state.

## Identity Rotation

When an App Service or system-assigned identity is recreated, its principal
changes. Treat this as a controlled database-access migration: resolve the new
identity, create and verify the new contained user, deploy/test, then revoke the
old user. Never assume a resource name preserves a system-assigned identity.
