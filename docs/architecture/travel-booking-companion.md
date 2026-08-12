# Travel Booking Companion

**Status:** Approved Architecture Direction; Direct Selling Deferred

**Last Updated:** August 12, 2026

## Purpose

AdventuresSuite may integrate with cruise lines, airlines, hotels, rental-car
companies, tour operators, travel professionals, and other suppliers. Its first
role is to make planning and the experience around a booking exceptionally
helpful. Adventures Studio does not initially become the travel supplier,
ticket issuer, merchant of record, or around-the-clock servicing operation.

This preserves trust, simplicity, and affordability while leaving a deliberate
path to deeper commercial integrations later.

## Four Operating Models

### 1. Planning and Outbound Booking

AdventuresSuite helps the traveler select an option and sends the traveler to
the supplier or an approved partner to complete the purchase. The supplier owns
payment, ticketing, cancellation, refund, fulfillment, and traveler servicing.
This is the recommended initial model.

### 2. Embedded Partner Checkout

The supplier's hosted checkout opens through a redirect or another tightly
controlled supplier-owned experience. AdventuresSuite may receive a minimal
completion reference afterward, with traveler consent, and propose importing
the confirmation into the private Adventure Plan. The supplier or approved
partner remains the seller and merchant of record.

### 3. Agency-Assisted Booking

AdventuresSuite collaborates with a host travel agency or appropriately
authorized travel professional. The platform supplies approved planning context
and may receive reservation status through provider-neutral contracts. The
agency or professional issues and services the booking.

Customer-plan access still requires a separate accepted, active, plan-scoped
Planning Engagement. Agency membership or a supplier relationship never grants
customer access.

### 4. AdventuresSuite as Booking Seller

Adventures Studio shops, reserves, charges, tickets, exchanges, cancels,
refunds, reconciles, handles fraud, and supports disruptions as an online
travel agency or travel seller.

This is a separate operating business, not a routine product feature. It is
explicitly deferred unless direct selling later becomes central enough to
justify its accreditation, licensing, financial, legal, security, support, and
reconciliation obligations.

## Recommended Direction

Build toward models 1 through 3. Do not assume model 4.

The preferred experience is a **booking companion**:

1. The traveler constructs the Adventure in AdventuresSuite.
2. The platform identifies suitable bookable options from approved sources.
3. The traveler purchases directly from the supplier or approved agency.
4. The external seller returns a minimal, opaque completion reference.
5. AdventuresSuite securely retrieves or accepts the confirmation with the
   traveler's consent.
6. The traveler reviews exactly what will be added to the plan.
7. The Planning Engine commits approved facts with required audit intent.
8. AdventuresSuite helps with deadlines, readiness, changes, documents, maps,
   calendars, travel-day guidance, and memories.

This model delivers substantial ongoing value without requiring an affordable
subscription to subsidize a 24-hour travel-service organization.

## Authority Boundary

```text
Private Planning need
        ↓
Offer search from approved source
        ↓
Untrusted, expiring offer proposal
        ↓
Supplier-owned checkout and fulfillment
        ↓
Consent-based confirmation import
        ↓
Reviewable reservation proposal
        ↓
Creator-approved private Planning mutation
```

An offer is not inventory, a hold, a price guarantee, or a booking. A handoff
reference is not payment or confirmation. An imported reservation remains
externally authoritative and retains verification and freshness state.

No search result, callback, webhook, message, professional action, or provider
response directly mutates Planning. Authorization, entitlement, validation,
optimistic concurrency, idempotency, and atomic audit remain below the UI.

## Provider-Neutral Capability Seams

Future implementations should use narrow capabilities equivalent to:

```text
ITravelOfferSearchProvider
IBookingHandoffProvider
IBookingConfirmationImportProvider
IReservationStatusProvider
IReservationServicingProvider       // deferred
ITravelPaymentProvider              // explicitly deferred
```

Provider SDK, transport, authentication, rate limits, paging, raw schemas, and
errors remain in infrastructure adapters. Every operation begins with explicit
Creator identity and, when applicable, Adventure Plan, traveler consent, and
Planning Engagement scope. Provider identifiers never become Planning
identities or select their own authorization scope.

## Offer and Confirmation Semantics

Every normalized offer preserves provider and supplier, opaque reference,
contract version, retrieval and expiration time, currency and price meaning,
inclusions, exclusions, restrictions, change and refund summary, traveler or
occupancy assumptions, availability source, attribution, licensing, and an
allowlisted fingerprint.

Expired offers cannot be presented as current or handed off without
revalidation. Comparisons must not hide taxes, fees, fare conditions, cabin or
room assumptions, baggage, or other material differences.

Confirmation import retains external authority, opaque reservation reference,
supplier and booking channel, confirmation and synchronization times,
externally reported status, source-linked proposals, consent, authorized
traveler visibility, and protected Resource references when required.

Booking PINs, ticket codes, payment-card data, passport details, and supplier
credentials do not belong in ordinary plan fields, logs, analytics, messages,
calendar events, URLs, or push payloads.

## Commercial and Regulatory Activation Gate

Before enabling live search, handoff, confirmation, servicing, commission, or
payment, Adventures Studio must document and approve:

- its exact role: software provider, affiliate, referral partner, travel
  professional partner, agent, seller, or merchant of record;
- supplier, agency, aggregator, GDS, or other commercial agreements;
- permitted search, caching, display, persistence, attribution, and use;
- applicable seller-of-travel registration and consumer disclosures;
- airline ticketing, appointment, accreditation, and settlement requirements;
- supplier authorization for cruise, lodging, vehicle, activity, or tour;
- commission, tax, refund, chargeback, fraud, and reconciliation ownership;
- payment security scope and supplier-hosted checkout design;
- appropriate errors-and-omissions, cyber, and other insurance review;
- support ownership for disruptions and traveler emergencies;
- privacy, consent, retention, deletion, and cross-border processing; and
- safe termination behavior that preserves authorized customer plan data.

Open technical standards do not grant commercial access, ticketing authority,
settlement rights, or permission to use supplier content. Legal and
professional review is required before activation.

## Initial Prohibitions

AdventuresSuite does not initially:

- collect or store payment-card numbers;
- issue airline tickets or supplier documents;
- become merchant of record;
- promise price, inventory, availability, safety, or fulfillment;
- silently convert an offer into a reservation;
- cancel, exchange, or modify travel without explicit confirmation;
- sell travel insurance without required licensing and disclosures;
- store supplier credentials, booking PINs, or ticket codes in ordinary plan
  fields; or
- imply that Adventures Studio is the carrier, supplier, travel agency, or
  booking authority.

## Incremental Delivery

1. Direct supplier links and manual, protected confirmation import.
2. Trackable affiliate or partner handoffs with clear disclosures.
3. Supplier-hosted checkout and authenticated, replay-protected callbacks.
4. Integrations through a trusted host agency or travel professional.
5. Reassess whether direct booking authority creates enough customer value to
   justify becoming a travel seller.

Each phase may stop permanently without preventing AdventuresSuite from
delivering its core Planning, Travel, and Remember value.

## Required Tests Before Live Integration

- Creator, plan, engagement, consent, and entitlement isolation;
- forged, expired, replayed, duplicate, out-of-order, and cross-environment
  callbacks;
- offer expiration, price changes, missing fees, and changed restrictions;
- provider timeout, throttling, partial response, and credential revocation;
- idempotent handoff and confirmation processing;
- no Planning write before review and atomic Planning plus audit on approval;
- change proposals that cannot silently overwrite the plan;
- protected confirmation handling without log or analytics disclosure;
- provider termination and retained-plan behavior; and
- proof that externally hosted payment sends no cardholder data through
  AdventuresSuite.

## Research References

- [IATA NDC](https://developer.iata.org/en/ndc/) is an open airline retailing
  communication standard; it does not itself grant supplier access or ticketing
  authority.
- [ARC travel-agency participation](https://www2.arccorp.com/products-participation/travel-agencies/)
  distinguishes accredited U.S. ticketing agencies from non-ticketing Verified
  Travel Consultants.
- [Traveltek Cruise API](https://www.traveltek.com/travel-api-provider/cruise-api/)
  and [Widgety API](https://widgety.org/product/api/) illustrate commercial
  cruise integration through travel-industry agreements.
- [California Seller of Travel guidance](https://oag.ca.gov/travel/reg-faqs)
  illustrates jurisdiction-specific duties that arranging or advertising air
  or sea travel can create.
- [PCI outsourcing guidance](https://www.pcisecuritystandards.org/faqs/does-pci-dss-apply-to-merchants-who-outsource-all-payment-processing-operations-and-never-store-process-or-transmit-cardholder-data/)
  explains that outsourcing payment can reduce technical scope without
  eliminating responsibility for the provider relationship.
