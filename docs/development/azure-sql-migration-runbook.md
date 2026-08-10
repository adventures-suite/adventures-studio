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
- Application principal/client ID:
  `21c95c0f-4855-433b-b835-9b14446276db`
- Application principal/display name: `adventures-suite-dev`
- Migration principal/object ID:
  `ce76a652-2741-4324-8a1c-18f25409dee0`
- Migration principal/client ID:
  `74fdf34f-9299-47fc-a114-099bf3d80cec`
- Migration principal/display name: `adventures-suite-migrate-dev`
- One-time operator: approved SQL Microsoft Entra administrator

Generated IDs are verified against live Azure resource identities immediately
before bootstrap. Names and object IDs must agree; scripts do not trust copied
IDs alone.

Supply the verified values only to the matching one-time operation:

- migration: `ADVENTURESSUITE_MIGRATION_PRINCIPAL_ID`,
  `ADVENTURESSUITE_MIGRATION_PRINCIPAL_CLIENT_ID`, and
  `ADVENTURESSUITE_MIGRATION_PRINCIPAL_NAME`;
- runtime: `ADVENTURESSUITE_APP_PRINCIPAL_ID`,
  `ADVENTURESSUITE_APP_PRINCIPAL_CLIENT_ID`, and
  `ADVENTURESSUITE_APP_PRINCIPAL_NAME`.

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

## Ordered Bootstrap and Migration

The approved private execution path performs four separate operations. Do not
combine them or run DbUp using administrator authority.

1. The approved Entra administrator confirms the exact target, supplies
   `ADVENTURESSUITE_ADMIN_SQL_CONNECTION_STRING` and the verified migration
   principal object ID, client ID, and exact display name, and runs
   `--bootstrap-sql`. This creates the migration contained user and the empty
   `AdventuresSuiteAuthenticationRuntime` and
   `AdventuresSuiteMembershipRuntime` database roles, and grants `CONNECT`,
   `db_ddladmin`, `db_datareader`, and `db_datawriter` for development
   migrations. Pre-creating runtime roles under administrator authority keeps
   role administration away from the migration identity. The bootstrap also
   assigns the migration user an explicit `dbo` default schema so Microsoft
   Entra workload authentication can create the source-controlled schemas
   without implicit-user or group-default-schema behavior. No runtime identity
   is added to either empty role at this stage.
2. The migration workload identity supplies
   `ADVENTURESSUITE_SQL_CONNECTION_STRING` and runs `--migrate`. No
   administrator connection string is present for this operation.
3. After migrations grant the intended permissions to the authentication and
   membership runtime roles, the Entra administrator supplies the verified
   application principal object ID, client ID, and exact display name and runs
   `--bind-runtime`. This creates the runtime contained user, adds it only to
   the precreated roles, and grants `CONNECT`.
4. The migration workload identity runs `--verify-permissions`. The bounded
   verification proves its development migration roles, journal access, and
   required authentication schema without changing application data.

No operation grants `db_owner`, user/role administration to a workload,
server-level roles, or cross-database authority. Record sanitized effective
grants and current principal IDs, then remove temporary operator or
execution-path elevation.

Bootstrap SQL is source-controlled, parameterized by resolved identity, reviewed,
idempotent where safe, and never contains a password or access token.
The generated contained-user alias begins with the exact Entra display name and
appends the first five object-ID characters, as Azure SQL requires. An existing
alias must have the approved client ID in its database SID or the operation
fails closed. Principal creation and grants commit in one transaction.

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

### Private execution-channel prerequisite

Before requesting approval for a database migration, run a separately approved,
SQL-free execution-channel proof. Use an Azure Managed Run Command resource,
not interactive SSH or the transient `run-command invoke` action. Its immutable
source is `operations/verify-private-execution-channel.sh` at the reviewed SHA.

The proof must:

1. start with the migration App Service stopped;
2. receive artifact authorization, a container-scoped staging SAS, a short-lived
   operator ARM token, and a random completion HMAC key only as Managed Run
   Command `protectedParameters`;
3. use a unique private staging container in the existing private-endpoint
   storage account, with a SAS lifetime no longer than the proof window;
4. verify the package SHA-256 before staging, upload it to private Blob, remove
   the first copy, download through the private endpoint, and verify it again;
5. obtain App Service publishing credentials transiently through the ARM token,
   invoke only private SCM, and execute `run-execution-channel-proof.sh`;
6. make no SQL connection and emit `sqlAccessAttempted: false`;
7. return the signed completion envelope in bounded Managed Run Command output
   and store the identical envelope in the protected evidence blob;
8. independently verify the HMAC, operation ID, release SHA, artifact checksum,
   real exit code, `Complete` classification, evidence checksum, and timestamps;
   and
9. remove the SCM work directory, staged package blob, VM files, protected
   parameters/run-command resource, SAS, and temporary container, then restore
   stopped/deallocated states.

Fail closed if output is absent or truncated, the envelopes differ, any
signature/checksum differs, cleanup is inconclusive, or SQL access is observed.
A successful proof authorizes no migration.

## Migration App Procedure

1. Confirm the migration app is stopped.
2. Confirm its public ingress remains disabled and VNet/DNS resolution passes.
3. Deploy the exact immutable, self-contained `linux-x64` migrator package
   through the approved private or Azure-native package path. The package must
   include `run-reviewed-migration-operation.sh` and the evidence-capable
   executable under one reviewed SHA-256 checksum.
4. Configure only non-secret target server/database and release identity.
5. Use a unique operation ID and a bounded 60-to-1800-second timeout. Invoke the
   reviewed wrapper only through the private migration App Service execution
   path; do not depend on the App Service web-startup probe to infer completion.
6. Acquire an Azure SQL token using the migration app's own system-assigned
   Managed Identity. Before DbUp, validate safe token tenant, audience,
   object/client identity metadata and require SQL to confirm the expected
   contained migration principal. Never print or retain the token.
7. Acquire the zero-wait `AdventuresSuite.DatabaseMigrator` application lock
   and hold it across pre-state capture, DbUp, and post-state capture. Reject a
   second active operation and stop if the journal is not exactly the approved
   pre-state.
8. Run the reviewed operation once using per-script transaction and
   `dbo.AdventuresSuiteSchemaVersions` journal behavior.
   Source-controlled application schemas are owned by the stable
   `db_ddladmin` database role. The migration principal is already a member of
   that role, so schema creation does not require the unsafe
   `IMPERSONATE dbo` permission or bind ownership to a rotating workload user.
9. Record process start/completion, real wrapper and migrator exit status,
   operation ID, script identifiers, safe outcome, duration, target version,
   release SHA, package checksum, identity evidence, schema/permission evidence,
   and before/after application fingerprints without SQL text containing data
   or credentials. A committed `0007` followed by failed `0008` is a recoverable
   stopped state, not an all-or-nothing rollback and not permission to rerun.
10. Complete administrator `--bind-runtime`, then run workload
    `--verify-permissions` as separate operations.
11. Stop the migration app even when migration or validation fails.
12. Verify stopped state, revoke temporary package access, and retain evidence.

The wrapper's exit trap records the original process exit code and attempts
cleanup without replacing that code. Do not automatically rerun an operation
that remains at `0006` or stops at `0007`; retain evidence and obtain a new
approval after diagnosis.

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
