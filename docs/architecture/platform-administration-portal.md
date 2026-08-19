# AdventuresSuite Platform Administration Portal Architecture

**Version:** 1.0

**Status:** Approved Direction; Implementation Deferred

**Last Updated:** August 18, 2026

## Purpose

AdventuresSuite will provide an administrative portal for operating the
platform, administering a Creator, and reviewing authorized security and
compliance evidence. This is a platform capability. It is not part of Planner,
Companion, a public Creator site, or an ordinary customer workspace menu.

The portal must make AdventuresSuite supportable without creating a silent
universal path into private customer data.

## Governing Principle

> Administrative responsibility does not imply unrestricted customer-data
> access.

Every administrative read and mutation requires an explicit authority, a
defined purpose, the minimum projection necessary for that purpose, and an
auditable outcome. Platform roles never substitute for Creator membership,
Adventure participation, or resource-level authorization.

## Administrative Authority Lanes

The portal has three distinct lanes. They may share visual foundations, but
they do not share an implicit authorization grant.

### Creator Administration

Creator administration is scoped to one Creator and supports authorized
management of:

- members, invitations, roles, and permissions;
- subscription and entitlement summaries;
- Creator settings and approved integration status;
- Creator-scoped audit history and reports; and
- access revocation and recovery workflows.

A Creator administrator has no platform-wide authority and no access to
another Creator merely because that person administers one Creator.

### Platform Operations

Platform operations supports the safe operation of AdventuresSuite through
minimal, non-content projections such as:

- service health and deployed release identity;
- migration and schema compatibility status;
- failed or delayed background work;
- integration and delivery health;
- aggregate Creator, subscription, and capability counts;
- environment and feature-rollout status; and
- bounded operational incident context.

Routine platform operations must not expose private plans, traveler details,
reservation credentials, private media, precise location, or customer-authored
content.

### Security and Compliance

Security and compliance supports narrowly authorized:

- audit-evidence search using safe identifiers and bounded time ranges;
- access reviews, revocations, and administrative-session review;
- retention, deletion, legal-hold, and evidence-export workflows;
- security-event investigation; and
- emergency-access review.

Access to sensitive audit evidence is itself a protected, audited action.

## Identity and Authorization Boundary

Administrative identities and permissions are separate from Creator
membership. There is no shared super-administrator account and no role that
silently grants ordinary access to all Creator-owned resources.

The administrative host or route boundary must use:

- strong multifactor authentication;
- explicit platform-administration permissions;
- short, revocable administrative sessions;
- reauthentication for sensitive operations;
- resource-aware authorization below the UI;
- exact environment and host validation; and
- default denial when authority, purpose, or target scope cannot be proven.

Support personnel do not impersonate customers as a routine workflow. Any
future customer-data access requires a separately approved just-in-time access
operation with reason capture, explicit target scope, limited duration,
customer or designated approval where policy requires it, complete audit, and
automatic expiry.

## Data-Minimization Rules

Administrative screens consume purpose-built projections, not unrestricted
joins across operational databases. Queries, indexes, caches, exports, and
background work preserve their authorized scope.

The portal must not display or export secrets, tokens, raw claims, payment-card
data, passport or medical details, booking PINs, confirmation references,
private notes, signed Resource URLs, precise traveler location, or arbitrary
request and response bodies.

Customer content is excluded from routine platform dashboards. When a future
support case genuinely requires protected content, that access uses a separate
approved policy and never becomes a side effect of viewing an operational
dashboard.

## Audit and Reporting

Administrative queries and mutations follow
`docs/architecture/audit-reporting.md`. Required evidence includes the actor,
administrative authority, purpose or reason category, target scope, action,
outcome, UTC time, correlation identity, session identity when permitted, and
previous/resulting versions for mutations.

Audit metadata is minimal and allowlisted. It does not copy the protected data
that an administrator viewed or changed. Required mutations and audit intent
commit atomically. Large evidence exports are bounded, encrypted, expiring,
watermarked where appropriate, and delivered through protected channels.

## Initial Product Boundary

The first implementation is read-only platform operations. It may expose only:

- service and dependency health;
- deployed release and environment identity;
- migration/schema status;
- non-sensitive aggregate counts;
- failed background-work summaries without customer payloads; and
- feature/capability rollout status.

Creator membership administration, audit-evidence access, support elevation,
retention, legal hold, and other mutations are separate later slices with their
own threat models and approval.

## Security Requirements

- Administrative UI visibility never establishes authority.
- Every service operation reauthorizes the exact administrative action.
- Ordinary Creator users cannot invoke administrative services by forging a
  route, identifier, claim, cookie, or client request.
- Administrative permissions are least-privilege and independently revocable.
- Sensitive operations require reason capture and may require step-up
  authentication or two-person approval.
- Administrative sessions and exports expire.
- Queries are bounded and resistant to enumeration and bulk extraction.
- Emergency access is disabled by default, time-limited, and reviewed after
  use.
- Production administration cannot be activated through development settings
  or a public Creator host.

## Explicit Exclusions

This architecture does not authorize:

- a universal super-admin account;
- silent impersonation;
- unrestricted SQL browsing;
- direct mutation of Engine tables;
- support access inferred from employment or a platform role;
- customer-content search as an ordinary operational feature;
- bypass of Creator, traveler, partner, consent, lifecycle, or entitlement
  policy;
- audit storage in ordinary application logs; or
- implementation inside Planner or Companion.

## Related Documents

- `docs/architecture/identity-authorization.md`
- `docs/architecture/security.md`
- `docs/architecture/audit-reporting.md`
- `docs/architecture/observability.md`
- `docs/architecture/platform-billing-entitlements.md`
- `docs/product/platform-administration-portal.md`
- `docs/development/platform-administration-implementation-plan.md`
