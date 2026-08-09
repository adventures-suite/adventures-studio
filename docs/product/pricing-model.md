# AdventuresSuite Pricing and Packaging Direction

**Status:** Product Hypothesis

**Last Updated:** August 8, 2026

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

## Candidate Plans

Names and bundles remain hypotheses:

### Free or Trial

Demonstrates a complete, bounded outcome with limited Adventures, storage, AI,
publishing, or duration. It should teach value rather than manufacture
frustration.

### Explorer

For individuals, couples, and families who plan, experience, capture, and
preserve personal Adventures. Potential capabilities include Planning,
AdventuresCompanion, journals, maps, photography, and basic publishing.

### Creator

For authors, photographers, bloggers, and serious storytellers. Potential
additions include advanced publishing, EPUB/PDF/print-ready output, larger
storage, custom domains, advanced AI, subscriber relationships, and storefront
eligibility.

### Professional

For travel professionals, schools, churches, mission organizations, tour
operators, and other teams. Potential additions include multiple seats,
professional collaboration, proposals, administration, branding, templates,
and advanced reporting.

### Enterprise

For larger organizations requiring negotiated seats, SSO/federation, advanced
governance, integrations, private deployment considerations, contractual
support, and custom limits.

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

## Plan Versioning

Published plan versions are immutable. New packaging produces a new version.
Grandfathering, migration offers, effective dates, and forced retirement require
explicit business approval and customer communication.

Application code never compares plan display names. It evaluates stable
capabilities through the Platform Billing and Entitlements boundary.

## Billing-State Experience

Trial expiry, past due, cancellation, downgrade, and suspension must provide
clear status, effective date, remediation, export, and data-retention behavior.
The platform should prefer bounded read-only or premium-operation restrictions
over immediate loss of existing work.

## Decisions Required Before Launch

- exact plan names, prices, currencies, markets, and tax treatment;
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
