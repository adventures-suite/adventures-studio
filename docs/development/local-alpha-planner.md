# Local authenticated Alpha Planner

This workflow runs the private Planner through its normal cookie session,
Creator membership, instance authorization, commands, persistence, and audit
paths. It is deliberately limited to the exact `Development` environment, a
fixed synthetic user, and the disposable `AdventuresSuiteLocalAlpha` database.
It must never be aimed at Azure SQL or a shared database.

## Prerequisites

- .NET SDK 10.0.303 (selected by `global.json`)
- Docker Desktop with a current SQL Server 2022 image
- `sqlcmd` 18 or later
- a current browser
- a locally trusted ASP.NET Core HTTPS development certificate

From the repository root, trust the certificate once:

```sh
dotnet dev-certs https --trust
```

Choose local-only passwords in the current shell. Do not commit them:

```sh
export LOCAL_ALPHA_SA_PASSWORD='choose-a-strong-local-only-SA-password'
export LOCAL_ALPHA_APP_PASSWORD='choose-a-different-local-only-app-password'
```

## Start disposable SQL and apply migrations

Create the isolated container and empty database:

```sh
docker run --name adventures-suite-local-alpha-sql \
  --platform linux/amd64 \
  -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD="$LOCAL_ALPHA_SA_PASSWORD" \
  -p 14333:1433 \
  -d mcr.microsoft.com/mssql/server:2022-latest

sqlcmd -S localhost,14333 -U sa -P "$LOCAL_ALPHA_SA_PASSWORD" \
  -C -b -Q 'CREATE DATABASE AdventuresSuiteLocalAlpha'
```

Apply the reviewed migrations with the existing migration runner and the DDL
identity. The web application never invokes this command. Migration `0010`
has a deliberately separate administrator-created, authority-free role
prerequisite, so a clean database uses the following two-stage sequence.

First run the migrator. Scripts `0001` through `0009` commit, and `0010` then
fails closed because the Companion policy runtime role does not exist yet:

```sh
export ADVENTURESSUITE_SQL_CONNECTION_STRING="Server=localhost,14333;Database=AdventuresSuiteLocalAlpha;User ID=sa;Password=$LOCAL_ALPHA_SA_PASSWORD;Encrypt=True;TrustServerCertificate=True"
dotnet run --project src/AdventuresSuite.DatabaseMigrator -- --migrate
```

Confirm that the journal stopped at exactly `0009`, then create only the fixed,
empty role in this disposable local database. This local `sa` operation does
not replace or authorize the separately reviewed private-Azure administrator
workflow:

```sh
sqlcmd -S localhost,14333 -U sa -P "$LOCAL_ALPHA_SA_PASSWORD" \
  -C -b -d AdventuresSuiteLocalAlpha -Q \
  "IF (SELECT COUNT(*) FROM dbo.AdventuresSuiteSchemaVersions)<>9
       THROW 51000, 'Expected exact 0009 state.',1;
   IF DATABASE_PRINCIPAL_ID(N'AdventuresSuiteCompanionPolicyRuntime') IS NOT NULL
       THROW 51000, 'Policy role unexpectedly exists.',1;
   CREATE ROLE AdventuresSuiteCompanionPolicyRuntime AUTHORIZATION dbo;"
```

Run the same migrator again. Migration `0010` independently verifies the role
owner, type, memberships, and permissions before applying `0010` through the
latest reviewed migration:

```sh
dotnet run --project src/AdventuresSuite.DatabaseMigrator -- --migrate
```

Require the final journal count and latest script to match the repository's
authoritative migration catalog before provisioning the application identity.
For the current catalog the expected state is 12 scripts ending in
`0012_create_planner_footstep_applications.sql`.

Provision a distinct DML login into only the three migrated runtime roles:

```sh
sqlcmd -S localhost,14333 -U sa -P "$LOCAL_ALPHA_SA_PASSWORD" -C -b \
  -v DatabaseName=AdventuresSuiteLocalAlpha \
     ApplicationLogin=adventures_alpha_app \
     ApplicationPassword="$LOCAL_ALPHA_APP_PASSWORD" \
  -i tools/local-alpha/provision-application-login.sql
```

## Run the bounded bootstrap

The bootstrap has no request inputs. It accepts only the compiled synthetic
user `user_local_alpha_planner`, Creator `creator_local_alpha`, and one active
`Planner` membership with no extra permission grants. Repeating the command is
an exact no-op after it verifies those rows. Divergent state fails closed.

```sh
export ASPNETCORE_ENVIRONMENT=Development
export ADVENTURESSUITE_LOCAL_ALPHA_ENABLED=true
export ADVENTURESSUITE_LOCAL_ALPHA_APP_CONNECTION_STRING="Server=localhost,14333;Database=AdventuresSuiteLocalAlpha;User ID=adventures_alpha_app;Password=$LOCAL_ALPHA_APP_PASSWORD;Encrypt=True;TrustServerCertificate=True"
dotnet run --project src/AdventuresSuite.LocalAlphaBootstrap -- --bootstrap
dotnet run --project src/AdventuresSuite.LocalAlphaBootstrap -- --bootstrap
```

## Start and sign in

Development authentication requires both the exact environment and explicit
enablement. All identity facts below are server configuration; headers, URLs,
forms, cookies, and claims cannot select another identity.

```sh
export Authentication__Mode=Development
export Authentication__Development__Enabled=true
export Authentication__WorkspaceOrigin=https://localhost:7041
export Authentication__ProviderId=local_alpha_development
export Authentication__AbsoluteSessionLifetime=08:00:00
export Authentication__IdleSessionTimeout=00:30:00
export Authentication__ActivityTouchInterval=00:05:00
export Authentication__CircuitRevalidationInterval=00:05:00
export Authentication__SqlDatabaseName=AdventuresSuiteLocalAlpha
export Authentication__SqlConnectionString="$ADVENTURESSUITE_LOCAL_ALPHA_APP_CONNECTION_STRING"
export Authentication__Development__Issuer=https://identity.localhost/adventures-suite
export Authentication__Development__Subject=local-alpha-planner
export Authentication__Development__UserId=user_local_alpha_planner
export Authentication__Development__ExternalIdentityId=identity_local_alpha_planner
dotnet run --project src/TheSimontonAdventures.Web --launch-profile https
```

Open
`https://localhost:7041/workspace/creators/creator_local_alpha/plans`, choose
**Sign in as the local alpha planner**, and create all fictional plan content
through the ordinary Planner forms. The sign-in POST issues the same protected,
server-validated application session used after External ID; Creator and plan
authorization are not bypassed.

## Deterministic reset and cleanup

To reset all plans, sessions, bootstrap rows, and audit evidence, remove the
entire disposable container and repeat the setup:

```sh
docker rm -f adventures-suite-local-alpha-sql
unset LOCAL_ALPHA_SA_PASSWORD LOCAL_ALPHA_APP_PASSWORD
unset ADVENTURESSUITE_SQL_CONNECTION_STRING
unset ADVENTURESSUITE_LOCAL_ALPHA_ENABLED
unset ADVENTURESSUITE_LOCAL_ALPHA_APP_CONNECTION_STRING
unset Authentication__Mode Authentication__Development__Enabled
unset Authentication__WorkspaceOrigin Authentication__ProviderId
unset Authentication__AbsoluteSessionLifetime Authentication__IdleSessionTimeout
unset Authentication__ActivityTouchInterval Authentication__CircuitRevalidationInterval
unset Authentication__SqlDatabaseName Authentication__SqlConnectionString
unset Authentication__Development__Issuer Authentication__Development__Subject
unset Authentication__Development__UserId Authentication__Development__ExternalIdentityId
```

No Azure resource, production identity configuration, shared SQL database, or
real traveler record participates in this workflow.
