# AdventuresCompanion API Development Deployment

**Status:** Development host provisioned; product activation disabled

**Last updated:** August 10, 2026

## Purpose and Boundary

This runbook records the separate development deployment boundary for
`AdventuresSuite.Api`. It does not activate production OAuth, SQL access,
protected Resource delivery, media routes, mutation routes, or the deterministic
Test adapter. The web application remains independently deployable.

Automatic development deployments originate only from `main`. The API workflow
also supports an explicit `workflow_dispatch` so an operator can deploy or roll
back an exact retained revision. A feature branch does not deploy automatically.

## Approved Azure Inventory

| Property | Development value |
| --- | --- |
| Subscription | `5ace9cdd-06d1-47d9-8214-1e7c756d076a` |
| Resource group | `rg-adventures-suite-dev` |
| Region | West US 2 |
| App Service plan | `asp-adventures-suite-api-dev` |
| Plan SKU | Linux B1, one worker |
| App Service | `adventures-suite-api-dev` |
| Exact origin | `https://adventures-suite-api-dev.azurewebsites.net` |
| Liveness | `/health/live` |
| Readiness | `/health/ready` |
| VNet integration | `vnet-adventures-suite-dev/snet-appservice-integration` |
| Runtime Managed Identity principal | `9c886d97-4ea7-4b73-aa19-679051285483` |
| Deployment identity | `oidc-adventures-suite-api-dev` |
| Deployment identity client ID | `91d49097-719d-44ae-9d8c-c394a68781e3` |
| Product activation | `Disabled` |

The dedicated plan adds an estimated USD $12.41 per month at the observed
Linux B1 retail rate of USD $0.017 per hour and 730 hours per month. This is an
estimate, not a contractual quote. Data transfer, monitoring, taxes, and future
scale changes can add cost.

The public endpoint is HTTPS-only. Minimum site and SCM TLS are 1.2, HTTP/2 and
Always On are enabled, FTP is disabled, and both FTP and SCM basic publishing
credentials are denied. Outbound traffic uses the approved App Service
integration subnet. SQL, Key Vault, and Blob public access remain disabled.

## Identity and Permission Boundary

GitHub authenticates through the dedicated user-assigned identity and the exact
GitHub `dev` Environment subject:

```text
repo:ssimonton007@55812276/adventures-studio@1317655952:environment:dev
```

The issuer is `https://token.actions.githubusercontent.com` and the audience is
`api://AzureADTokenExchange`. These are workload-federation identifiers, not
credential material. The deployment identity has `Website Contributor` only at
the `adventures-suite-api-dev` App Service scope. It has no subscription or
resource-group Contributor assignment and no SQL, Key Vault, Blob, migration,
or application-runtime permission.

The API's system-assigned Managed Identity is distinct from the deployment
identity and currently has no Azure role assignments. Future data access must
be separately approved and limited to the runtime DML boundary. It must never
receive migration DDL authority.

## GitHub Development Environment

The workflow reuses the existing non-secret `AZURE_TENANT_ID` and
`AZURE_SUBSCRIPTION_ID` values and requires these API-specific non-secret
variables:

| Variable | Purpose |
| --- | --- |
| `AZURE_COMPANION_API_CLIENT_ID` | Dedicated federated deployment identity |
| `AZURE_COMPANION_API_RESOURCE_GROUP` | API resource group |
| `AZURE_COMPANION_API_WEBAPP_NAME` | API App Service name |
| `AZURE_COMPANION_API_ORIGIN` | Exact HTTPS origin used by smoke tests |
| `COMPANION_API_ACTIVATION_MODE` | Must equal `Disabled` |

No repository or Environment secret is required for this workflow. Do not add
publish profiles, client secrets, connection strings, access tokens, or Azure
service keys.

## Immutable Deployment and Verification

`.github/workflows/deploy-companion-api-dev.yml` performs the following bounded
sequence:

1. checks out and verifies the exact release SHA and rejects a superseded
   automatic `main` run;
2. restores the committed NuGet lock graph;
3. builds the Release solution, runs the full and focused tests, audits
   vulnerable dependencies, and generates the OpenAPI 3.1 document;
4. publishes only `AdventuresSuite.Api`;
5. creates one ZIP named with the full SHA and run attempt and retains its
   SHA-256 checksum and generated OpenAPI document;
6. authenticates with the dedicated federated deployment identity;
7. writes the exact release SHA and explicit disabled activation settings;
8. uploads synchronously without restart and requires a new successful Azure
   deployment record;
9. explicitly restarts the App Service; and
10. requires liveness and readiness to report the exact SHA, service name,
    healthy state, and disabled activation state.

The post-deployment gate also proves that the protected Adventures listing is
fail-closed with a safe `401` problem and that OpenAPI and Scalar return `404`
under the Production environment. Health responses contain only bounded status,
service identity, full release SHA, and activation state. They contain no
dependency names, private hosts, identities, connection information, or tokens.

## Rollback

The retained build artifact is named
`adventures-companion-api-<full-sha>-<run-attempt>` and contains the exact ZIP,
its SHA-256 checksum, and the generated OpenAPI document.

1. Select a previously verified workflow run and record its full SHA, attempt,
   artifact checksum, and Azure deployment record.
2. Download that retained artifact; do not rebuild or select a mutable latest
   package.
3. Verify the checksum before upload.
4. Use `workflow_dispatch` at the selected revision, or perform the equivalent
   reviewed upload with implicit restart disabled.
5. Require a new successful Azure deployment record.
6. Explicitly restart the API App Service.
7. Verify both health probes report the selected full SHA and `Disabled`.
8. Verify the protected endpoint remains fail-closed and Production API
   documentation remains unavailable.

Record the source run, rollback run, package checksum, Azure deployment record,
restart time, and sanitized verification evidence.

## Failure Diagnostics

Failure evidence may include only the expected and safely reported SHA, package
artifact name, Azure deployment record identifier, App Service running state,
and bounded health state. Do not retain access tokens, authorization headers,
environment dumps, raw Azure CLI caches, private DTOs, SQL, precise location,
or protected media URLs.

If upload succeeds but verification fails, leave product activation disabled,
inspect the App Service deployment and application logs using support and run
identifiers, and either repair forward or execute the immutable rollback. Do
not enable basic publishing credentials or public SQL, Key Vault, or Blob
access to troubleshoot.

## Remaining Gates

- Production API infrastructure is not provisioned.
- External ID public-client OAuth with PKCE is not activated for the API.
- Creator membership, stale-version, and resource-aware authorization are not
  connected to Azure persistence.
- The runtime identity has no SQL DML grant.
- Media and protected Resource delivery remain separate and unimplemented.
- Mutation and synchronization routes remain unimplemented.
- The current live resources require reconciliation into reviewed
  infrastructure as code; Azure live state is not the reproducible source of
  truth.

These gates require separate reviewed slices. None may be inferred from a
healthy host deployment.
