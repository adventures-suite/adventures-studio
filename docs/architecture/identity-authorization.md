# AdventuresSuite Identity and Authorization Architecture

**Version:** 1.0

**Status:** Approved Direction

**Last Updated:** August 7, 2026

## Purpose

Identity and Authorization protect private Creator-owned capabilities without
coupling domain contracts to an authentication vendor or scattering role checks
through pages and components.

## Governing Rule

> Authentication establishes who the user is. Authorization determines which
> Creator-owned resource they may access for this operation.

Authentication never selects a Creator, proves ownership of an Adventure Plan,
or grants access merely because a public host resolves successfully.

## Identity Boundaries

### Human User Identity

`UserId` identifies one authenticated human independently of email address,
display name, authentication provider, agency, and Creator. Provider subject
identifiers map to the platform user through an infrastructure adapter.

### Creator Identity

`CreatorId` remains the tenancy and ownership boundary. It is not a user,
session, organization role, hostname, or authentication claim. A user may be a
member of multiple Creators, and a Creator may have multiple users.

### Creator Membership

A membership is the explicit, revocable relationship between one user and one
Creator. It carries status, roles or permissions, effective dates, version, and
audit metadata. Membership in one Creator grants nothing in another.

Agency staff use ordinary membership in the agency Creator. Agency membership
does not grant customer access. Future customer-plan access additionally
requires an accepted, active, plan-scoped Planning Engagement.

### Customer Ownership

The customer Creator owns its Adventure Plans and derived private or public
records. Ownership is read from authoritative data, never inferred from the
request host, current navigation, agency relationship, or a supplied identifier.

### Actor Identity and Workload Identity

Azure Managed Identities and deployment principals identify workloads, not
humans. They receive infrastructure permissions but do not become platform
users, Creator members, or audit actors for human decisions.

Core operations use an `ActorIdentity` with an explicit actor type such as
Human, System, BackgroundJob, or Support. A human actor carries a required
`UserId`; a non-human actor does not fabricate one. Background work carries its
own actor identity plus the initiating human actor when applicable. Policies
that require consent, approval, membership administration, or another human
decision explicitly require a Human actor.

## Authorization Context

Every protected operation builds a framework-independent context containing:

- explicit `ActorIdentity` and optional initiating actor;
- explicit `CreatorId`;
- resource type and resource scope;
- requested permission;
- active membership and applicable delegated relationship;
- plan version or other concurrency context when required;
- correlation and audit context.

The caller supplies resource intent, but authoritative ownership is loaded and
compared at the application or persistence boundary. A mismatch fails without
revealing whether another Creator owns the identifier.

Resource scope distinguishes a Creator-owned collection from an existing
resource instance:

```text
CreatorCollection(CreatorId, AdventurePlan)
AdventurePlanInstance(CreatorId, AdventurePlanId)
```

Creation and normal or archived listing authorize the collection scope. View,
edit, archive, restore, and sensitive-field operations authorize an instance
scope whose ownership is verified authoritatively. A caller does not invent a
placeholder resource identity for a collection operation.

## Initial Permission Vocabulary

Permissions describe operations rather than UI locations:

- `Creator.View`
- `Creator.ManageMembers`
- `AdventurePlan.View`
- `AdventurePlan.Create`
- `AdventurePlan.Edit`
- `AdventurePlan.ViewArchived`
- `AdventurePlan.Archive`
- `AdventurePlan.Restore`
- `AdventurePlan.ViewSensitiveReservations`
- `PlanningProposal.Submit`
- `PlanningProposal.Review`
- `PlanningProposal.ApplyApproved`
- `PlanningEngagement.Invite`
- `PlanningEngagement.Manage`
- `PlanningEngagement.DirectEdit`
- `Audit.View`
- `Support.Impersonate`

The Planning Engagement permissions establish future vocabulary only. They do
not authorize access or start Partner Collaboration implementation in Phase 3.
Proposal submission is deliberately weaker than direct edit.

## Roles and Policies

Roles are Creator-scoped permission bundles for administration convenience.
Initial candidate roles are Owner, Administrator, Planner, Contributor, and
Viewer. Agency roles are scoped to the agency Creator and do not cross into a
customer Creator.

Application code authorizes named policies and permissions. Razor components
may hide unavailable actions for usability, but UI checks are not enforcement.
Protected application services and persistence operations enforce authorization
before accessing private data.

Policies must support resource-specific facts. For example:

```text
CanRestoreAdventurePlan =
    authenticated user
    + active membership in owning Creator
    + AdventurePlan.Restore
    + target plan belongs to that Creator
    + target plan is Archived
```

## Session and Revocation Semantics

- Disabled users and memberships lose access predictably.
- Membership and permission versions invalidate stale authorization state.
- High-risk operations re-evaluate current membership rather than trusting a
  long-lived UI or cached decision.
- Authorization caches include user, Creator, permission, membership version,
  and expiry; denial is safer than a stale grant.
- Revocation affects background work not yet executed.

## Public Host Separation

Public Creator host resolution remains independent of private authorization.
An approved host may establish public Creator Context for public content, but a
private operation must still use authenticated user and authoritative resource
ownership. Host headers and route identifiers never broaden membership.

## Audit Boundary

Audit events identify the human or system actor, Creator, resource scope, action,
outcome, UTC timestamp, correlation identity, and relevant before/after or
version information. Authentication tokens, secrets, confirmation references,
and protected record contents do not enter audit messages.

Required events include:

- membership, role, and permission changes;
- protected plan access and sensitive mutations;
- archive and restore operations;
- engagement invitation, acceptance, expiration, and revocation;
- proposal approval and application;
- direct professional edits;
- support access and administrative impersonation.

For a required mutation, the mutation and durable audit intent commit atomically
in one SQL transaction or through a transactional outbox written in that same
transaction. Failure to persist required audit intent fails and rolls back the
mutation.

Sensitive reads that policy marks as audit-required fail closed when durable
audit recording is unavailable. Lower-risk reads may use a separately approved,
bounded fallback policy. Rejected attempts use rate-limited, non-blocking
security telemetry where transactional audit is impossible; an attacker must
not be able to exhaust audit storage and deny service merely by generating
unlimited rejected requests. Material rejection evidence remains correlated,
redacted, access-controlled, and subject to retention policy.

## Provider Independence

Core contracts use AdventuresSuite identities, permissions, resource contexts,
decisions, and audit actors. OIDC claims, cookies, ASP.NET Core policy types,
Microsoft Entra identifiers, and provider SDK objects remain in adapters.

Identity-provider selection follows approval of these boundaries and the threat
model. Login screens, membership persistence, and framework middleware do not
belong in this architecture slice.

## Definition of Done for the Architecture Slice

- Human, Creator, membership, customer, agency, and workload identities are
  distinct.
- Permission and policy vocabulary covers current Planning operations and
  future engagement needs without implementing engagements.
- Resource ownership is verified below the UI.
- Threats and negative authorization cases are documented.
- Audit requirements cover privileged and sensitive actions.
- Provider selection remains unresolved until the next approved step.
