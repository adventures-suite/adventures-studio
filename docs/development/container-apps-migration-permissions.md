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

## Development sole-maintainer governance exception

**Accepted development risk:** AdventuresSuite is currently a personal GitHub
repository whose only collaborator is owner `ssimonton007`. Independent GitHub
Environment review is unavailable. Do not invite an untrusted person, create a
second owner-controlled account, or describe sole-maintainer approval as
independent review. Prevention of self-review is not currently enforceable and
is not part of the development configuration below.

Development-only federation and infrastructure operations may proceed under an
explicit approval from the sole maintainer. Every approval and operation must
record the approving human operator and executing GitHub workload identity.
Exact protected-main SHA binding, required GitHub Environment approval, no
administrator bypass, narrow permissions, a short stated operational window,
sanitized evidence retention, immediate privilege revocation, and independent
loss-of-access verification remain mandatory. Any unexpected or inconclusive
condition stops the operation.

In this exception, "independent loss-of-access verification" means a separate
technical check performed with a fresh token and fresh session after
revocation. It does not imply or require a second human reviewer while the
documented sole-maintainer exception remains in effect.

This exception does not represent the target production posture. Production
provisioning, production migration, customer-data operations, and general
availability remain blocked until a real independent reviewer is established.
Moving the repository into a GitHub organization with durable reviewer/team and
branch-governance controls must be reassessed before alpha or beta operational
scope expands.

Workflow paths are reviewed policy controls, but an environment-based Entra
subject does not encode a workflow filename. Actual controls are protected
`main`, Environment approval, no administrator bypass, exact source-SHA checks
inside each workflow, and branch protection preventing unauthorized workflow
changes.

## Prepared GitHub Environment configuration approval packet

**Status: draft; not approved and not executed.** Repository review basis is PR
#18 head `935dd6b7c1b52c51ecae1aaed9c2092310a366da`. Its historical regular-merge
main SHA is `91e889b9b702d8d280a6a449688c4a427ed3c7de`; this historical value is not a
permanent workflow release SHA and does not authorize an operation.

Create exactly two GitHub Environments with protected `main` as the only allowed
deployment branch, owner `ssimonton007` as the required development approver,
and administrator bypass disabled. Because the approver is also the repository
owner, self-review prevention must be disabled; this is the accepted
development-only sole-maintainer risk above. Add no secrets.

`migration-foundation-deployment` variables:

- `MIGRATION_FOUNDATION_DEPLOYER_CLIENT_ID=223af00d-69e5-4302-9ac5-6b338f3ea2e5`
- `MIGRATION_FOUNDATION_DEPLOYER_PRINCIPAL_ID=b77b6201-ad26-4f77-8f88-6d0d43f7dbb8`
- `AZURE_SUBSCRIPTION_ID=5ace9cdd-06d1-47d9-8214-1e7c756d076a`
- `WORKFORCE_TENANT_ID=d7add2bb-ac03-49a8-9377-d0bf6a012f2f`
- `MIGRATION_RESOURCE_GROUP=rg-adventures-suite-dev`

`migration-rbac-deployment` variables:

- `MIGRATION_RBAC_DEPLOYER_CLIENT_ID=d678e2ad-ada2-4cde-bb79-44630acf1cc8`
- `MIGRATION_RBAC_DEPLOYER_PRINCIPAL_ID=822c1c0c-39e1-400f-b9fc-9532a11bae5d`
- `AZURE_SUBSCRIPTION_ID=5ace9cdd-06d1-47d9-8214-1e7c756d076a`
- `WORKFORCE_TENANT_ID=d7add2bb-ac03-49a8-9377-d0bf6a012f2f`
- `MIGRATION_RESOURCE_GROUP=rg-adventures-suite-dev`

Verify exact names, variables, protected-main restriction, required approver,
self-review setting, disabled administrator bypass, and absence of secrets or
unapproved variables. Record the configuring GitHub administrator and UTC
window. Stop without repair on drift or inconclusive configuration evidence.
This packet authorizes no Azure or Entra operation and no workflow dispatch.

## Prepared Entra deployer federation approval packet

**Status: draft; not approved and not executed.** This packet may be considered
only after the GitHub configuration above is independently read back and matches
exactly. It creates no Azure role and performs no Bicep deployment.

Common values:

- Issuer: `https://token.actions.githubusercontent.com`
- Audience: `api://AzureADTokenExchange`
- Repository owner: `ssimonton007`
- Repository owner ID: `55812276`
- Repository: `adventures-studio`
- Repository ID: `1317655952`
- Subscription: `5ace9cdd-06d1-47d9-8214-1e7c756d076a`
- Tenant: `d7add2bb-ac03-49a8-9377-d0bf6a012f2f`
- Resource group: `rg-adventures-suite-dev`
- Required workflow release SHA: `<EXACT_CURRENT_MAIN_SHA>`

Read-only GitHub OIDC evidence recorded on 2026-08-11 showed
`use_default=true`, `use_immutable_subject=false`, no custom
`include_claim_keys` template, and effective default prefix
`repo:ssimonton007@55812276/adventures-studio@1317655952`. The explicit opt-in
flag is therefore not enabled, but this repository's effective GitHub default
is the immutable owner/repository-ID format. A live federation-proof assertion
confirmed the same prefix. The immutable IDs protect against owner or repository
namespace reuse and GitHub does not permit removing them from an immutable
subject. Approval and Entra trust records must use the effective subject
verbatim; the legacy name-only form is prohibited.

Immediately before execution, the approval record must resolve
`<EXACT_CURRENT_MAIN_SHA>` to the then-current full lowercase 40-character SHA
of protected `main`. The approved SHA, workflow `release_sha` input, and
`github.sha` must match that resolved value exactly. Any new commit on `main`
invalidates the approval and requires a new read-only review and explicit
approval for the new SHA.

| Purpose | Identity resource ID | Principal ID | Client ID | Credential | Subject | Workflow |
| --- | --- | --- | --- | --- | --- | --- |
| Foundation/Job resources | `/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migration-foundation-deployer-dev` | `b77b6201-ad26-4f77-8f88-6d0d43f7dbb8` | `223af00d-69e5-4302-9ac5-6b338f3ea2e5` | `github-migration-foundation-deployment` | `repo:ssimonton007@55812276/adventures-studio@1317655952:environment:migration-foundation-deployment` | `.github/workflows/provision-migration-foundation-resources.yml` |
| Access resources | `/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migration-rbac-bootstrap-dev` | `822c1c0c-39e1-400f-b9fc-9532a11bae5d` | `d678e2ad-ada2-4cde-bb79-44630acf1cc8` | `github-migration-rbac-deployment` | `repo:ssimonton007@55812276/adventures-studio@1317655952:environment:migration-rbac-deployment` | `.github/workflows/provision-migration-rbac-access.yml` |

Before creation, record approving operator and executing Azure identity;
resolve and record `<EXACT_CURRENT_MAIN_SHA>` immediately before execution, and
verify the exact main SHA and GitHub configuration, identity IDs, zero roles at
every scope, zero credentials/secrets, zero attachments, and absence of both
proposed credentials. Verify publisher and starter federation remains absent.

Create only the two exact federated credentials and read them back to compare
name, issuer, single audience, and subject. Re-prove zero roles. Dispatch each
proof workflow separately with release SHA `<EXACT_CURRENT_MAIN_SHA>`, a unique
approval ID, and recorded sole-maintainer approval. At dispatch,
`github.sha`, `release_sha`, and the SHA in the approval record must be exactly
equal; a changed `main` stops and invalidates the operation. Both explicit ARM
probes must return exact
`AuthorizationFailed`; every other result fails closed. Retain bounded evidence,
then remove the two credentials immediately if the approved window or purpose
ends and verify loss of authentication using a separate fresh token and fresh
session technical check. This technical independence does not assert that a
second human reviewer participated.

Stop on any mismatch, unexpected access, role, attachment, publisher/starter
federation, workflow-source drift, inconclusive denial, cleanup failure, or
unrelated activity. Do not grant roles, deploy Bicep, publish images, access SQL,
run migrations, or change applications, networking, production, or MAUI.
