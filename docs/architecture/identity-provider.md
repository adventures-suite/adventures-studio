# AdventuresSuite Human Identity Provider Decision

**Version:** 1.0

**Status:** Approved for Phase 3 Slice 5

**Last Updated:** August 7, 2026

## Decision

AdventuresSuite will use **Microsoft Entra External ID in an external tenant**
as the initial provider for customer and travel-professional human
authentication.

The web application will use browser-delegated OpenID Connect authorization
code flow with PKCE. The provider authenticates a person; AdventuresSuite maps
the validated issuer and subject to a stable platform `UserId`, creates and
controls its own application session, and performs Creator-scoped authorization
through the existing provider-neutral policy boundary.

No authentication package, tenant, application registration, or login UI is
introduced by this decision slice. Those belong to Phase 3 Slice 5.

## Governing Boundaries

- Entra External ID establishes external human identity only.
- A validated provider identity does not establish Creator, membership, role,
  ownership, Planning Engagement, or resource permission.
- The canonical provider mapping key is issuer plus subject. Both values retain
  their validated representation and use exact ordinal, case-sensitive
  comparison; neither value is lowercased. Email address, display name, social
  provider, or tenant branding is never the stable platform identity.
- Provider claims are translated in an infrastructure adapter and do not enter
  core authorization contracts.
- Azure Managed Identity remains workload identity. It never becomes a human
  `UserId` or satisfies customer consent and approval policies.
- Agency users authenticate through the same human identity boundary. Agency
  membership still grants nothing in a customer Creator without a future
  accepted, active, plan-scoped Planning Engagement.

## Why Entra External ID

External ID is Microsoft's current CIAM service for consumer and business
customer applications. An external tenant is separate from the Adventures
Studio workforce tenant and supports self-service customer registration,
email-based local accounts, Google, Facebook, Apple, Microsoft Entra federation,
and custom OIDC or SAML/WS-Fed providers through browser-delegated flows.

It aligns with the existing Azure App Service and Azure operational model while
retaining an OIDC boundary that can be replaced without changing `UserId`,
Creator membership, permissions, Planning ownership, or audit contracts.

External ID supports Conditional Access MFA. Current external-tenant methods
include email one-time passcode, paid SMS, and passkeys for eligible local
password accounts. Passkeys currently require a custom authentication domain,
prior MFA, and a credential-management experience; federated and email-OTP
primary users cannot currently register them. These limitations must be
re-evaluated before passkeys are promised as a universal customer feature.

External ID uses monthly-active-user billing with optional paid capabilities.
Exact pricing, Conditional Access licensing, SMS cost, custom-domain cost, and
support requirements must be rechecked before production activation rather
than copied into source as permanent assumptions.

## Evaluation Matrix

| Criterion | Entra External ID | Auth0 | Self-hosted ASP.NET Core Identity |
| --- | --- | --- | --- |
| Standards | OIDC/OAuth with external federation | OIDC/OAuth with broad federation | Application-defined; external providers available |
| Consumer and business users | External tenant supports both | B2C and B2B plans and organizations | Must be modeled and operated by AdventuresSuite |
| Recovery | Provider-managed local-account reset | Provider-managed recovery | AdventuresSuite owns the complete recovery system |
| MFA and phishing resistance | Conditional Access; email OTP, paid SMS, eligible passkeys | MFA and passkeys vary by plan | Must be selected, implemented, and supported |
| Social and enterprise federation | Google, Facebook, Apple, Entra, custom OIDC/SAML | Broad social and enterprise connections | Each provider is configured and supported locally |
| ASP.NET Core integration | Official Microsoft guidance and libraries | Mature official SDK and OIDC support | Native framework integration |
| Azure operations | Same cloud control and billing model | Separate vendor control plane | Application and database become identity control plane |
| Local development and CI | Non-production registration plus deterministic fake adapter | Non-production tenant plus deterministic fake adapter | Local database possible, but credential behavior enters tests |
| Session revocation | Application cookie remains application-owned | Application cookie remains application-owned | Fully application-owned |
| Cost model | MAU plus optional capabilities | MAU and feature tiers | Infrastructure plus substantial engineering/security operations |
| Portability | Strong when issuer/subject adapter is preserved | Strong when issuer/subject adapter is preserved | Framework and credential-store migration burden |
| Current fit | Selected | Viable contingency | Rejected for current operational scope |

## Evaluated Alternatives

### Auth0

Auth0 is a credible OIDC CIAM alternative with Universal Login, social and
enterprise connections, passwordless authentication, MFA, attack protection,
and mature .NET integration. It was not selected initially because it adds a
second identity operating model beside Azure, and production environment,
advanced MFA, organization, support, and rate-limit capabilities vary by paid
tier. The provider-neutral issuer/subject mapping preserves Auth0 as a migration
or contingency option.

### Self-Hosted ASP.NET Core Identity

Self-hosting would maximize credential-store control and remove a CIAM vendor,
but AdventuresSuite would own password security, credential recovery, abuse
controls, MFA lifecycle, account linking, breach response, email delivery, and
authentication availability. ASP.NET Core supplies strong primitives, but this
operational and security burden is not justified for the current team and
product phase. Framework Identity types also must not become core platform
identities.

## Protocol and Session Requirements

Slice 5 must implement:

- redirecting the user agent through the browser-delegated OIDC authorization
  code flow with PKCE;
- confidential-client authentication using a certificate-backed client
  assertion in hosted environments, with a client secret allowed only as a
  temporary, approved, securely stored fallback;
- exact issuer, audience, signature, nonce, state, redirect URI, and token-time
  validation;
- local or explicitly allowlisted return destinations only;
- a Secure, HttpOnly application cookie with an appropriate SameSite policy,
  bounded absolute and inactivity lifetimes, renewal rules, and session-id
  rotation at sign-in and privilege changes;
- server-controlled session revocation independent of provider token lifetime;
- local session termination plus provider sign-out without assuming provider
  sign-out invalidates every application session;
- anti-forgery protection for cookie-authenticated mutations;
- generic authentication and recovery errors that prevent account enumeration;
- current Creator-membership re-evaluation below the session boundary;
- recent-authentication or step-up requirements for selected high-risk actions;
- no access or refresh token in browser-readable storage, URLs, logs, or
  telemetry.

The application must not keep provider access or refresh tokens unless a
reviewed downstream-API requirement exists. Sign-in alone does not justify
retaining them.

## Account Provisioning and Linking

Successful provider authentication resolves a platform identity mapping:

```text
ExternalIdentity(provider, issuer, subject) -> UserId
```

First sign-in may create a disabled or onboarding platform user before any
Creator membership is granted. Creator membership creation is a separate,
authorized, audited operation.

Accounts are never linked automatically by matching email address. Linking a
second provider identity requires recent authentication, explicit confirmation,
conflict checks, notification, audit, and a recovery design. Unlinking must not
remove the final usable authentication method without a safe replacement.

## Recovery and MFA

- Use provider-managed password reset for provider-local passwords.
- Do not build an application password database or password-reset flow.
- Recovery must not grant Creator membership or bypass application revocation.
- Initial development may use email/password or email OTP according to the
  validated External ID user-flow capabilities.
- Production MFA policy, passkey rollout, fallback factors, recovery assurance,
  and high-risk step-up rules require a dedicated pre-production security
  review.
- SMS is not the preferred default because of cost and weaker phishing
  resistance; it may be an approved fallback where customer accessibility
  requires it.

## Environment, Development, and CI

- Production and non-production use separate app registrations and redirect
  URI allowlists; separate external tenants are preferred and required before
  real production customer identities are collected.
- Configuration comes from environment-specific Azure settings and GitHub
  Environments. No client credential or tenant secret is committed.
- External ID application authentication does not currently support Managed
  Identity or workload identity. Use a certificate-backed client assertion for
  the hosted confidential web client. Any temporary client secret must be
  stored in an approved secret store, rotated, and excluded from telemetry.
- Managed Identity remains reserved for supported Azure workload-to-service
  access and must not be attempted as the External ID OIDC client credential.
- Local development uses either the non-production External ID registration or
  an explicit Development-only fake authentication adapter.
- Automated unit and integration tests use deterministic fake identities and
  never depend on a live external tenant.
- The Development-only adapter must fail startup outside Development and must
  never be included as a production fallback.

## Revocation

Provider token revocation and application session revocation are separate.
Entra cannot directly revoke an application-issued session cookie. AdventuresSuite
therefore maintains a server-verifiable session or user security version and
revalidates high-risk operations and Creator membership against current state.

Disabling a platform user or Creator membership must deny protected operations
without waiting for the external provider session to expire. Disabling the
external identity mapping must prevent new application sessions. Emergency
response terminates local sessions and invokes supported provider revocation
as complementary controls.

## Portability and Exit Strategy

Provider configuration is isolated behind authentication adapters. Persisted
platform data stores `UserId`; the external mapping stores provider, issuer,
and subject separately. Domain records do not store Entra object identifiers,
emails, or claims as ownership keys.

A provider migration adds verified mappings to existing `UserId` records. It
does not rewrite Creator membership, Adventure Plan ownership, audit actors, or
Planning data. Migration requires an explicit account-proofing and conflict
procedure; matching by email alone is prohibited.

## Sources Reviewed

- [Microsoft Entra External ID overview](https://learn.microsoft.com/en-us/entra/external-id/external-identities-overview)
- [External-tenant identity providers](https://learn.microsoft.com/en-us/entra/external-id/customers/concept-authentication-methods-customers)
- [MFA in external tenants](https://learn.microsoft.com/en-us/entra/external-id/customers/concept-multifactor-authentication-customers)
- [ASP.NET Core .NET 10 OIDC guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0)
- [External ID security operations guidance](https://learn.microsoft.com/en-us/entra/architecture/deployment-external-operations)
- [Microsoft Entra access revocation](https://learn.microsoft.com/en-us/entra/identity/users/users-revoke-access)
- [External ID pricing and billing overview](https://learn.microsoft.com/en-us/entra/external-id/external-identities-pricing)
- [Auth0 pricing and capability tiers](https://auth0.com/pricing)

Provider capabilities, pricing, preview status, and service limits are
time-sensitive. Revalidate them before Slice 5 package selection and again
before production activation.
