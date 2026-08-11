# Container Apps Migration Deployment Permissions

**Status:** repository design only; no Azure authority granted

## Responsibility matrix

| Boundary | Actor | May create | Explicit exclusions |
| --- | --- | --- | --- |
| `foundation-resources.bicep` | Temporary infrastructure deployer | Subnet, workspace, ACR, environment, four identities, two federated credentials | RBAC, Job, SQL |
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

## Deployer permissions

The infrastructure action catalog is source controlled under
`infrastructure/container-apps-migrations/roles/`. Assignment is temporary at
the development resource group and excludes role-definition and role-assignment
writes. Provider registration, if needed, is a distinct temporary approval for
only provider read and the two exact register actions at subscription scope.

The RBAC deployer has no ordinary resource write. Role-definition authority is
resource-group scoped. Assignment authority is granted separately at each exact
target:

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

## Approval packets

1. **Create deployer identities.** Create exact user-assigned identities
   `id-adventures-suite-migration-foundation-deployer-dev` and
   `id-adventures-suite-migration-rbac-bootstrap-dev` and their reviewed
   `database-development` OIDC credentials only. Grant no roles.
   Verify tenant, IDs, issuer, subject, audience, and absence of secrets/access.
2. **Temporarily authorize the infrastructure deployer.** Approve one named
   resource-template deployment in a bounded window. Record assignment and
   deadline, verify plan and post-state, remove access, refresh credentials, and
   prove loss of write access. Provider registration requires a separate packet.
3. **Temporarily authorize the RBAC deployer.** Approve one named access-template
   deployment with resolved exact scopes, principals, roles, conditions, and a
   bounded window. Verify assignments and absence of broader roles, remove
   authority, refresh credentials, and prove loss of access.
4. **Deploy and clean up each boundary in sequence.** Separately approve and
   verify foundation resources, foundation access, publication, Job resource,
   and Job access. Retain source SHA, template checksum, deployment ID, inputs,
   outputs, UTC timing, post-state, and cleanup. Stop on drift, excess authority,
   ambiguous output, cleanup failure, or combined steps.

These packets do not authorize SQL, Job execution, migration, public ingress,
production changes, or retirement of the old bridge. The SQL-free proof remains
a later separate approval.

`job-access.bicep` intentionally has no checked-in development parameter file:
its custom role-definition ID does not exist until `foundation-access.bicep`
completes. A bounded parameter file may be generated from that exact deployment
output, checksum-reviewed for the separate approval, used once, and removed.
