# AdventuresSuite Authentication Integration Design

**Version:** 1.0

**Status:** Proposed for Phase 3 Slice 5 Review

**Last Updated:** August 7, 2026

## Purpose

This design translates the approved Microsoft Entra External ID decision into a
secure ASP.NET Core integration boundary. It defines identities, persistence,
sessions, protocol handling, configuration, failure behavior, tests, and rollout
before packages, tenant resources, routes, or login UI are introduced.

Read with:

- `docs/architecture/identity-provider.md`
- `docs/architecture/identity-authorization.md`
- `docs/architecture/security.md`
- `docs/architecture/observability.md`
- `docs/development/identity-authorization-implementation-plan.md`

## Governing Flow

```text
Browser
  -> local sign-in endpoint
  -> OIDC challenge with protected local return target
  -> Microsoft Entra External ID
  -> exact application callback
  -> validate issuer, signature, audience, code, PKCE, state, and nonce
  -> resolve (provider, issuer, subject) to platform UserId
  -> create revocable application session
  -> issue protected application cookie
  -> build ActorIdentity(UserId) on later authenticated requests
  -> authorize Creator + permission + resource through platform policies
```

Authentication ends at a stable `UserId` and valid application session. It does
not choose a Creator, create a membership, trust a public host, or authorize a
Planning operation.

## Authentication and Workspace Origin

Private authentication uses one explicitly configured canonical AdventuresSuite
workspace origin. Public Creator domains remain public-content origins and do
not independently issue or share authentication cookies.

- The canonical workspace scheme and host come from trusted environment
  configuration, never the inbound Host or forwarded headers.
- Register only exact canonical callback and sign-out URLs for each environment.
- The application cookie is host-only for the workspace origin.
- Private authentication schemes, cookie validation, challenges, callbacks,
  sign-out, and private endpoints activate only when the request matches the
  canonical workspace scheme and host. A public Creator or unknown host must
  ignore or reject a manually supplied workspace cookie before constructing an
  authenticated principal.
- Public Creator pages may link to the workspace sign-in entry point, but they
  do not transfer a cookie or infer a Creator membership.
- After sign-in, the user enters the platform workspace or an allowlisted local
  workspace destination. A public Creator URL is not accepted as an arbitrary
  post-authentication return target.
- Production and non-production use different workspace origins, registrations,
  cookies, certificates, key rings, and identity/session data.

The host boundary must evolve from “every host resolves a public Creator” to an
explicit trusted-host classification:

```text
PublicCreatorHost -> public Creator Context -> published routes
PlatformWorkspaceHost -> no implicit Creator -> authenticated private routes
UnknownHost -> reject
```

This does not weaken Creator resolution. A workspace request still supplies an
explicit Creator resource scope and proves current membership and ownership.
Authentication endpoints must never run on an unknown host. Host-classification
changes require Creator host-confusion and public-route regression tests before
authentication endpoints are exposed.

## Framework and Package Direction

Slice 5 should use ASP.NET Core cookie and OpenID Connect authentication with
Microsoft Identity Web as the External ID integration adapter. Exact package
versions are selected and locked when implementation begins; packages remain in
the web composition and infrastructure boundary.

Core identity and authorization contracts do not reference:

- `ClaimsPrincipal` or provider claim types;
- ASP.NET Core authentication schemes or handlers;
- Microsoft Identity Web, MSAL, Entra, or Graph types;
- cookies, OIDC messages, tokens, certificates, or HTTP context.

No downstream Microsoft Graph or provider API requirement exists in this
slice. Do not retain access or refresh tokens merely because a library supports
token acquisition.

## Confidential Client Credential

The hosted ASP.NET Core application authenticates to External ID with a client
assertion signed by an X.509 certificate.

- Register only the public certificate with the External ID application.
- Store the private certificate in Azure Key Vault.
- App Service Managed Identity may receive least-privilege access to retrieve
  the certificate from Key Vault.
- Managed Identity authenticates to Key Vault only. It is not the External ID
  application credential.
- The certificate private key signs the confidential-client assertion sent to
  External ID.
- Support overlapping old and new certificates for rotation.
- Monitor expiry and prove rotation before production activation.
- A client secret is a temporary development or emergency fallback only. It is
  never committed, logged, placed in global settings, or accepted as normal
  production configuration.

Production startup fails closed when authentication is enabled but the required
certificate, authority, tenant, client, callback, or data-protection settings
are missing or invalid. Public-only deployments may explicitly disable private
authentication; they must not silently fall back to development identity.

## External Identity Mapping

Provider identity is represented by a validated immutable external identity key:

```text
ExternalIdentityKey
  Provider = "entra_external_id"
  Issuer = validated exact issuer URI
  Subject = validated non-empty subject
```

OIDC `iss` and `sub` values are case-sensitive identity values. Contracts and
Dapper mappings compare their validated representations with ordinal,
case-sensitive semantics and never lowercase either value. Azure SQL columns
participating in external-identity lookup and uniqueness use an explicitly
case-sensitive binary collation, or an equivalently reviewed collision-safe
canonical key that preserves the exact original values. The unique provider,
issuer, and subject boundary must not inherit a database's case-insensitive
default collation.

The mapping resolves to an AdventuresSuite `UserId`. Email address, display
name, preferred username, social identity provider, tenant branding, and Entra
object identifier are attributes, not ownership keys.

Minimal persistence required by Slice 5:

```text
Users
  UserId
  Status
  SecurityVersion
  CreatedAtUtc
  UpdatedAtUtc

ExternalIdentities
  ExternalIdentityId
  UserId
  Provider
  Issuer
  Subject
  CreatedAtUtc
  LastAuthenticatedAtUtc
  DisabledAtUtc

UserSessions
  UserSessionId
  UserId
  ExternalIdentityId
  SecurityVersion
  CreatedAtUtc
  LastSeenAtUtc
  AbsoluteExpiresAtUtc
  RevokedAtUtc
  RevocationReason
```

Requirements:

- issuer and subject form a unique provider-scoped identity;
- persistence and lookup preserve exact case-sensitive issuer and subject
  semantics under every supported database collation;
- mapping creation is transactional and race-safe;
- a first authentication can create a platform user but grants no Creator
  membership;
- disabled users or external mappings cannot create a new session;
- each session is authoritatively bound to the external identity that
  established it, and disabling that mapping invalidates the bound session;
- no automatic linking by email;
- linking and unlinking are deferred until their recent-authentication,
  notification, recovery, concurrency, and audit workflow is approved;
- raw tokens and provider assertions are never persisted;
- provider profile attributes are not copied unless a separately approved
  product purpose requires them.

These records are platform identity/session persistence, not Creator membership
persistence. Creator memberships, role assignments, effective periods, and
permission versions remain Phase 3 Slice 6.

## Application Session Model

OIDC authentication creates an application-controlled session. The protected
cookie contains only the minimum identity needed to resolve and validate that
session:

- `UserId`;
- opaque `UserSessionId`;
- user `SecurityVersion`;
- authentication time and authentication-method context needed for approved
  recent-authentication policies;
- no Creator, membership, role, resource permission, provider token, email, or
  private profile data.

Every authenticated private request validates that:

- the session exists and is not revoked;
- the user is active;
- the cookie and authoritative security versions match;
- idle and absolute expiration have not passed;
- the current request still proceeds through Creator authorization.

Security-critical validation is read-through and immediate: revocation, user
status, security version, and expiration are never hidden behind an activity
write throttle. `LastSeenAtUtc` is non-security-critical bookkeeping. Touches
are coalesced and written no more than once per configured interval per session,
with a proposed maximum frequency of once every five minutes. Updates are
monotonic (`new LastSeenAtUtc > stored LastSeenAtUtc`), use optimistic or
conditional concurrency, and tolerate a lost competing activity touch without
failing an otherwise valid request. A stale writer may never move the timestamp
backward, extend absolute expiry, clear revocation, or overwrite a security
version. Idle-expiry evaluation uses the authoritative stored value plus only a
validated current-request observation; throttling must not create an extension
beyond the configured idle or absolute boundary.

Initial policy uses a bounded absolute lifetime and shorter inactivity lifetime.
Exact durations are environment configuration with secure maximums validated at
startup. Proposed production defaults for review are eight hours absolute and
thirty minutes inactive. Sliding activity never extends the absolute boundary.

Session identity rotates on sign-in and after a security-sensitive identity or
privilege change. A user security-version increment invalidates every earlier
session. Individual logout revokes one session; “sign out everywhere,” account
disablement, recovery response, or identity compromise can revoke all sessions.

Provider session and application session are separate. Provider sign-out cannot
be assumed to revoke the application cookie, and deleting the application
cookie cannot be assumed to end every provider session.

## Interactive Server Circuit Boundary

The current application uses Blazor Interactive Server. Its authentication
state is captured when the SignalR circuit is established and is not naturally
re-read from the cookie on each interactive navigation.

Slice 5 must therefore add a custom
`RevalidatingServerAuthenticationStateProvider` that checks the authoritative
application session and user security version on a short, bounded interval. An
invalid, expired, or revoked session changes the circuit to anonymous and forces
protected UI to stop operating. Reconnection and resumed circuit state must
revalidate before protected work continues.

Periodic circuit revalidation improves UI responsiveness to revocation but is
not the authorization boundary. Every protected application-service command or
query revalidates the session and executes Creator resource authorization before
private data access. A stale circuit principal cannot authorize a mutation or
sensitive read.

Authentication endpoints and sign-out execute as normal server HTTP endpoints
with full-page navigation, not as interactive component event handlers. No
`HttpContext`, claims principal, session object, or actor context is retained in
a singleton. Circuit-scoped state is never shared across users.

Cookie-authenticated SignalR transport establishment requires exact Origin
validation against the configured workspace origin. Enforce this for the
negotiate request and for WebSocket, Server-Sent Events, and long-polling
connections and reconnections. Missing, malformed, public Creator, unknown, or
otherwise non-workspace origins fail before a circuit is established. CORS and
antiforgery are not substitutes for this check because browser CORS protections
do not govern WebSocket origin acceptance. Forwarded headers cannot expand the
origin allowlist, and no wildcard or suffix host matching is permitted.

## Cookie and Data Protection

The application authentication cookie must use:

- a `__Host-` prefixed name;
- `Secure=Always` outside explicitly isolated local HTTP tests;
- `HttpOnly=true`;
- `Path=/` and no `Domain` attribute;
- `SameSite=Lax` unless protocol testing proves a narrower compatible setting;
- bounded ticket lifetime with renewal controlled by the server-side session;
- no client-side token storage.

OIDC nonce and correlation cookies use framework protocol-safe settings and are
covered by callback, SameSite, timeout, replay, and excessive-cookie tests.

Data Protection keys must be shared and durable across App Service instances,
restarts, and deployment slots before authentication reaches a shared Azure
environment. Store the key ring in an approved Azure Blob container and protect
keys with a versionless Azure Key Vault key. App Service Managed Identity may
access Blob Storage and Key Vault with least privilege. Development uses an
isolated local key ring; tests use deterministic isolated configuration.

Changing application name, key ring, or key-encryption key is a session-impacting
deployment and requires an explicit rotation or invalidation plan.

## Endpoints and Redirect Safety

Reserved infrastructure paths are configured explicitly and registered exactly
with External ID:

- OIDC callback;
- signed-out callback;
- remote/front-channel sign-out callback if approved during implementation.

User-facing endpoints are application-owned:

- sign-in initiation;
- sign-out mutation;
- signed-out landing page;
- safe authentication failure page;
- access-denied page.

Rules:

- Sign-in initiation accepts only a local relative return target that passes a
  centralized validator.
- Never accept a scheme-relative target, encoded external URL, forwarded-host
  target, or arbitrary callback URI.
- State, nonce, correlation, issuer, audience, signature, code, PKCE verifier,
  token times, and registered redirect URI are validated by reviewed middleware.
- Sign-out is an unsafe POST protected by antiforgery. A GET may render a
  confirmation but never performs logout.
- Local session revocation and cookie deletion happen even if remote provider
  sign-out is unavailable.
- Provider errors map to bounded internal categories and a safe correlation ID.
  Raw exception, error description, claims, code, token, state, or issuer input
  never appears in the URL or public response.
- Unknown users and disabled users receive generic responses that do not enable
  account enumeration.

## CSRF, Headers, and Proxy Trust

All browser-accessible cookie-authenticated POST, PUT, PATCH, and DELETE
operations require antiforgery validation. Apply protection by default and
review every opt-out. Authentication callback endpoints rely on OIDC state,
nonce, and correlation validation rather than application antiforgery tokens.

Before authentication evaluates scheme or redirects, forwarded headers are
accepted only from the explicitly trusted Azure proxy topology. Untrusted Host,
`X-Forwarded-Host`, or `X-Forwarded-Proto` values cannot construct authority,
callback, logout, or return URLs.

Production enables HTTPS redirection or enforces HTTPS at the trusted edge,
HSTS, content-type sniffing protection, a reviewed referrer policy, frame
protection, and a Content Security Policy compatible with the Blazor rendering
model. Header values are tested rather than assumed from platform defaults.

## Reauthentication and Authentication Assurance

Authentication records the provider authentication time and safe method or
assurance context when validated and available. Provider claims are not treated
as permanent authorization.

Initial recent-authentication candidates include:

- membership and permission administration;
- restoring archived plans when policy classifies it as high risk;
- viewing or changing especially sensitive reservation data;
- linking or unlinking identities;
- changing recovery or session controls;
- future support impersonation and professional direct-edit grants.

The maximum acceptable authentication age and MFA/step-up requirement are named
policies, not UI checks. If External ID cannot satisfy the required assurance
for an operation, deny safely rather than accepting an old application cookie.

## Local Development and Automated Tests

Local development supports two explicit modes:

1. the non-production External ID tenant and app registration over HTTPS; or
2. a Development-only deterministic authentication adapter.

The development adapter:

- activates only when the host environment is exactly Development and explicit
  configuration selects it;
- uses server configuration, not caller-controlled headers, query strings, or
  cookies, to choose a test identity;
- creates only allowlisted synthetic `UserId` values;
- emits a prominent warning and never acts as a production fallback;
- causes startup failure if selected in any non-Development environment.

Test projects replace authentication through the application factory with a
test-only scheme. CI requires no External ID tenant, certificate, browser, or
network exporter. A live-provider smoke test is a separately approved Azure
environment test and never replaces deterministic tests.

## Failure Semantics

- External ID unavailable: public content remains available; new protected
  sign-ins fail safely with a support correlation identifier.
- Identity/session database unavailable: authenticated private access fails
  closed; no stale cookie grants access.
- certificate unavailable or expired: authentication-enabled startup fails;
  health reports a safe degraded category without certificate details.
- Data Protection key ring unavailable: authentication-enabled startup or
  readiness fails safely; do not generate an incompatible temporary key ring in
  a shared environment.
- telemetry unavailable: authentication behavior continues, but required audit
  persistence rules remain independent.
- provider sign-out unavailable: local session revocation still succeeds and
  the user receives a safe status explaining that provider-wide sign-out could
  not be confirmed.

Retry behavior is bounded and never repeats an interactive mutation or callback
indefinitely.

## Telemetry and Audit

Safe operational and security events include:

- sign-in initiated, completed, rejected, or failed;
- callback validation category;
- session created, refreshed, expired, or revoked;
- disabled user or identity mapping rejection;
- repeated state, nonce, callback, or antiforgery failure;
- certificate and Data Protection readiness category.

Telemetry may include actor type, opaque platform `UserId` only when necessary,
safe event category, route template, outcome, and correlation identity. It never
contains email, name, issuer input, subject, token, claim set, authorization
code, state, nonce, cookie, certificate material, raw return target, or provider
error description.

Durable audit intent is required for identity linking/unlinking, user disable or
reenable, security-version changes, administrative session revocation, and
future support access. Routine sign-in telemetry is not substituted for durable
audit where a protected mutation requires it.

## Verification Matrix

Automated tests must prove:

- missing or invalid authority, client, tenant, callback, certificate, session,
  and Data Protection configuration fails startup appropriately;
- issuer, subject, audience, signature, state, nonce, code, PKCE, and token-time
  validation failures deny without disclosure;
- local, encoded, scheme-relative, forwarded-host, and external return targets
  are handled safely;
- external identities resolve idempotently and never link by email;
- issuer and subject comparisons and SQL uniqueness remain exact and
  case-sensitive, including values differing only by case;
- concurrent first sign-in creates one mapping and one platform user;
- first sign-in grants no Creator membership;
- disabled user or mapping cannot establish a session;
- session, user status, security version, idle expiry, absolute expiry, and
  revocation are checked server-side;
- revocation and security-version checks remain immediate while activity
  touches are coalesced, bounded in frequency, monotonic, and safe under
  concurrent requests;
- individual and all-session revocation work without provider availability;
- cookies and protocol cookies have reviewed security attributes;
- Data Protection keys survive restart and multiple test instances;
- unsafe cookie-authenticated methods fail without valid antiforgery proof;
- callback and sign-out endpoints have only their intended CSRF behavior;
- unknown hosts and forwarded headers cannot alter callbacks or Creator scope;
- public Creator hosts cannot issue workspace cookies or host OIDC callbacks;
- public Creator and unknown hosts ignore or reject a manually supplied
  workspace cookie and cannot activate a private authentication scheme or
  endpoint;
- SignalR negotiate, WebSocket, Server-Sent Events, and long-polling requests
  accept only the exact configured workspace origin, including reconnection;
- workspace requests receive no implicit Creator from their host;
- development authentication cannot activate outside Development;
- test identity cannot be selected through request input;
- no Creator, membership, role, permission, access token, or refresh token is
  accepted from provider claims;
- logs, errors, URLs, and traces pass prohibited-value canary tests;
- public routes remain anonymous and available;
- private authorization still rejects anonymous, cross-Creator, stale,
  disabled, support, and unsupported workload contexts;
- Interactive Server circuits revalidate revoked sessions, and stale circuit
  state cannot call protected application services.

## Incremental Implementation Sequence

### Slice 5A: Contracts and Configuration

Add provider-neutral external identity, user-status, and session contracts plus
validated authentication options. Add no live provider integration.

Exit gate: boundary and configuration tests pass; no provider types enter core
contracts.

### Slice 5B: Identity and Session Persistence

Add forward-only SQL migrations and Dapper adapters for `Users`,
`ExternalIdentities`, and `UserSessions`. Keep Creator memberships deferred.

Exit gate: real SQL Server tests prove uniqueness, concurrency, disablement,
expiry, revocation, transaction rollback, ordinal issuer/subject identity lookup
and uniqueness under the configured case-sensitive collation, concurrent
monotonic `LastSeenAtUtc` updates with coalescing, and no email-based linking.

### Slice 5C: Deterministic Authentication Adapters

Add the Development-only adapter, test-only scheme, actor-context mapping, and
server-side session validation without a live External ID dependency.

Exit gate: negative environment tests and private-policy integration tests pass.

### Slice 5D: External ID Adapter

Add reviewed ASP.NET Core/Microsoft Identity Web packages, certificate-backed
configuration, OIDC events, identity resolution, and safe failure mapping.

Exit gate: protocol configuration tests pass; no token persistence; provider
types remain in adapters.

### Slice 5E: Endpoints and Browser Security

Add sign-in, callback plumbing, POST sign-out, error/access-denied pages,
antiforgery defaults, headers, and return-target validation. UI remains minimal.

Exit gate: browser and pipeline security matrix passes.

### Slice 5F: Azure Integration and Smoke Test

Provision separate non-production External ID configuration, Key Vault
certificate, durable Data Protection keys, exact redirects, and environment
settings through reviewed infrastructure changes.

The provisioned development inventory, IaC boundary, private execution gates,
External ID operations, SQL bootstrap/migration, certificate lifecycle, and Data
Protection procedures are governed by:

- `docs/development/slice-5f-azure-environment.md`;
- `docs/development/external-id-environment-runbook.md`;
- `docs/development/azure-sql-migration-runbook.md`; and
- `docs/development/authentication-key-management-runbook.md`.

Exit gate: sign-in, sign-out, restart, certificate readiness, session revocation,
public-route availability, and Creator authorization smoke tests pass without
private telemetry leakage.

Do not combine these slices into one authentication rewrite.

## Explicitly Deferred

- Creator membership persistence, which remains Phase 3 Slice 6;
- account linking and unlinking UI;
- universal passkey enrollment;
- SMS as a default factor;
- Microsoft Graph and downstream-provider API access;
- provider tokens stored for background work;
- support impersonation;
- Planning Engagement authentication behavior;
- native mobile authentication;
- bearer-token public APIs;
- production identity collection before environment separation, recovery,
  certificate rotation, Data Protection, monitoring, and rollback are approved.

## Sources Reviewed

- [ASP.NET Core .NET 10 OIDC web authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0)
- [Microsoft Identity Web certificate authentication](https://learn.microsoft.com/en-us/entra/msidweb/authentication/certificates)
- [ASP.NET Core .NET 10 cookie authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0)
- [ASP.NET Core .NET 10 antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
- [ASP.NET Core Data Protection key storage](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0)
- [Blazor .NET 10 authentication and circuit revalidation](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0)
- [Microsoft Entra access revocation](https://learn.microsoft.com/en-us/entra/identity/users/users-revoke-access)
- [External ID security operations](https://learn.microsoft.com/en-us/entra/architecture/deployment-external-operations)
- [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html)
- [ASP.NET Core .NET 10 SignalR security](https://learn.microsoft.com/en-us/aspnet/core/signalr/security?view=aspnetcore-10.0)

Revalidate package support, External ID capabilities, credential support,
browser behavior, pricing, and platform limits before live integration.
