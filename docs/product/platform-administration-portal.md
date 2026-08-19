# AdventuresSuite Platform Administration Portal

**Status:** Approved Product Direction

**Last Updated:** August 18, 2026

## Product Intent

AdventuresSuite needs a clear, trustworthy place to administer Creators and
operate the platform. The administrative portal is a dedicated platform
experience, not a Planner feature and not an extension of a public Creator
site.

The experience should help an authorized administrator answer:

- Is AdventuresSuite healthy?
- What version is running and is the schema compatible?
- Are integrations and background processes working?
- Which Creators, subscriptions, and capabilities require attention?
- Who has administrative access and what protected actions occurred?
- Is a support, security, retention, or compliance workflow waiting for action?

## Audiences

### Creator Administrator

Manages one Creator's membership, permissions, settings, subscription summary,
and Creator-scoped evidence. This user cannot administer the platform or see
another Creator.

### Platform Operator

Maintains reliability and delivery using health, release, deployment,
integration, and aggregate operational information. This user does not receive
ordinary access to customer plans or traveler content.

### Security or Compliance Administrator

Performs explicitly authorized access reviews, evidence searches, revocations,
retention operations, legal holds, and incident investigations. Sensitive
actions require stronger controls and complete audit history.

## Experience Principles

- Lead with actionable status, not raw infrastructure detail.
- Use plain language and clear severity, ownership, and next-step guidance.
- Keep each authority lane visually and functionally distinct.
- Show the minimum data needed for the task.
- Make scope, environment, and administrative identity continuously visible.
- Require confirmation, reason capture, and reauthentication for sensitive
  actions.
- Never make customer impersonation the default support experience.
- Treat empty, denied, expired, stale, partial, and failed states as first-class
  product states.
- Support keyboard operation, screen readers, responsive layouts, and light,
  dark, and system themes.

## First Release: Read-Only Operations Dashboard

The first release should provide:

- overall service and dependency health;
- current environment and deployed release;
- migration/schema status;
- background-work and integration-health summaries;
- non-sensitive aggregate Creator and subscription counts; and
- capability or feature-rollout status.

It must not expose private Creator content. Every value should identify its
freshness, source, and safe failure state. Operational counts are not billing
authority, authorization evidence, or customer-content access.

## Later Capability Slices

1. Creator membership and permission administration.
2. Administrative session and access-review reporting.
3. Creator-scoped audit history.
4. Security and compliance evidence search.
5. Failed-job investigation with redacted, non-content context.
6. Retention, deletion, legal-hold, and protected evidence export.
7. Just-in-time support access, only after separate policy and threat-model
   approval.

Each mutation is delivered independently. A broad administrative console is
not released merely because the read-only dashboard exists.

## Trust Commitments

- Platform administration never silently grants access to every customer.
- Creator ownership and private Adventure data remain protected.
- Administrative access is explicit, least-privilege, revocable, and audited.
- Emergency access is exceptional, expiring, reason-bound, and reviewable.
- Customer data is not copied into operational dashboards for convenience.
- Reports and exports are purpose-built, bounded, protected, and traceable.

## Success Measures

The portal succeeds when authorized administrators can identify and resolve
platform and Creator-administration issues quickly while tests and evidence
prove that ordinary users, unrelated Creators, and insufficiently privileged
operators cannot access the portal's services or protected data.

## Not Part of This Product

- Planner itinerary or Adventure management;
- Companion traveler workflows;
- public-site content management;
- unrestricted database administration;
- silent customer impersonation;
- a general-purpose analytics warehouse or report builder; or
- direct booking, payment, or provider operations.
