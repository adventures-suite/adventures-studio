# Companion Projection Integration Runbook

**Status:** Approval-gated development operation

## Invariants

- Product activation remains `Disabled` throughout bootstrap, deployment, and
  private-connectivity validation.
- Automatic `main` deployments select the `Closed` provider.
- The deterministic provider cannot start outside `Test`.
- The Companion Managed Identity is never added to `db_owner`, `db_ddladmin`,
  `db_datareader`, `db_datawriter`, the web runtime roles, or the migration
  identity.
- No schema-level read grant is permitted. The migrated
  `AdventuresSuiteCompanionReadRuntime` role reads six explicit objects and has
  explicit mutation and schema-ALTER denials. Effective-permission checks must
  also prove that schema CONTROL remains absent; it is not explicitly denied
  because CONTROL implies SELECT and would override the approved object reads.
- Production remains disabled. The MAUI application is outside this slice.

## Required Approved Inventory

Record and independently verify these non-secret values before any mutation:

- subscription and workforce tenant identifiers;
- `rg-adventures-suite-dev` and West US 2;
- exact API App Service and its Managed Identity object/client identifiers;
- `adventures-suite-dev-sql.database.windows.net`;
- the dedicated AdventuresSuite development database;
- private endpoint address `10.40.1.4`, VNet integration, and private DNS; and
- the immutable migrator artifact SHA.

Stop if any value differs from approved inventory.

## Ordered Operations

1. Deploy migration `0008_create_companion_read_role.sql` using only the
   migration Managed Identity through the approved private execution path.
2. As the approved Entra SQL administrator, run only:

   ```text
   AdventuresSuite.DatabaseMigrator --bind-companion-read-runtime
   ```

   Supply the exact Companion API Managed Identity object ID, client ID, and
   display name through the documented environment variables. Retain sanitized
   principal, role, database, timestamp, SHA, and support identifiers only.
3. Connect as the Companion API Managed Identity and run only:

   ```text
   AdventuresSuite.DatabaseMigrator --verify-companion-read-permissions
   ```

   The verification must prove required reads and absence of Planning writes,
   DDL, broad reader/writer roles, and ownership.
4. From the API private network path, resolve the SQL FQDN to `10.40.1.4` and
   complete TLS/login plus the read-only readiness probe. Do not enable SQL
   public networking.
5. Set the GitHub `dev` Environment inventory variables for the exact SQL
   server, database, and Companion Managed Identity client ID.
6. Manually dispatch the API deployment with projection provider `Sql`.
   Activation remains `Disabled`. Verify the exact SHA, liveness, SQL-backed
   readiness, inaccessible list/detail endpoints, and unavailable
   OpenAPI/Scalar.

Authenticated list/detail data smoke, including Creator/traveler isolation,
ETags, conditional 304, and enumeration-safe 404, belongs to a later,
separately approved development-activation gate. No test header or
deterministic identity is permitted in Azure. Production remains `Disabled`.

## Rollback

Set `Companion__ProjectionProvider=Closed`, redeploy the last known-good exact
SHA, and verify readiness plus safe 401 responses. Remove the Companion
principal from `AdventuresSuiteCompanionReadRuntime` if access must be revoked.
Do not drop Planning tables, reverse immutable migrations, or broaden another
identity as a workaround.
