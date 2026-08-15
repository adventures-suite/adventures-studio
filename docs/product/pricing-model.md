# AdventuresSuite Pricing and Packaging Direction

**Status:** Product Direction

**Last Updated:** August 11, 2026

## Purpose

This document captures how AdventuresSuite may package customer value. It does
not approve exact prices, contract terms, or a payment provider.

## Principles

- Revenue follows demonstrated customer value.
- Plans are understandable bundles, not authorization roles.
- Stable capabilities remain independent from marketing plan names.
- Customers understand limits, renewal, downgrade, cancellation, and data
  effects before committing.
- Existing work remains recoverable when billing state changes.
- AI, storage, notification, and other variable-cost features have transparent
  allowances rather than surprise charges.
- Accessibility, security, privacy, and core data export are not premium safety
  features.
- Keep packaging simple enough to understand on one screen.
- Free provides genuine utility; paid plans add coherent value rather than
  removing manufactured frustration.
- The desired customer response is that AdventuresSuite delivers exceptional
  value for a small, predictable monthly cost.

## Candidate Plans

AdventuresSuite intends to maintain no more than three ordinary plan choices.
Exact bundles and prices remain subject to commercial approval.

### Free

Provides a genuinely useful, bounded way to plan a real Adventure and experience
the product's value. Free is an explicit versioned subscription, not the absence
of billing and not a temporary trial by implication.

### Explorer

The complete and natural choice for most individuals, couples, and families who
plan, experience, capture, and preserve personal Adventures.

### Navigator

Adds meaningful convenience, intelligence, collaboration, readiness, advanced
outputs, or capacity for customers who want more coordinated planning and
travel support. Navigator should not make Explorer deliberately incomplete.

### Promotional Offerings

Trials, previews, launch offers, and support remedies are time-bounded capability
grants layered over Free, Explorer, or Navigator. They do not create additional
hidden plans. An offer clearly identifies granted capabilities, effective and
expiration times, and the experience that remains afterward.

## Add-ons and Allowances

Plans may be supplemented by versioned add-ons such as:

- team seats;
- storage;
- AI usage;
- subscriber notification volume;
- advanced publishing and print-ready export;
- commerce and photography licensing;
- professional collaboration;
- custom domains and branding;
- advanced reporting; and
- approved API/integration access.

An add-on grants a `PlatformEntitlement` or allowance to an explicit Creator. It
does not grant a user role or membership.

The initial product should prefer the three-plan structure and promotional
grants over a large add-on catalog. Add-ons are introduced only when customer
understanding and a clear commercial need justify them.

## Plan Versioning

Published plan versions are immutable. New packaging produces a new version.
Grandfathering, migration offers, effective dates, and forced retirement require
explicit business approval and customer communication.

Application code never compares plan display names. It evaluates stable
capabilities through the Platform Billing and Entitlements boundary.

When capabilities are added to a plan, the new immutable plan version does not
silently rewrite existing subscriptions. Automatic migration, opt-in migration,
and grandfathering are explicit policies.

## Billing-State Experience

Trial expiry, past due, cancellation, downgrade, and suspension must provide
clear status, effective date, remediation, export, and data-retention behavior.
The platform should prefer bounded read-only or premium-operation restrictions
over immediate loss of existing work.

Subscription or promotion expiry does not revoke Creator membership, change an
Adventure lifecycle state, or delete customer data. Time-bound access is
evaluated from trusted UTC timestamps below the UI and for background work.

## Decisions Required Before Launch

- exact prices, currencies, markets, and tax treatment;
- monthly, annual, trial, refund, grace, and cancellation terms;
- included seats, storage, AI, notifications, and other allowances;
- add-on and overage behavior;
- grandfathering and plan-migration policy;
- nonprofit, education, professional, and enterprise terms;
- customer support and service commitments; and
- payment, invoice, tax, and customer-portal provider selection.

Architecture and implementation gates are defined in
`docs/architecture/platform-billing-entitlements.md` and
`docs/development/platform-billing-entitlements-implementation-plan.md`.
Workspace experience, capability discovery, and the customer-value promise are
defined in `docs/product/workspace-experience-and-value.md`.
