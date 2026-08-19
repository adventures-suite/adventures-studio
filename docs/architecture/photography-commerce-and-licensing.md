# Photography Commerce and Licensing

**Version:** 1.0

**Status:** Approved Direction

**Last Updated:** August 7, 2026

---

# Purpose

AdventuresSuite should allow eligible Creator-owned photography to become
physical art and, where appropriate, a licensed digital asset.

This capability composes the Resource, Commerce, Rendering, Notification, and
future Rights capabilities. It is not a separate photo store disconnected from
the Adventure that gives the photograph meaning.

---

# Two Distinct Products

Buying a physical print and licensing an image are different transactions.

```text
Photograph
    ├── Buy a Print
    │     └── Own a manufactured physical object
    │
    └── License This Image
          └── Receive defined usage rights and a digital derivative
```

A print purchase does not transfer copyright or reproduction rights unless an
explicit agreement states otherwise. A licensing Offer must not be presented as
an ordinary print purchase.

---

# Physical Print Products

Supported products may eventually include:

- Photographic prints
- Fine-art paper prints
- Framed prints
- Canvas
- Metal prints
- Acrylic prints
- Curated collections
- Open editions
- Signed or numbered limited editions

Product variants may include size, aspect ratio, crop, paper or substrate,
finish, frame, mat, and edition configuration.

The initial catalog should be intentionally curated. AdventuresSuite should not
expose every laboratory option before product quality, support, and margins have
been validated.

---

# Photography Licensing

Potential license types include:

- Personal digital use
- Editorial use
- Commercial website use
- Advertising use
- Book or magazine use
- Nonprofit or educational use
- Exclusive or nonexclusive use

A license Offer may define:

- Permitted use
- Media and channels
- Territory
- Duration
- Audience, print-run, or impression limits
- Exclusivity
- Attribution requirements
- Modification rights
- Resale and redistribution restrictions
- Price and currency
- Delivered resolution and format
- Governing license document

Standardized licenses and legal review are required before self-service digital
licensing is launched. Custom or exclusive licensing may initially require
manual review.

---

# Rights and Eligibility

A photograph must not be offered for print or licensing merely because it is
publicly visible.

Eligibility should establish:

- Creator ownership or sufficient copyright authority
- Identified photographer and copyright holder
- Required model and property releases
- Restrictions involving artwork, trademarks, events, or locations
- Permitted commercial uses
- Attribution requirements
- License and territorial restrictions
- Whether the image is AI-generated or materially AI-edited
- Whether the image is approved for print, licensing, or both

The Resource Engine should own durable rights metadata and related release
records. Commerce consumes that approved state; it does not invent or infer
rights.

---

# Print Readiness and Quality

Print eligibility should also validate:

- Source resolution
- Supported output dimensions
- Aspect ratio and crop-safe areas
- Color profile
- Sharpness and enlargement limits
- Product-specific bleed and safe zones
- Creator-approved proof or sample
- Watermark-free production derivative

The platform should create purpose-specific derivatives while preserving the
original Resource. Public previews, licensed downloads, and laboratory files
should not be the same asset.

---

# Creator-Branded Experience

The public storefront should be branded primarily as the Creator. Adventures
Studio operates AdventuresSuite and partner relationships, but the customer is
buying the Creator's work.

Initial fulfillment packaging may be neutral. Premium options may later include:

- Creator-branded thank-you inserts
- Photograph title and location
- Adventure and story context
- A permanent QR address back to the story
- Care instructions
- Signed or numbered certificates of authenticity

“White label” must be evaluated precisely for each provider. Generic packaging
without laboratory branding is not the same as custom Creator or Adventures
Studio packaging.

---

# Fulfillment Partner Architecture

Bay Photo and other professional laboratories are potential fulfillment
partners. No laboratory is selected as a permanent platform dependency by this
document.

The platform integrates through a provider-neutral `FulfillmentProvider`
boundary responsible for capabilities such as:

- Product and option discovery
- Price and production estimate retrieval
- Print-file requirements
- Order submission
- Shipping selection
- Production status
- Tracking
- Cancellation where supported
- Damage, reprint, and exception handling

Provider capabilities vary. AdventuresSuite must not advertise custom branding,
international delivery, returns, production times, or automated ordering until
the selected provider contract confirms them.

Bay Photo currently presents professional print products, direct-to-customer
drop shipping, integrated ecommerce partners, and unbranded or white-label
delivery options. A public general-purpose API and custom AdventuresSuite or
Creator-branded packaging have not been established. Those capabilities require
direct partner validation before implementation.

Public references reviewed August 7, 2026:

- [Bay Photo ordering solutions and integrated partners](https://bayphoto.com/order/)
- [Bay Photo professional products and drop shipping](https://bayphoto.com/proprints/)
- [Bay Photo white-label shipping policy](https://art.bayphoto.com/shipping-policies)

---

# Recommended Pilot

The first release should validate the business manually:

1. Select a small collection of flagship photographs.
2. Confirm rights and releases.
3. Define a small set of products and sizes.
4. Produce and approve physical samples.
5. Accept a limited number of orders through the Commerce Engine when ready.
6. Submit fulfillment manually to the selected laboratory.
7. Measure quality, packaging, damage, turnaround, support effort, margin, and
   customer demand.
8. Negotiate or build an automated adapter only after the workflow is proven.

Manual fulfillment is an intentional discovery phase, not the long-term
architecture.

---

# Operational Requirements

The operating model must define:

- Retail price and Creator revenue allocation
- Laboratory and shipping cost changes
- Sales tax and resale documentation
- Returns, damage, reprints, and refunds
- Lost shipments
- Customer service ownership
- International shipping and customs
- Limited-edition inventory and numbering
- Product discontinuation and substitution
- Provider outages and migration

Photography licensing additionally requires license records, agreement versions,
delivered derivatives, revocation or correction procedures, and an auditable
history of the rights granted.

---

# Current Implementation Status

Photography remains public content and shared static media in the current
application. No image is currently approved for automated sale or licensing.
Paid files, production derivatives, rights records, and licensed downloads must
not use public `wwwroot` storage.
