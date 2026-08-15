# AdventuresSuite Platform Billing and Entitlements Architecture

**Version:** 1.1

**Status:** Approved Direction

**Last Updated:** August 11, 2026

## Purpose

Platform Billing and Entitlements govern how Creators, agencies, organizations,
and future enterprise customers pay Adventures Studio to use AdventuresSuite
and which platform capabilities they receive.

This is separate from the Commerce Engine, which lets a Creator sell books,
prints, licenses, and other products to the Creator's customers.

```text
Platform Billing
    Customer pays Adventures Studio for AdventuresSuite capabilities

Creator Commerce
    Customer pays a Creator for a Publication, Resource, or service
```

The two capabilities must not share orders, subscriptions, entitlements,
payment state, reporting, or merchant responsibilities merely because they may
eventually use the same payment provider.

## Governing Rule

> Identity establishes who the person is. Membership and authorization decide
> what that person may do. Platform entitlement decides what the Creator has
> purchased or been granted. Rollout and availability decide whether the
> platform can currently provide it.

An operation that consumes a paid capability requires all applicable gates:

```text
Authenticated actor
    AND authorized Creator operation
    AND active PlatformEntitlement
    AND enabled feature rollout
    AND available dependency
```

Passing one gate never implies another. A subscription does not grant a user a
role, an Owner role does not purchase a feature, a feature flag does not create
a contractual entitlement, and an available service does not authorize access.

## Language and Boundaries

Use these distinct terms:

- `CreatorMembership`: a user's role and permission relationship to a Creator;
- `PlatformEntitlement`: a Creator's right to use an AdventuresSuite
  capability;
- `CommerceEntitlement`: a customer's right to access a Creator-sold digital
  product or protected experience;
- `FeatureFlag`: an operational rollout, experiment, or emergency control;
- `ServiceAvailability`: whether the environment and dependencies can safely
  provide a capability; and
- `UsageAllowance`: a contractual or granted quantity, not an authorization
  role.

Do not use the bare term `Entitlement` in new cross-boundary contracts.

## Core Concepts

### BillingAccount

A `BillingAccount` identifies the party responsible for paying Adventures
Studio. It may initially correspond one-to-one with a Creator, but it is not a
tenant or authorization boundary. Future agencies or organizations may pay for
more than one Creator without acquiring access to their data.

### PlatformCustomer

A `PlatformCustomer` is the legal or commercial customer associated with a
Billing Account. Contact and tax information are minimized and kept separate
from Creator brand, user identity, and subscriber records.

### PlatformProduct and PlatformPrice

A `PlatformProduct` represents a commercial AdventuresSuite offering. A
versioned `PlatformPrice` defines currency, billing interval, market, effective
period, and external provider reference. Historical subscriptions retain their
agreed price identity even after the public catalog changes.

### PlanDefinition and PlanVersion

A plan is a marketing bundle of capabilities and allowances. Plan names are not
authorization vocabulary. Every published plan version is immutable; changing
features or limits creates a new version. Grandfathering and migration are
explicit commercial decisions.

### PlatformSubscription

A `PlatformSubscription` relates a Billing Account to a Platform Price and
tracks the provider-independent lifecycle:

- `Trialing`;
- `Active`;
- `GracePeriod`;
- `PastDue`;
- `Suspended`;
- `Canceled`; and
- `Expired`.

Provider status is mapped into this vocabulary through an adapter. Raw provider
status, checkout responses, and browser redirects are never authoritative
platform state.

### PlatformCapability

Capabilities use stable, provider-neutral names rather than plan comparisons.
Potential families include:

- Planning;
- AI planning and authoring;
- AdventuresCompanion;
- advanced publishing and print-ready export;
- custom domains and advanced branding;
- subscriber notifications;
- Creator storefront and photography licensing;
- professional collaboration;
- advanced reporting;
- team seats;
- storage; and
- approved API or integration access.

The exact vocabulary is introduced incrementally with implemented features.
Code must never branch on a display plan name such as `Professional`.

### PlatformEntitlement

A `PlatformEntitlement` grants one capability to one explicit target, normally
a Creator. It records source, plan version or manual grant, effective period,
status, limits, version, and safe audit provenance.

Entitlements can arise from a paid subscription, trial, promotional grant,
contract, support remedy, or approved migration. Manual grants are time-bound
where possible, require stronger permission and reason, and are fully audited.

Every time-bounded grant records a trusted UTC effective time, optional UTC
expiration, status, version, source, and supersession or revocation evidence.
Expiration is evaluated below the UI for every protected operation and again
when delayed background work executes. Opening a screen before expiration does
not authorize a later mutation. User-facing remaining-time displays are derived
projections; countdown ticks are never persisted as entitlement state.

### SeatAllocation

A seat allocation limits how many active memberships may consume a plan's team
capacity. A seat does not create membership, bind an external identity, choose a
role, or grant access. Membership administration and last-owner safety remain
owned by Identity and Authorization.

### UsageAllowance and UsageLedger

Usage allowances may govern storage, AI units, notifications, exports, or other
measured capabilities. Metering records immutable, idempotent usage facts with
Creator, capability, period, quantity, source, and correction references.

Operational metrics and analytics do not become the billing ledger. Estimated
telemetry may inform product design but cannot produce an invoice or deny a
contractual capability.

## Entitlement Evaluation

Application services request a decision through a provider-neutral evaluator
using explicit Creator, capability, operation context, and trusted current time.
The result is a bounded decision such as:

- entitled;
- not entitled;
- allowance exhausted;
- subscription restricted;
- feature unavailable; or
- indeterminate and therefore denied safely.

The evaluator reads authoritative platform state. Browser claims, workspace
cookies, Entra claims, mobile tokens, query strings, feature flags, and payment
redirects cannot assert entitlement.

Entitlement checks occur below the UI. The UI may hide or explain unavailable
features, but application services enforce the decision again. Background jobs
carry Creator and capability context and re-evaluate time-sensitive entitlement
when execution could occur after expiration or revocation.

Decisions are cacheable only under a documented, short, version-aware policy.
Security-sensitive revocation, administrative suspension, or corrected grants
must have deterministic invalidation.

Cache keys include the Creator, capability, relevant relationship or actor
scope, entitlement version, and applicable expiry. A cache entry never lives
past the authoritative grant expiration. Offline clients consume only bounded,
versioned entitlement projections and fail safely when the projection becomes
too stale.

## Workspace Tool Projection

The private workspace consumes a provider-neutral projection of tools applicable
to the current actor, Creator, resource, and product state. Each registered tool
identifies its stable Platform Capability, required permission, resource and
relationship context, applicable allowance, rollout and availability gates, and
presentation metadata.

The projection may classify a tool as available, discoverable as a commercial
upgrade, or omitted. A missing subscription capability may be explained without
claiming authorization. A role-, traveler-policy-, engagement-, or
resource-denied tool normally remains omitted to avoid disclosing protected
relationships or resource state.

Navigation projection is not enforcement. Application services, APIs, exports,
and background work independently evaluate authorization, entitlement,
allowances, rollout, and service availability. Browser state, a rendered icon,
a hidden tool, or a forged route cannot grant or deny authoritative capability.

## Plan and Packaging Direction

Initial product direction uses three understandable plans: Free, Explorer, and
Navigator. Free is a genuine versioned subscription that provides bounded real
utility. Explorer is intended to be the complete choice for most travelers.
Navigator adds meaningful advanced convenience, intelligence, collaboration,
readiness, outputs, or capacity without manufacturing limitations in Explorer.

Plans remain versioned bundles over stable capabilities and allowances. Trials,
previews, launch offers, and support remedies are time-bounded grants layered
over a base plan rather than additional hidden tiers. Add-ons can supplement a
plan without creating a new role or code path, but the initial product should
prefer simple packaging over a large add-on catalog.

Potential add-ons include additional seats, storage, AI usage, notification
volume, advanced publishing, commerce, professional collaboration, custom
domains, and reporting.

Price, tax treatment, refund terms, free limits, and exact bundles are business
decisions, not architectural constants.

Adding a capability to Free, Explorer, or Navigator creates a new immutable plan
version. Whether existing subscribers receive it automatically, opt in, or
retain a grandfathered version is an explicit, audited reconciliation policy.
Feature rollout remains separate: entitlement does not promise that a feature
is enabled for a cohort, and rollout never creates contractual entitlement.

## Billing Lifecycle and Customer Protection

Payment failure must not silently delete, publish, transfer, or permanently hide
a Creator's work. The approved commercial policy defines behavior for each
subscription state. A safe default is:

- preserve existing private and public data;
- maintain account recovery and bounded export;
- keep public Publications stable unless a separate published policy says
  otherwise;
- restrict selected premium creation or execution capabilities rather than
  destroying records;
- provide clear remediation and support paths; and
- define retention and eventual closure separately from payment collection.

Cancellation, expiration, downgrade, and allowance reduction require preview,
effective time, data-impact explanation, and deterministic reconciliation.
Downgrade cannot make existing data cross Creator boundaries or disappear
without the approved retention process.

Subscription or promotional expiry does not revoke Creator membership or alter
Planning lifecycle state. Seats remain capacity rather than identity or access.
Where policy permits, existing data remains viewable, recoverable, and
exportable while selected new premium operations are restricted. Grace periods
and support overrides are explicit, time-bounded, reasoned, and audited.

## Provider Integration

Payment, invoicing, tax, and customer-portal providers remain adapters. Core
contracts contain no provider SDK types or event names. AdventuresSuite stores
provider references, not payment-card data, bank credentials, or raw payment
payloads.

Provider webhooks are untrusted input and require signature validation, replay
protection, bounded payloads, event allowlists, idempotent transactional inbox
processing, ordering-aware state transitions, safe retries, and reconciliation.
An accepted webhook records provider evidence and drives a platform transition;
it does not directly grant UI claims or Creator permissions.

Checkout completion in the browser is not proof of payment. AdventuresSuite
confirms authoritative provider state through the approved server boundary.

## Data and Transaction Boundaries

Azure SQL is the initial expected store for billing accounts, products, prices,
subscriptions, grants, allowances, usage ledgers, webhook inbox, reconciliation,
and audit intent. Logical schemas and least-privilege identities remain separate
from Creator Commerce.

Subscription changes, resulting entitlement state, usage corrections, and
required audit intent commit atomically where they share one database boundary
or use a transactional outbox. Cross-provider calls are never held inside a
long-lived SQL transaction.

Financial and entitlement records use optimistic concurrency, UTC effective
times, immutable provider references, append-oriented history, and recoverable
correction entries rather than destructive rewriting.

## Security, Privacy, and Audit

Protected operations include billing-account administration, checkout/session
creation, subscription changes, plan migration, manual grants, allowance
adjustments, refunds or credits, payment-method portal access, evidence export,
and reconciliation overrides.

Controls include:

- separate billing permissions and step-up authentication for high-risk work;
- no authority inferred from payer email or provider customer identity;
- Creator isolation at query, cache, job, export, and reporting boundaries;
- generic safe failures that prevent customer or subscription enumeration;
- no secrets, payment data, raw webhook bodies, invoices, contact data, or
  provider tokens in logs and analytics;
- required append-oriented audit intent for sensitive mutations and reads; and
- audited platform support access with no silent impersonation.

## Reporting and Reconciliation

Creator-facing billing views use purpose-built projections scoped to the
authorized Billing Account and Creator relationship. Platform financial reports
use separate permissions and reconcile provider settlements, subscriptions,
invoices, credits, taxes, usage, and entitlement state without exposing another
customer's information.

Operational telemetry, product analytics, the usage ledger, invoices, and
financial reconciliation are distinct data products.

## Reliability and Failure Behavior

The platform defines behavior for provider outage, webhook delay, duplicate or
out-of-order events, reconciliation disagreement, database failure, clock
skew, expired checkout, charge dispute, refund, and notification failure.

Previously proven entitlement should not disappear solely because the payment
provider is temporarily unavailable. New or ambiguous grants fail safely while
reconciliation and support tooling surface the degraded state. Billing-provider
availability never blocks Creator data recovery or platform rollback.

## Implementation Timing

This architecture is recorded before paid enforcement is needed. Current
development remains unmetered unless an explicitly approved test entitlement is
required. Do not add a payment provider, pricing checks, or billing tables to
the current Phase 3 identity work.

Detailed delivery gates are defined in
`docs/development/platform-billing-entitlements-implementation-plan.md`.
Product experience and packaging principles are defined in
`docs/product/workspace-experience-and-value.md`.
