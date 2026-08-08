# AdventuresSuite Identity and Authorization Threat Model

**Version:** 1.0

**Status:** Approved Phase 3 Baseline

**Last Updated:** August 7, 2026

## Scope

This threat model covers authenticated access to private Creator-owned Planning
data, Creator membership, future professional collaboration, background work,
and supporting caches, logs, exports, and AI context. Public content security
continues to follow Creator host-resolution and publication policies.

## Protected Assets

- Creator membership and delegated permissions
- private Adventure Plans and archived plans
- traveler preferences, reservation references, notes, budgets, and tasks
- audit history and proposal approvals
- future Planning Engagements
- protected Resources and exports
- authorization caches and background-work envelopes

## Trust Boundaries

```text
Browser or API client
    ↓ untrusted request
Authentication adapter
    ↓ ActorIdentity with optional human UserId
Authorization policy boundary
    ↓ permitted Creator + resource + operation
Application and Planning transaction
    ↓ Creator-scoped persistence
Azure SQL / protected Resource providers
```

Public host resolution, authentication, authorization, persistence, external
identity providers, AI providers, and background workers are separate trust
boundaries. Success at one boundary does not imply success at another.

## Principal Threats and Required Controls

### Cross-Creator Disclosure and IDOR

An attacker changes an Adventure Plan or child identifier to access another
Creator's record.

Controls: explicit Creator scope in every operation; authoritative ownership
lookup; composite Creator keys and predicates; indistinguishable not-found or
denied responses where disclosure matters; negative cross-Creator tests.

### Host and Creator-Context Confusion

A forged Host header or stale public Creator Context is reused for a private
operation.

Controls: approved-host resolution; unknown-host rejection; private policies do
not infer membership from host; explicit authenticated Creator scope; no default
Creator fallback.

### Membership Privilege Escalation

A member grants itself a stronger role, changes another membership, or exploits
a stale administrative view.

Controls: `Creator.ManageMembers` policy; last-owner protection; concurrency;
current membership-version checks; complete audit; no self-elevation through
client-submitted claims.

### Stale, Disabled, or Revoked Sessions

A valid authentication session retains authorization after membership or user
revocation.

Controls: short bounded authorization cache; membership status and version;
revalidation for sensitive actions; deterministic revocation; session and token
expiry; background jobs reauthorize at execution time when appropriate.

### Agency-to-Customer Scope Escalation

Agency membership is mistaken for permission to access a customer plan.

Controls: agency membership alone is insufficient; future access requires a
matching active Planning Engagement, delegated permission, customer Creator,
and Adventure Plan; proposal permission does not imply direct edit.

### Background Work Under the Wrong Creator

A queued job loses or substitutes Creator or actor context.

Controls: immutable work envelope with Creator, resource, operation, initiating
actor, and authorization basis; Creator-scoped idempotency keys; validation at
enqueue and execution; no ambient default Creator.

### Leakage Through Secondary Channels

Private values enter logs, errors, caches, analytics, exports, AI prompts, or
support diagnostics.

Controls: structured redaction; safe error contracts; Creator-scoped cache and
export keys; minimum AI context; protected Resource delivery; audit metadata
without protected payloads; automated leakage tests.

### Administrative Impersonation

Support personnel access customer data without visible authorization.

Controls: disabled by default; explicit `Support.Impersonate`; strong
re-authentication and reason; time-bound session; visible banner; immutable
audit; prohibit silent impersonation and credential sharing.

### Cross-Site Request Forgery

A malicious site causes an authenticated browser to submit a state-changing
request.

Controls: anti-forgery protection for cookie-authenticated mutations; SameSite,
Secure, and HttpOnly cookie settings; origin validation where appropriate;
unsafe operations never use GET; re-authentication for selected high-risk
actions.

### Session Theft and Fixation

An attacker steals, predicts, reuses, or fixes a valid authenticated session.

Controls: rotate session identity at sign-in and privilege changes; bounded
lifetime and inactivity timeout; secure cookies; logout and server-side
revocation; no tokens in URLs or logs; risk-based re-authentication for
sensitive operations.

### Open Redirects and Authentication Endpoint Abuse

An attacker supplies an external return URL, floods authentication endpoints,
or abuses callback and recovery flows.

Controls: local or allowlisted return targets; exact callback validation;
bounded rate limits and lockout protections; generic account responses;
correlation and nonce validation; monitored recovery and sign-in failures.

### Account Linking and Takeover

An external identity is incorrectly linked to an existing platform user, or a
recovered provider account silently gains access.

Controls: stable issuer-and-subject mapping rather than email-only matching;
verified, explicit linking with recent authentication; conflict handling;
notification and audit of link changes; MFA and recovery evaluation during
provider selection.

### Cross-Site Scripting and Authenticated Actions

Injected script reads protected data or performs authorized actions as the
victim.

Controls: contextual output encoding; restrictive Content Security Policy;
avoid unsafe HTML and script injection; sanitize approved rich content;
anti-forgery remains required; security headers and dependency review; never
place secrets or excessive private state in browser-readable storage.

## Authorization Test Matrix

Every protected operation must cover at least:

| Actor and context | Expected result |
| --- | --- |
| Anonymous user | Deny without private-data disclosure |
| Active member creating a plan in an authorized Creator collection | Allow |
| Active member listing active plans with collection view permission | Allow |
| Active member listing archived plans without archive-view permission | Deny |
| Active member listing archived plans with archive-view permission | Allow |
| Active member with permission and matching Creator/resource | Allow |
| Active member without required permission | Deny |
| Member of another Creator using a valid plan identifier | Deny safely |
| Disabled user or membership | Deny |
| Stale membership or permission version | Re-evaluate and deny if revoked |
| Archived plan viewer | Allow only with normal plan-view permission |
| Archived plan restorer without restore permission | Deny |
| Archived plan restorer with matching ownership and permission | Allow and audit |
| Agency member without Planning Engagement | Deny customer access |
| Professional with proposal-only permission | Allow proposal; deny direct edit |
| Expired or revoked future engagement | Deny |
| Background job with mismatched Creator envelope | Reject before data access |
| Support actor without explicit impersonation grant | Deny |
| Background/system actor invoking a human-only approval policy | Deny |
| Cookie-authenticated mutation without valid anti-forgery proof | Deny |
| Authentication callback with invalid state, nonce, or return target | Deny |

Tests must exercise application and persistence boundaries, not only rendered
UI. Denials must not reveal another Creator's membership, resource title,
archive status, or protected fields.

## Audit Verification Matrix

Required tests verify successful and rejected high-risk operations record:

- actor and actor type;
- Creator and resource identity;
- permission or policy evaluated;
- outcome and reason category without secrets;
- UTC timestamp and correlation identity;
- previous and resulting version when mutation succeeds.

Audit storage must be append-oriented and access-controlled. A required mutation
and its durable audit intent commit atomically in one transaction or through a
transactional outbox; audit-intent failure rolls back the mutation. Audited
sensitive reads fail closed unless a reviewed policy explicitly permits a
bounded fallback. Rejected-attempt telemetry is rate-limited and aggregated so
an attacker cannot turn audit volume into a denial of service.

## Review Triggers

Review this model when identity provider, session strategy, membership schema,
Planning Engagement, protected Resources, AI context, exports, background jobs,
support tooling, or public APIs are introduced or materially changed.

Operational logging, tracing, metrics, redaction, sampling, and telemetry access
follow `docs/architecture/observability.md`. Security telemetry and durable audit
records remain distinct even when they share correlation identity.
