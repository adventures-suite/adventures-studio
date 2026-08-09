# External ID Development Environment Runbook

**Status:** Slice 5F Operational Runbook

**Last Updated:** August 8, 2026

## Scope

Configure and validate the non-production `AdventuresSuite Development`
External ID tenant without placing credentials or provider-specific identity in
core contracts.

## Fixed Environment Identity

- Tenant ID: `4fc030b6-583b-4b7a-9718-c8c26019771e`
- Initial domain: `adventuressuitedev.onmicrosoft.com`
- Data location: United States
- Workspace origin:
  `https://adventures-suite-dev-e7a5g5dudneqgqcv.westus2-01.azurewebsites.net`
- Sign-in callback: `/signin-oidc`
- Signed-out callback: `/signout-callback-oidc`

## Prerequisites

- approved operator identity and least-privilege tenant role;
- access to the approved external tenant and billing relationship;
- exact application build and environment inventory;
- approved Key Vault certificate process;
- sanitized change ticket/evidence location; and
- no real production customer identities.

Maintain two reviewed emergency tenant administrators before production. Do not
use emergency accounts for normal application registration work.

## Application Registration

1. Switch explicitly to tenant ID
   `4fc030b6-583b-4b7a-9718-c8c26019771e`.
2. Create or select the one development confidential web application.
3. Record the generated client/application ID as a non-secret environment
   value.
4. Register only the exact HTTPS sign-in and signed-out callback URLs.
5. Disable implicit and hybrid token issuance; use authorization code with PKCE.
6. Do not configure access-token persistence or Microsoft Graph permissions
   without a separately approved use case.
7. Configure only the minimum claims required by the validated OIDC flow.
   Creator, membership, role, permission, entitlement, and resource claims are
   prohibited.
8. Register only the public portion of the approved certificate. The private
   key remains in Key Vault.
9. Record certificate identifier, thumbprint, validity, and rotation dates
   without exporting private material.

## User Flow

Create a development-only sign-up/sign-in flow with approved methods and
branding. Record enabled identity providers, recovery, MFA/Conditional Access,
claim output, session behavior, and test-user policy.

Email, display name, provider object ID, and profile claims never link an
existing AdventuresSuite account. Exact validated issuer and subject resolve the
platform `UserId`.

## Application Configuration

GitHub Environment and App Service configuration may contain:

- tenant ID and initial domain;
- authority/issuer configuration;
- client/application ID;
- exact workspace origin and callback paths;
- Key Vault certificate URI/reference; and
- approved session and revalidation durations.

Do not store a private key, assertion, authorization code, token, cookie, or
temporary client secret in GitHub variables, repository files, deployment
artifacts, logs, or support evidence.

## Validation

Before enabling authentication:

- confirm tenant, application, user flow, callbacks, and public certificate;
- verify non-production/prod separation;
- verify callback mismatch and public-host challenges fail;
- validate certificate time, private-key access, key usage, and purpose through
  the application Managed Identity;
- confirm tokens are not persisted;
- confirm startup fails closed for missing or invalid provider configuration;
- run prohibited-value logging canaries; and
- retain sanitized configuration and operator evidence.

## First Sign-In Gate

Using an approved development test identity:

1. initiate sign-in only from the canonical workspace host;
2. complete External ID authentication;
3. validate state, nonce, PKCE, issuer, audience, signature, lifetime, and exact
   callback;
4. map exact ordinal `iss` and `sub` to one platform `UserId`;
5. atomically create or resolve identity and application session;
6. issue the bounded authoritative workspace cookie;
7. prove no Creator membership or permission is granted;
8. restart App Service and prove Data Protection/session survival;
9. revoke the session and prove immediate request and circuit rejection;
10. sign in again and prove POST-only authoritative sign-out; and
11. confirm public Creator routes remain anonymous.

No raw token, claim set, subject, authorization code, assertion, cookie, or
certificate material enters retained evidence.

## Rotation and Emergency Response

Normal certificate rotation overlaps old and new public certificates:

1. create a new non-exported Key Vault certificate version/object according to
   the approved key policy;
2. validate purpose and dates;
3. register its public certificate with External ID;
4. update the version-independent application reference if required;
5. deploy/restart and complete a sign-in smoke test;
6. retain rollback capability during the overlap window; and
7. remove the old provider registration and revoke old private-key access only
   after evidence and rollback approval.

For compromise, disable the credential/application as appropriate, revoke local
sessions/security versions, stop new sign-in, preserve evidence, rotate through
the incident process, and never expose a temporary client secret as an
unreviewed fallback.

## Teardown

Development teardown requires approval and evidence. Disable sign-in, revoke
sessions, remove application credentials and registrations, remove test users
under retention policy, unlink billing only through the approved tenant process,
and preserve required audit and incident records. Tenant deletion is destructive
and is not implied by ordinary environment cleanup.
