# Container Apps Migration Deployment Permissions

**Status:** six identities created; no Azure authority or federation granted

## Responsibility matrix

| Boundary | Actor | May create | Explicit exclusions |
| --- | --- | --- | --- |
| Owner bootstrap | Approved Owner | Six UAMIs, all with no initial roles — **complete 2026-08-11** | Federated credentials, roles, other resources |
| Deployer federation | Separately approved Owner | One reviewed OIDC credential on each deployer identity | Roles, deployment, publisher/starter federation |
| `foundation-resources.bicep` | Temporary infrastructure deployer | Subnet, workspace, ACR, environment; reference four existing operational identities | Identity creation/mutation, RBAC, Job, SQL |
| `identity-access.bicep` | Infrastructure deployer with exact-identity temporary grants | Publisher and starter OIDC credentials only | Other identities/resources, RBAC, SQL |
| `foundation-access.bicep` | Temporary RBAC deployer | Two ACR assignments, starter custom role, dedicated-workspace reader assignment | Ordinary resources, Job, SQL |
| Image publication | Publisher identity | Full-SHA image and registry digest evidence | IaC, Job start, SQL |
| `job-resource.bicep` | Temporary infrastructure deployer | Exact digest-bound dormant Job | RBAC, execution, SQL |
| `job-access.bicep` | Temporary RBAC deployer | Exact-Job starter assignment | Job mutation, broader assignment, SQL |
| SQL-free proof | Starter identity | One approved execution and bounded evidence | SQL token/access, migration |

The order is mandatory; each row needs separate approval and verification.
Outputs may flow only to the next reviewed boundary. IDs must match subscription
`5ace9cdd-06d1-47d9-8214-1e7c756d076a`, resource group
`rg-adventures-suite-dev`, exact type, and exact name. Generated IDs are never
hardcoded or found by display name.

The six Owner-created identities are the infrastructure deployer, RBAC deployer,
migration, pull, publisher, and starter identities. All six must initially have
zero role assignments. Creation completed on 2026-08-11 and verified zero roles,
federation, credentials, and attachments. The next separate gate may create only
the reviewed OIDC credentials on the two deployer identities; credentials for
publisher and starter remain absent until `identity-access.bicep`. Foundation
resource deployment does not create or mutate any identity.

## Deployer permissions

The infrastructure action catalog is source controlled under
`infrastructure/container-apps-migrations/roles/`. Assignment is temporary at
the development resource group and excludes role-definition and role-assignment
writes. It also excludes every Managed Identity write. Operational identity
read is granted separately only at each exact identity referenced by the named
resource template. Federated-credential read/write is granted only at the exact
publisher and starter identities for `identity-access.bicep` and is removed
immediately afterward. Provider registration, if needed, is a distinct temporary approval for
only provider read and the two exact register actions at subscription scope.

The RBAC deployer has no ordinary resource write. Role-definition authority is
resource-group scoped. Assignment authority is granted separately at each exact
target. Its assignment role includes only deployment read, write, and operation
status read at resource-group scope so it can submit and observe the access-only
templates; role-assignment writes remain scoped and conditioned at each target:

| Scope | Role | Principal |
| --- | --- | --- |
| Migration ACR | AcrPull `7f951dda-4ed3-4680-a7ca-43fe172d538d` | Foundation-output pull principal |
| Migration ACR | AcrPush `8311e382-0749-4cb8-b61a-304f252e45ec` | Foundation-output publisher principal |
| Dedicated workspace | Log Analytics Reader `73c42c96-874c-492b-b04d-ab87d138a893` | Foundation-output starter principal |
| Exact migration Job | Reviewed starter custom-role output | Foundation-output starter principal |

Each assignment-authority grant requires an Azure RBAC condition matching both
`@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId]` and
`@Request[Microsoft.Authorization/roleAssignments:PrincipalId]` to that row.
Resolved output IDs and the final condition are independently reviewed; a
placeholder is never executable approval material. Role-definition write cannot
be usefully condition-restricted, so it is isolated, time-bounded,
resource-group scoped, and never combined with ordinary resource writes.

Log Analytics Reader is necessary to query the dedicated workspace for the
exact execution's bounded completion envelope. It is read-only, reaches no
other workspace, and has no direct cost. Ingestion and 30-day retention are
already included in the cost estimate.

The custom starter role uses only `Microsoft.App/jobs/read`,
`Microsoft.App/jobs/start/action`, `Microsoft.App/jobs/execution/read`, and
`Microsoft.App/jobs/executions/read`. A live read-only provider-operation query
confirmed all four on 2026-08-10 in subscription
`5ace9cdd-06d1-47d9-8214-1e7c756d076a`. Repeat immediately before role creation;
Bicep compilation is insufficient and a missing action is a stop condition.

## Approval packets

1. **Create six unprivileged identities — COMPLETE.** An approved Owner created
   exact user-assigned identities
   `id-adventures-suite-migration-foundation-deployer-dev` and
   `id-adventures-suite-migration-rbac-bootstrap-dev`, plus the exact migration,
   pull, publisher, and starter identities and granted no roles. Completion
   evidence records the exact resource, principal,
   client, and tenant IDs and proves absence of federation, secrets, roles, and
   attachments.
2. **Federate the two deployers — NOT APPROVED.** Use the separate packet below.
   This creates no role and performs no deployment.
3. **Temporarily authorize the infrastructure deployer.** Approve one named
   resource-template deployment in a bounded window. Record assignment and
   deadline, verify plan and post-state, remove access, refresh credentials, and
   prove loss of write access. Provider registration requires a separate packet.
4. **Temporarily authorize the RBAC deployer.** Approve one named access-template
   deployment with resolved exact scopes, principals, roles, conditions, and a
   bounded window. Verify assignments and absence of broader roles, remove
   authority, refresh credentials, and prove loss of access.
5. **Deploy and clean up each boundary in sequence.** Separately approve and
   verify foundation resources, identity access, foundation access, publication,
   Job resource, and Job access. Retain source SHA, template checksum, deployment ID, inputs,
   outputs, UTC timing, post-state, and cleanup. Stop on drift, excess authority,
   ambiguous output, cleanup failure, or combined steps.

These packets do not authorize SQL, Job execution, migration, public ingress,
production changes, or retirement of the old bridge. The SQL-free proof remains
a later separate approval.

`job-access.bicep` intentionally has no checked-in development parameter file:
its custom role-definition ID does not exist until `foundation-access.bicep`
completes. A bounded parameter file may be generated from that exact deployment
output, checksum-reviewed for the separate approval, used once, and removed.

## Prepared deployer OIDC federation approval packet

**Status: draft for review; not approved and not executed.** This packet creates
only two federated identity credential child resources. It grants no Azure role,
performs no Bicep deployment, and does not create publisher or starter
federation.

Common immutable values:

- Issuer: `https://token.actions.githubusercontent.com`
- Audience: `api://AzureADTokenExchange`
- Subscription: `5ace9cdd-06d1-47d9-8214-1e7c756d076a`
- Tenant: `d7add2bb-ac03-49a8-9377-d0bf6a012f2f`
- Resource group: `rg-adventures-suite-dev`

| Purpose | Identity resource ID | Principal ID | Client ID | Proposed credential name | Exact subject | Permitted workflow path |
| --- | --- | --- | --- | --- | --- | --- |
| Foundation/Job resource deployer | `/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migration-foundation-deployer-dev` | `b77b6201-ad26-4f77-8f88-6d0d43f7dbb8` | `223af00d-69e5-4302-9ac5-6b338f3ea2e5` | `github-migration-foundation-deployment` | `repo:ssimonton007/adventures-studio:environment:migration-foundation-deployment` | `.github/workflows/provision-migration-foundation-resources.yml` only |
| Access-template deployer | `/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migration-rbac-bootstrap-dev` | `822c1c0c-39e1-400f-b9fc-9532a11bae5d` | `d678e2ad-ada2-4cde-bb79-44630acf1cc8` | `github-migration-rbac-deployment` | `repo:ssimonton007/adventures-studio:environment:migration-rbac-deployment` | `.github/workflows/provision-migration-rbac-access.yml` only |

The workflow paths are reviewed repository controls, but an environment-based
Entra subject does not encode a workflow filename. GitHub Environment protection
is therefore mandatory and independently reviewed. Create two distinct
Environments—`migration-foundation-deployment` and `migration-rbac-deployment`—
with no shared deployer client-ID variable. Each requires at least one designated
infrastructure/security reviewer, prevents self-review, allows only protected
`main`, and has no administrator bypass. Any temporary allowance for PR #18's
exact branch/ref requires separate time-bounded GitHub-administration approval
and removal evidence.

Both proof workflows fail before checkout or Azure login unless `github.ref` is
exactly `refs/heads/main`, `github.sha` equals the required lowercase
40-character `release_sha` input, and that input passes strict SHA syntax
validation. The post-checkout `HEAD` comparison is an additional integrity
invariant; it is not treated as proof of which workflow source GitHub executed.

The foundation Environment defines only
`MIGRATION_FOUNDATION_DEPLOYER_CLIENT_ID=223af00d-69e5-4302-9ac5-6b338f3ea2e5`,
`MIGRATION_FOUNDATION_DEPLOYER_PRINCIPAL_ID=b77b6201-ad26-4f77-8f88-6d0d43f7dbb8`,
`AZURE_SUBSCRIPTION_ID`, `WORKFORCE_TENANT_ID`, and
`MIGRATION_RESOURCE_GROUP`. The RBAC Environment substitutes only
`MIGRATION_RBAC_DEPLOYER_CLIENT_ID=d678e2ad-ada2-4cde-bb79-44630acf1cc8` and
`MIGRATION_RBAC_DEPLOYER_PRINCIPAL_ID=822c1c0c-39e1-400f-b9fc-9532a11bae5d`.
The shared target variables must equal the exact subscription, tenant, and
resource group above. Neither Environment contains secrets, credentials for the
other deployer, publisher/starter variables, or database/application variables.

Before execution, verify exact PR SHA, subscription, tenant, Owner object ID,
identity resource/principal/client IDs, absence of either proposed credential,
zero role assignments at all scopes, zero secrets/certificates, exact workflow
path, pinned actions, Environment protection/variables, and the corresponding
exact GitHub Environment subject. Verify that publisher and starter still have no
federated credentials. Stop on any mismatch, existing credential, role,
attachment, unexpected workflow, or environment-policy drift; do not repair,
replace, retry, grant roles, or deploy.

After the two child resources are created, read each credential back and compare
name, issuer, single audience, and subject exactly. Re-prove zero direct or
inherited role assignments for both deployer principal IDs. Authenticate each
identity only from its permitted protected workflow and run its federation-proof
mode. The ARM resource-group read and non-mutating deployment-validation
capability probe use explicit ARM URLs and must both return the exact
`AuthorizationFailed` denial. A successful HTTP result, subscription-resolution
error, authentication failure, resource-not-found response, malformed response,
network failure, or throttling is inconclusive and fails closed. No deployment
may be submitted. Confirm the activity log contains only the
two federated-credential writes and no Azure resource, role, network, SQL, or
application mutation. Any successful resource read, effective permission,
deployment ability, or inconclusive denial is a stop condition and security
review trigger.
