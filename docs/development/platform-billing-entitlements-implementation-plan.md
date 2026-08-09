# Platform Billing and Entitlements Implementation Plan

**Status:** Approved Future Incremental Delivery

**Last Updated:** August 8, 2026

## Objective

Introduce provider-neutral SaaS packaging, entitlement evaluation, and billing
without coupling identity, Creator membership, authorization, feature rollout,
or Creator Commerce to a payment provider.

Read first:

- `AGENTS.md`
- `docs/architecture/platform-billing-entitlements.md`
- `docs/product/pricing-model.md`
- `docs/business/business-model.md`
- `docs/architecture/identity-authorization.md`
- `docs/architecture/commerce-engine.md`
- `docs/architecture/audit-reporting.md`
- `docs/architecture/security.md`

## Sequencing Rule

Record capability vocabulary while features are designed, but keep current
development unmetered. Do not introduce billing persistence, provider packages,
webhooks, checkout, usage charging, or paid denial behavior during current
Phase 3 authentication implementation.

## PB1: Product and Capability Vocabulary

Approve initial customer types, stable `PlatformCapability` names, entitlement
targets, plan/version rules, add-ons, allowances, seat semantics, downgrade and
grace behavior, support policy, and the separation from Commerce Entitlements.

Exit gate: product, business, architecture, security, privacy, legal, tax,
accounting, and support owners approve terminology and unresolved decisions are
explicit.

## PB2: Provider-Neutral Entitlement Contracts

Add typed billing, subscription, entitlement, allowance, decision, and version
contracts plus deterministic evaluators and time abstractions. No payment
provider or SQL types enter core contracts.

Exit gate: authorization, entitlement, feature flag, availability, and usage
tests prove that no gate can satisfy another; unknown capability and invalid
state fail safely.

## PB3: Persistence and Audit Foundation

Add forward-only Azure SQL migrations, Dapper adapters, transaction boundaries,
optimistic concurrency, append-oriented history, required audit intent, and
real SQL tests. Keep schemas and permissions separate from Creator Commerce.

Exit gate: Creator/Billing Account isolation, lifecycle, effective periods,
manual grants, concurrency, rollback, corrections, least privilege, migration,
and audit tests pass on supported SQL Server infrastructure.

## PB4: Entitlement Enforcement Without Payments

Integrate entitlement evaluation below the UI using deterministic development
grants. Add version-aware caching, administrative revocation, background-job
rechecks, safe denial UX, and operational telemetry.

Exit gate: protected services require both authorization and entitlement;
revocation and expiry are deterministic; existing Creator data remains
recoverable; public content behavior is explicitly tested.

## PB5: Provider Selection and Billing Integration

Evaluate payment, subscription, invoice, tax, customer portal, international,
security, cost, portability, and operational requirements. Record a decision
before adding an SDK. Implement checkout and provider adapters with verified
server-side completion.

Exit gate: provider and merchant decisions are approved; browser input cannot
grant entitlement; secrets and payment data remain outside AdventuresSuite.

## PB6: Webhook Inbox and Reconciliation

Add signed, replay-protected, bounded, idempotent webhook ingestion; transactional
inbox/outbox processing; ordering-aware transitions; retries; dead letters;
scheduled reconciliation; and support-safe correction workflows.

Exit gate: duplicates, replay, invalid signature, delay, reordering, outage,
dispute, refund, provider disagreement, and rollback tests pass without double
granting or silently removing entitlement.

## PB7: Plans, Add-ons, Seats, Trials, and Grace

Publish versioned plan bundles and implement approved trials, add-ons, seat
allocations, grace periods, downgrade previews, cancellation, and recovery.

Exit gate: grandfathering, migration, over-limit, last-owner, past-due,
suspension, cancellation, export, retention, and customer-communication tests
pass.

## PB8: Usage Metering When Justified

Add an immutable idempotent usage ledger only for capabilities with an approved
commercial need. Define measurement, corrections, delayed events, limits,
customer visibility, disputes, budgets, and provider reconciliation.

Exit gate: operational telemetry cannot create charges; retries cannot double
count; customers can understand and dispute measured usage; cost and abuse
controls are operational.

## PB9: Billing Workspace and Reporting

Add accessible Creator billing administration, invoice and subscription views,
payment-provider portal handoff, entitlement explanation, plan changes, usage,
and support/reconciliation tools using authorized projections.

Exit gate: IDOR, enumeration, step-up authentication, evidence access, export,
cross-Creator reporting, accessibility, and audit matrices pass.

## Explicitly Deferred

- exact prices and bundles before business approval;
- provider-specific types in platform contracts;
- payment-card or bank-account storage;
- billing claims in identity tokens or cookies;
- automatic deletion or unpublication for failed payment;
- a seat automatically creating membership or permission;
- usage charging derived from logs, metrics, or analytics; and
- sharing Platform Billing records with Creator Commerce.
