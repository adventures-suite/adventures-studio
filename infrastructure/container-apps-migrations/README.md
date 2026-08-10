# Container Apps migration job IaC

This directory proposes the permanent development migration runner. It is not
deployed by PR validation. The template references the existing development
VNet and creates a delegated `10.40.3.0/27` subnet, Consumption Container Apps
environment, manual Job, Basic ACR, two user-assigned identities, Log Analytics
workspace, and only the ACR pull assignment.

`10.40.3.0/27` does not overlap the recorded `10.40.0.0/26` App Service or
`10.40.1.0/27` private-endpoint ranges and remains inside `10.40.0.0/16`.
Deployment must still query live VNet prefixes immediately before approval.

The template intentionally does not create SQL users or permissions. A later
administrator operation binds the generated migration identity to Azure SQL.
