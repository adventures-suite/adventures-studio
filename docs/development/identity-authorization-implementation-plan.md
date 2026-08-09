# Identity and Authorization Implementation Plan

**Status:** Approved for Incremental Phase 3 Implementation

**Last Updated:** August 7, 2026

## Objective

Protect private Creator-owned Planning operations through provider-independent
identity, explicit resource authorization, deterministic revocation, and
auditable server-side enforcement. Do not begin with login screens.

Read first:

- `AGENTS.md`
- `docs/DECISIONS.md`
- `docs/architecture/identity-authorization.md`
- `docs/architecture/security.md`
- `docs/architecture/creator-engine.md`
- `docs/architecture/planning-engine.md`
- `docs/architecture/partner-collaboration-engine.md`
- `docs/architecture/observability.md`
- `docs/architecture/audit-reporting.md`

## Working Rules

- Authentication identifies a human; authorization evaluates an operation on a
  Creator-owned resource.
- Creator, user, membership, workload, and future engagement identities remain
  distinct.
- Enforce policies below the UI.
- Deny safely when ownership cannot be proven.
- Agency membership never grants customer access.
- Provider claims and framework policy types remain in adapters.
- Full Planning Engagement implementation remains deferred.
- Complete each slice and its negative tests before continuing.
- Follow the platform telemetry taxonomy; authorization telemetry never replaces
  required audit records.
- Follow the platform audit and reporting architecture. Slice 5A introduces
  only the minimum provider-neutral audit vocabulary and authentication action
  classifications it requires; persistence and reporting remain in their
  approved incremental slices.

## Authoritative Immediate Sequence

Before Phase 3 authentication implementation continues:

1. preserve the completed deployment run, retained package, health payload, and
   workflow result as release evidence;
2. implement the App Service immutable-package activation correction as a
   separate operational commit under `docs/development/deployment.md`;
3. deploy that correction and prove the expected SHA, Creator validation,
   Resource validation, repeatable sequencing, diagnostics, and rollback gate;
4. implement Authentication Integration Slices 5A through 5F in order;
5. implement Creator membership persistence in Slice 6; and
6. begin Planning authorization enforcement and protected UI only through
   Slices 7 and 8 after actor resolution, sessions, and membership persistence
   are dependable.

Do not fold the package-activation correction into a Slice 5 feature commit.
Do not begin Planning authorization enforcement merely because authentication
contracts exist; authenticated actor resolution, revocable application
sessions, and Creator membership persistence must first pass their gates.

## Slice 1: Architecture and Threat Model

Scope: identity boundaries, permission vocabulary, policy model, threat model,
audit requirements, authorization matrix, decisions, and agent guidance.

Exclusions: application code, packages, identity-provider selection,
authentication, database tables, routes, and UI.

Exit gate: architecture and security review approve the boundaries and matrix.

## Slice 2: Framework-Independent Contracts

Scope:

- strongly typed `UserId` and membership identity;
- `ActorIdentity`, actor type, optional human `UserId`, and initiating actor;
- permission value and authorization resource context with explicit Creator
  collection and resource-instance scopes;
- authorization request, decision, and denial reason;
- policy evaluator contract;
- audit actor and required audit event contract.

Acceptance criteria:

- every request carries explicit actor context, Creator, operation, and resource
  scope; the actor may be absent only to represent an anonymous request that
  must be denied safely;
- human-only policies reject system and background actors;
- create, active-list, and archived-list operations authorize collection scope;
- default or mismatched identities fail predictably;
- contracts expose no ASP.NET Core, OIDC, Entra, cookie, or provider types;
- professional proposal versus direct-edit permissions remain distinct;
- unit tests cover the authorization matrix with deterministic fakes.

## Slice 3: Policy and Permission Evaluation

Scope: initial role bundles, explicit policies, ownership requirements, archive
and restore rules, membership version and status semantics, and audit intent.

This slice authorizes authenticated human actors through current Creator
membership only. System and background actors are denied with an explicit
unsupported-actor or human-required reason, as applicable, until a separate
provider-neutral workload authorization basis is approved. Queued work must not
infer authority from a human role bundle.

Acceptance criteria: policies are composable, default-deny, resource-aware, and
independent of UI; agency membership cannot satisfy customer-plan policy.

## Slice 4: Identity-Provider Selection

Evaluate supported sign-in protocols, account recovery, MFA, session and token
revocation, local development, CI automation, cost, privacy, operational burden,
and migration portability. Record an approved decision before adding a package.

Decision: Microsoft Entra External ID in an external tenant is the initial
human CIAM provider. Use browser-delegated OIDC authorization code flow with
PKCE and preserve issuer-plus-subject mapping behind an infrastructure adapter.
Application sessions, Creator membership, authorization, and revocation remain
AdventuresSuite responsibilities. Full constraints are defined in
`docs/architecture/identity-provider.md`.

Exit gate: provider and protocol decision approved; recovery, MFA, session,
revocation, environment, local-development, CI, cost, privacy, and migration
boundaries documented; no authentication package or login UI added.

## Slice 5: Authentication Integration

Map validated provider identity to stable platform `UserId`. Establish secure
session behavior, CSRF protection, security headers, logout, revocation, and
safe authentication errors. Workload identity remains separate.

Preserve exact case-sensitive issuer/subject identity in contracts and SQL;
activate private authentication only on the canonical workspace host; validate
the exact workspace origin for every SignalR transport; and coalesce only
non-security-critical session activity writes while keeping revocation and
security-version checks immediate.

Detailed design and six incremental implementation gates are defined in
`docs/architecture/authentication-integration.md`. Complete 5A through 5F in
order. Do not add a live provider package, login UI, or Azure identity resource
before its preceding contracts, persistence, deterministic-adapter, and
security gates pass.

Slice 5F environment integration additionally requires the approved inventory,
IaC reconciliation, private execution path, External ID, SQL migration,
certificate, and Data Protection runbooks rooted at
`docs/development/slice-5f-azure-environment.md`. Slice 6 remains blocked until
the exact live sign-in and infrastructure evidence in that document passes.

## Slice 6: Creator Membership Persistence

Add forward-only migrations and Dapper adapters for memberships, roles or
permission grants, status, version, effective period, and audit metadata.

Acceptance criteria: Creator-scoped keys and indexes, last-owner safety,
concurrency, cross-Creator isolation, deterministic disable/revocation, and real
SQL Server tests.

## Slice 7: Server-Side Planning Enforcement

Protect application services and Planning transaction creation before private
data access. Implement Adventure Plan view, edit, archive, restore, sensitive
reservation, proposal, and audit policies. Return safe denial responses.

## Slice 8: UI and Security Verification

Add protected workspace routes and action visibility only after enforcement is
proven below the UI. Execute the threat-model matrix, IDOR tests, stale-session
tests, leakage tests, host-confusion tests, and accessibility review.

## Deferred

- full Planning Engagement domain and persistence;
- agency customer enumeration;
- direct professional editing UI;
- broad organization administration;
- billing and partner commercial terms;
- support impersonation implementation;
- AI provider integration.

## Phase 3 Exit Gate

- anonymous and unauthorized access is denied below the UI;
- active membership and permissions are Creator-scoped;
- IDOR and host confusion fail safely;
- membership revocation has deterministic effect;
- archive and restore require explicit permission and audit;
- audit and leakage tests pass;
- public Creator resolution remains independent;
- threat-model and authorization review are approved.
