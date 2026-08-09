# Authentication Certificate and Data Protection Runbook

**Status:** Slice 5F Operational Runbook

**Last Updated:** August 8, 2026

## Purpose

Operate the External ID confidential-client certificate and ASP.NET Core Data
Protection key ring using private Azure services and least-privilege Managed
Identity access.

These are separate cryptographic purposes:

- the External ID certificate signs short-lived client assertions; and
- the Data Protection key ring protects AdventuresSuite cookies and protocol
  state, with a Key Vault key protecting keys at rest.

Do not reuse one key merely because both are stored through the same vault.

## Environment

- Key Vault: `adventures-suite-dev-kv`
- Data Protection storage account: `advsuitedevdpkeys`
- Public network access: disabled for both
- Storage shared-key access: disabled
- Access: application system-assigned Managed Identity over private endpoints

The exact certificate, wrapping key, Blob container, and object URIs are recorded
as deployment outputs. Applications use stable version-independent references
where the approved library requires them for rotation.

## Certificate Provisioning

1. Use an approved private-network execution path and operator identity.
2. Create/import a non-production certificate meeting the reviewed algorithm,
   key size, key usage, purpose, subject, validity, and non-exportability policy.
3. Confirm the private key is available only through the approved Key Vault
   boundary.
4. Grant the application identity only the minimum certificate/secret retrieval
   operations required by the selected Microsoft library.
5. Export only the public certificate for External ID registration.
6. Record public thumbprint, validity, Key Vault object reference, approver, and
   rotation due date without recording private material.
7. Validate certificate loading, private-key presence, purpose, and client
   assertion creation from the application environment.

No PFX password, exported private key, client assertion, or temporary client
secret enters the repository, GitHub, App Service settings, deployment package,
or retained logs.

## Data Protection Provisioning

1. Create or confirm the private Blob container for one environment-specific
   key ring.
2. Grant the application identity Blob data access scoped to that container,
   not the storage account where avoidable.
3. Create or confirm the dedicated Key Vault wrapping key.
4. Grant only required cryptographic operations for Data Protection.
5. Configure the application name/discriminator uniquely for the AdventuresSuite
   development authentication boundary.
6. Configure Blob and Key Vault through private DNS hostnames and Managed
   Identity; do not use a storage connection key.
7. Generate the first key through the application under controlled startup.
8. Restart and scale the application to prove the same cookie is readable by
   another instance/restart within its valid server session.
9. Verify public Blob/Vault access and shared-key access remain denied.

The Data Protection container is not a general document store. Keys are not
downloaded into diagnostics, backups, or support attachments outside the
approved recovery process.

## Startup and Health

When authentication is enabled, startup/readiness fails closed if:

- certificate reference or private key is missing;
- certificate is expired, not yet valid, incorrectly purposed, or inaccessible;
- Data Protection Blob or wrapping key is missing or inaccessible;
- Managed Identity or private DNS resolution fails; or
- environment/application discriminator is unsafe or inconsistent.

Public health output reveals no vault, container, certificate, key, exception,
or access detail. Access-controlled telemetry emits only safe readiness
categories and the server-generated support identity.

## Normal Rotation

Certificate rotation:

1. create the new certificate before the overlap window;
2. register its public portion with External ID;
3. validate the application can load and use it;
4. deploy configuration and perform real sign-in;
5. keep the old certificate available for the approved rollback interval;
6. remove the old External ID credential; and
7. disable/delete the old private certificate version only under retention and
   incident policy.

Data Protection rotation normally uses framework key management. Preserve old
keys while any valid protected payload may require decryption. Rotating the Key
Vault wrapping key uses a version-independent reference and does not justify
deleting old wrapping-key versions needed by stored Data Protection keys.

## Compromise Response

- stop new authentication when trust cannot be established;
- disable the compromised External ID credential and revoke local sessions;
- advance user security versions when the incident scope requires it;
- rotate through a separately approved clean operator/execution path;
- preserve audit and incident evidence without private key material;
- verify public-host isolation, sign-in, restart, revocation, and sign-out; and
- remove emergency access after the incident.

Deleting the Data Protection key ring can invalidate cookies and protocol state
but is not an ordinary logout mechanism. Destructive key deletion requires an
incident or teardown decision with recovery impact understood.

## Backup, Recovery, and Teardown

Document Azure-native retention and recovery settings for the vault, keys,
certificates, storage account, container, and Blob versions. Test restoration in
an isolated non-production environment without copying keys into source control.

Teardown first disables authentication and revokes sessions, then removes
application access and provider credentials. Purge-protected Key Vault objects
follow their retention lifecycle; environment deletion does not imply immediate
cryptographic purge.
