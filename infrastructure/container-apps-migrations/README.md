# Container Apps migration job IaC

This directory proposes the permanent development migration runner. It is not
deployed by PR validation. Before any template deployment, an Owner separately
creates six unprivileged user-assigned identities—both deployers plus migration,
pull, publisher, and starter—and verifies that all six have no role assignments.
Identity creation completed on 2026-08-11 with zero roles, federation,
credentials, or attachments. A separate approval may create the two reviewed
deployer OIDC credentials so those otherwise-unprivileged identities can later
receive temporary authority; publisher and starter credentials are not created
during bootstrap.
The workflows use distinct protected Environments:
`migration-foundation-deployment` and `migration-rbac-deployment`. They can
authenticate and prove identity/denial. Proof-only mode remains available. An
explicit checksum-bound foundation mode deploys only the reviewed resource
template, while a separate RBAC workflow handles role bootstrap, exact temporary
assignment, or unconditional cleanup as distinct approved operations. The full
protection and variable contract is documented in
`docs/development/container-apps-migration-permissions.md`.
Provisioning then has five least-privilege template
boundaries and one intervening publication boundary. Every step requires its
own approval and verification; no workflow may combine them:

1. The temporarily authorized RBAC bootstrap identity deploys
   `deployer-role-definitions.bicep` only, then separately deploys
   `foundation-temporary-access.bicep` to create the exact two assignments.
2. The temporary infrastructure deployer deploys
   `foundation-resources.bicep` only; the four operational identities are
   validated `existing` resources.
3. The RBAC boundary removes both assignments after success or failure, and a
   fresh foundation proof must again receive `AuthorizationFailed`.
4. With federated-credential write assigned only on the exact publisher and
   starter identity resources, it deploys `identity-access.bicep` only and then
   loses that identity-specific authority.
5. The temporary RBAC deployer deploys `foundation-access.bicep` only.
6. The GitHub publisher publishes the reviewed full-SHA image and resolves and
   verifies its registry-authoritative digest.
7. The temporary infrastructure deployer deploys digest-bound
   `job-resource.bicep` only.
8. The temporary RBAC deployer deploys `job-access.bicep` only.
9. Only afterward may the GitHub starter run a separately approved SQL-free
   execution-channel proof.

Resource templates contain no `Microsoft.Authorization` resources. Access
templates create no ordinary resources: named resources are `existing`, and
supplied IDs are validated against the expected subscription, resource group,
type, and exact name. Generated IDs come only from immediately preceding
reviewed deployment outputs. Display-name lookup, hardcoded generated principal
or client IDs, and unrelated deployment outputs are prohibited.

The infrastructure deployer is temporary and resource-group scoped. Its exact
catalog is `roles/infrastructure-deployer.role.json`; its fixed role UUID is
`4bfa5b8d-8e4a-4fc8-9f2b-6115f07cad54`. The identity-reader UUID is
`9df6bf68-4db7-4d38-b7f1-7bb26a541199`. Both definitions have the exact
development resource group as their sole assignable scope, explicit actions,
and no wildcards. The infrastructure role has no authorization
writes and no Managed Identity or federated-credential writes. The separate
identity reader contains only the user-assigned-identity read action; its exact
development-resource-group assignment is temporary and removed with the
infrastructure assignment. Provider registration, if required, needs a separate temporary
subscription-scoped approval limited to provider read plus
`Microsoft.App/register/action` and
`Microsoft.ContainerRegistry/register/action`.

The RBAC deployer has no ordinary resource write. Its role-definition authority
is resource-group scoped. Role-assignment authority is granted separately at
the exact ACR, workspace, or Job and conditioned to the reviewed principal and
role-definition pair. The reviewed action catalogs are under `roles/`.
Its assignment catalog includes only the minimum resource-group deployment
read/write/status actions needed to submit and observe an access-only template,
plus role-assignment read/write; the latter remains exact-resource scoped and
conditioned.
The two proposed deployer identity names are
`id-adventures-suite-migration-foundation-deployer-dev` and
`id-adventures-suite-migration-rbac-bootstrap-dev`; their creation is complete
and grants no access by itself. Deployer federation remains a separate gate.

The starter receives built-in Log Analytics Reader
(`73c42c96-874c-492b-b04d-ab87d138a893`) only on
`log-adventures-suite-migrations-dev`. This is required to retrieve and validate
the exact execution completion envelope. It is read-only, reaches no other
workspace, and adds no direct cost; ingestion and retention remain the relevant
Log Analytics meters.

GitHub has no Job configurator identity or Job-definition mutation path. It may
publish reviewed images and start/observe separately approved executions only.
The persistent Job never stores an operation ID; the starter supplies it as a
per-execution override. The registry-authoritative digest is the artifact
identity.

`10.40.3.0/27` does not overlap the recorded `10.40.0.0/26` App Service or
`10.40.1.0/27` private-endpoint ranges and remains inside `10.40.0.0/16`. Live
VNet prefixes must still be checked immediately before approval.

The templates create no SQL users or permissions. The SQL-free proof obtains an
ARM token using the explicitly selected migration identity and verifies tenant,
object ID, client ID, and audience. It requests no SQL token and emits
`sqlAccessAttempted=false`.

See `docs/development/container-apps-migration-permissions.md` for the complete
matrix, conditional assignment model, cleanup rules, and approval packets. The
legacy combined `foundation.bicep` and `job.bicep` are removed.
There is deliberately no checked-in `job-access.dev.bicepparam`: the custom
role ID must be captured from the immediately preceding foundation-access
deployment, independently reviewed, and supplied only for that operation.

On 2026-08-10, a live read-only query of the Microsoft.App provider operation
catalog in the approved subscription confirmed all four custom-role actions:
`Microsoft.App/jobs/read`, `Microsoft.App/jobs/start/action`,
`Microsoft.App/jobs/execution/read`, and
`Microsoft.App/jobs/executions/read`. Repeat that read-only validation directly
before custom-role creation and stop if any action is absent.
