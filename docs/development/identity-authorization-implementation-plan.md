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

- every request carries explicit actor, Creator, operation, and resource scope;
- human-only policies reject system and background actors;
- create, active-list, and archived-list operations authorize collection scope;
- default or mismatched identities fail predictably;
- contracts expose no ASP.NET Core, OIDC, Entra, cookie, or provider types;
- professional proposal versus direct-edit permissions remain distinct;
- unit tests cover the authorization matrix with deterministic fakes.

## Slice 3: Policy and Permission Evaluation

Scope: initial role bundles, explicit policies, ownership requirements, archive
and restore rules, membership version and status semantics, and audit intent.

Acceptance criteria: policies are composable, default-deny, resource-aware, and
independent of UI; agency membership cannot satisfy customer-plan policy.

## Slice 4: Identity-Provider Selection

Evaluate supported sign-in protocols, account recovery, MFA, session and token
revocation, local development, CI automation, cost, privacy, operational burden,
and migration portability. Record an approved decision before adding a package.

## Slice 5: Authentication Integration

Map validated provider identity to stable platform `UserId`. Establish secure
session behavior, CSRF protection, security headers, logout, revocation, and
safe authentication errors. Workload identity remains separate.

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
