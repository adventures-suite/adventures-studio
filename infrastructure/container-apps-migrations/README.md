# Container Apps migration job IaC

This directory proposes the permanent development migration runner. It is not
deployed by PR validation. Provisioning is deliberately split to avoid a
registry/image bootstrap cycle:

1. `foundation.bicep` creates the delegated subnet, Log Analytics, Basic ACR,
   identities, federated GitHub credentials, minimum Azure roles, Container
   Apps environment, and `AcrPull` assignment.
2. The publisher pushes the reviewed full-SHA image and resolves its digest
   from ACR, comparing it with the digest returned by the push.
3. `job.bicep` creates or updates the dormant Job only after that digest exists.

The persistent Job template never stores an operation ID. The separately
authorized starter supplies it as a per-execution override to
`az containerapp job start`; an active execution prevents another start. The
registry-authoritative image digest is the artifact identity, so no unbound
caller-supplied artifact checksum is accepted.

`10.40.3.0/27` does not overlap the recorded `10.40.0.0/26` App Service or
`10.40.1.0/27` private-endpoint ranges and remains inside `10.40.0.0/16`.
Deployment must still query live VNet prefixes immediately before approval.

The templates intentionally do not create SQL users or permissions. A later
administrator operation binds the generated migration identity to Azure SQL.
The SQL-free execution proof obtains an ARM token using the explicitly selected
migration user-assigned identity and verifies its tenant, object ID, client ID,
and ARM audience. It does not request a SQL token and emits
`sqlAccessAttempted=false`.
