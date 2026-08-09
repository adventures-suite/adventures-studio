# Commerce Engine

**Version:** 1.0

**Status:** Approved Direction

**Last Updated:** August 7, 2026

---

# Purpose

The Commerce Engine enables Creators to market and sell products derived from
their published Adventures while preserving clear boundaries between content,
publishing, resources, transactions, and fulfillment.

The initial objective is a focused Creator storefront for digital publications
and printed books. AdventuresSuite should not begin as a general-purpose
marketplace.

---

# Core Principle

> An Adventure is the source. A Publication is an output. A Product is an offer
> to a customer.

Commerce concerns must not become part of the core Adventure content model.

```text
Adventure
    ↓
Publication and Edition
    ↓
Resource artifact
    ↓
Product and Offer
    ↓
Order
    ↓
CommerceEntitlement or Fulfillment
```

An EPUB file may be a Publication artifact and Resource without being for sale.
It becomes commercially available only when a Creator places it in a Product
and publishes an Offer.

---

# Initial Products

The initial storefront should concentrate on:

- EPUB editions
- Downloadable PDF editions
- Printed books
- Curated photography prints

Future products may include:

- Destination and planning guides
- Photography collections
- Premium interactive Adventures
- Maps and itineraries
- Courses, workshops, and services
- Memberships
- Licensed photography
- Merchandise

New product categories should be introduced only after their rights,
fulfillment, support, tax, and refund requirements are understood.

---

# Core Concepts

## Publication

A Publication is an approved rendered expression of Adventure content, such as
a website edition, EPUB, PDF, or print-ready book.

## Edition

An Edition identifies a particular version or format of a Publication. Editions
allow revisions, formats, languages, and special releases to evolve without
changing the owning Adventure.

## Product

A Product is a Creator-owned commercial item presented in a catalog. It refers
to one or more Publications, Resources, services, or physical configurations.

## Offer

An Offer defines how a Product may be purchased, including price, currency,
availability, market, terms, and effective dates.

One Product may have multiple Offers, such as an EPUB, a digital bundle, a
printed edition, and a signed edition.

## Order

An Order records the customer, selected Offers, commercial amounts, payment
state, and fulfillment state. Payment-provider records support an Order but do
not replace the AdventuresSuite Order identity.

## Commerce Entitlement

A `CommerceEntitlement` records a customer's continuing right to access a
purchased digital product or protected experience. It is distinct from a
`PlatformEntitlement`, which governs a Creator's right to use an AdventuresSuite
SaaS capability. Commerce and Platform Billing do not share entitlement,
subscription, order, or payment state.

## Fulfillment

Fulfillment delivers the purchased item. Digital fulfillment grants a
`CommerceEntitlement` and protected access. Physical fulfillment creates and
tracks work with an approved production and shipping provider.

---

# Creator and Customer Boundaries

Every Catalog, Product, Offer, storefront configuration, revenue allocation,
and fulfillment configuration must be scoped by `CreatorId`.

Customer identity and checkout may eventually be platform-wide so a customer
can purchase from more than one Creator. A Creator may see only its own catalog,
orders, permitted customer information, fulfillment, and reporting.

The storefront should be Creator-branded. AdventuresSuite powers the capability
and Adventures Studio operates the platform, but neither should erase the
Creator's public identity.

---

# Merchant Model Decision

Implementation requires an explicit business decision about who is the merchant
of record.

If Adventures Studio is the merchant, it assumes substantial responsibility for
customer payments, tax, refunds, disputes, fraud, support, Creator payouts, and
financial reporting.

If each Creator is the merchant, onboarding and marketplace payment integration
become more complex, while more of the direct commercial relationship remains
with the Creator.

Architecture must support an explicit model and must not infer it from a payment
provider integration. Legal, tax, accounting, payment, and consumer-protection
review is required before commerce is enabled in production.

---

# Security and Reliability

The Commerce Engine must:

- Avoid storing card data
- Treat payment-provider webhooks as untrusted, authenticated input
- Process payment and fulfillment events idempotently
- Preserve an auditable order and refund history
- Protect paid resources from public static delivery
- Use short-lived authorized access for digital downloads
- Separate payment success from fulfillment success
- Preserve Creator identity in queues and background work
- Define refund, cancellation, dispute, and failed-fulfillment behavior

Paid EPUB, PDF, and image files must not be stored as public assets in
`wwwroot`.

---

# Engine Relationships

```text
Content Engine
    owns Adventure source content

Publishing capability
    creates Publications, Editions, and artifacts

Resource Engine
    stores protected artifacts and rights metadata

Commerce Engine
    owns catalogs, Products, Offers, Orders, and CommerceEntitlements

Fulfillment adapters
    manufacture, deliver, and track physical products

Subscription and Notification Engine
    sends receipts, delivery updates, and permitted marketing messages

Identity and Permission capabilities
    identify customers and authorized Creator users
```

Transactional customer messages such as receipts are not marketing
subscriptions. The Notification Engine must maintain that distinction.

---

# Provider Independence

Payment, printing, shipping, tax, and file-delivery providers are adapters. Core
Commerce contracts must not be named for or depend directly upon one vendor.

Examples include:

- `PaymentProvider`
- `FulfillmentProvider`
- `TaxProvider`
- `ProtectedDeliveryProvider`

Stripe Connect is a potential future payment foundation for multi-Creator
commerce, but it is not selected by this document.

---

# Expected Azure Direction

When justified, the likely direction is:

- Azure SQL for catalogs, Products, Offers, Orders, CommerceEntitlements, and
  fulfillment state
- Azure Blob Storage for protected digital artifacts and licensed derivatives
- Durable messaging for payment, fulfillment, and delivery processing
- Managed Identity between internal Azure services
- Short-lived authorized URLs or application-mediated downloads

These are expected implementation directions rather than irreversible provider
commitments.

---

# Delivery Phases

## Phase 1: Commerce Architecture

- Finalize Publication, Edition, Product, Offer, Order, and
  CommerceEntitlement contracts
- Decide the merchant model
- Define tax, refund, support, privacy, and accounting responsibilities
- Establish Resource rights and protected-delivery foundations

## Phase 2: Catalog Without Checkout

- Creator-owned storefront configuration
- Product catalog and product detail pages
- Digital and printed edition metadata
- Availability and pricing preview

## Phase 3: One Digital Product

- Sell one EPUB or PDF product type
- Process payment safely
- Grant a CommerceEntitlement
- Deliver a protected artifact
- Send transactional receipts

## Phase 4: Printed Books

- Integrate one provider-neutral print fulfillment adapter
- Track production, shipment, exceptions, refunds, and support

## Phase 5: Photography and Marketplace Expansion

- Curated photography prints
- Photography licensing
- Multi-Creator payouts where the selected merchant model requires them
- Bundles and additional product categories

---

# Current Implementation Status

This document establishes approved future direction. Commerce, checkout,
payments, CommerceEntitlements, and fulfillment are not part of the current
JSON-backed public application. AdventuresSuite SaaS plans and paid platform
capabilities follow `docs/architecture/platform-billing-entitlements.md`.
