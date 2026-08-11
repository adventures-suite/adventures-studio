# Container Apps migration job IaC

This directory proposes the permanent development migration runner. It is not
deployed by PR validation. Provisioning has four least-privilege template
boundaries and one intervening publication boundary. Every step requires its
own approval and verification; no workflow may combine them:

1. The temporary infrastructure deployer deploys
   `foundation-resources.bicep` only.
2. The temporary RBAC deployer deploys `foundation-access.bicep` only.
3. The GitHub publisher publishes the reviewed full-SHA image and resolves and
   verifies its registry-authoritative digest.
4. The temporary infrastructure deployer deploys digest-bound
   `job-resource.bicep` only.
5. The temporary RBAC deployer deploys `job-access.bicep` only.
6. Only afterward may the GitHub starter run a separately approved SQL-free
   execution-channel proof.

Resource templates contain no `Microsoft.Authorization` resources. Access
templates create no ordinary resources: named resources are `existing`, and
supplied IDs are validated against the expected subscription, resource group,
type, and exact name. Generated IDs come only from immediately preceding
reviewed deployment outputs. Display-name lookup, hardcoded generated principal
or client IDs, and unrelated deployment outputs are prohibited.

The infrastructure deployer is temporary and resource-group scoped. Its exact
catalog is `roles/infrastructure-deployer.role.json`; it has no authorization
writes. Provider registration, if required, needs a separate temporary
subscription-scoped approval limited to provider read plus
`Microsoft.App/register/action` and
`Microsoft.ContainerRegistry/register/action`.

The RBAC deployer has no ordinary resource write. Its role-definition authority
is resource-group scoped. Role-assignment authority is granted separately at
the exact ACR, workspace, or Job and conditioned to the reviewed principal and
role-definition pair. The reviewed action catalogs are under `roles/`.
The two proposed deployer identity names are
`id-adventures-suite-migration-foundation-deployer-dev` and
`id-adventures-suite-migration-rbac-bootstrap-dev`; creating them is a future
separate approval and grants no access by itself.

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
