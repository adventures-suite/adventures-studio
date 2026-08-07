## 2026-08-01

### Shared Media Components

Decision:

Photography components will live under:

Components/Shared/Media

Reason:

Photography is a platform capability rather than a destination capability.

The same lightbox will eventually be used by:

- Homepage
- Adventure pages
- Destination pages
- Story images
- Books
- Future galleries

Status:

Approved

---

## 2026-08-06

### Creator Is the Tenancy and Ownership Boundary

Decision:

AdventuresSuite will use Creator as the stable tenancy and ownership boundary.

Every creator-owned object, content lookup, public address, resource, cache key,
search document, analytics event, and background operation must be scoped by a
stable Creator identity.

Adventures Studio is the company that owns and operates AdventuresSuite. The
Simonton Adventures is the first Creator and flagship implementation.

Publisher is a publishing role or capability of a Creator rather than a
parallel tenancy boundary. A User is an authenticated person who may receive
permissions within one or more Creators. An Organization may be represented by
a Creator and does not establish a separate content-ownership boundary.

Incoming public requests resolve an explicitly approved host to a Creator once.
The resulting Creator Context is used throughout the request. Unknown production
hosts must fail safely and must not silently select a default Creator.

Reason:

A single explicit ownership boundary prevents tenant data leakage, permits
different Creators to use the same public slug, keeps public addresses stable,
and allows the existing JSON implementation to evolve toward multi-tenant
storage without a large rewrite.

Migration:

The Creator Engine will be introduced incrementally around the existing
JSON-backed content service. Working behavior will be preserved while Creator
identity is added to address resolution, content access, branding, caching, and
future platform capabilities.

Status:

Approved

---

## 2026-08-07

### Subscriptions and Notifications Are a Platform Engine

Decision:

AdventuresSuite will provide a Subscription and Notification Engine for
permission-based Creator-audience relationships.

The initial subscription target will be Creator, followed by Adventure when the
foundation is proven. Every Subscription, Notification Event, policy, template,
background operation, and delivery record must preserve Creator identity and
tenant isolation.

Subscriber notifications are triggered by meaningful completed publications or
explicit public events. Draft saves, previews, and internal authoring changes do
not notify subscribers. Creators may publish minor changes silently.

When database-backed publishing is introduced, publication state and
notification intent will be committed together through a transactional outbox.
Delivery will be asynchronous, durable, idempotent, consent-aware, and
independent of any specific message provider.

AdventuresSuite owns platform identity, consent evidence, preference and
unsubscribe enforcement, suppression, and delivery safety. Creators own their
audience relationships within their Creator boundary and receive only the
subscriber information and aggregate insights authorized for that relationship.

Reason:

An Adventure exists before, during, and after travel. Subscribers should be able
to follow meaningful progress across that lifecycle without turning Creator
Studio draft activity into noisy or unreliable messages. Defining event,
ownership, consent, and reliability boundaries before browser-based publishing
prevents notifications from becoming a fragile mailing-list integration.

Status:

Approved

---

## 2026-08-07

### Commerce and Photography Fulfillment Use Platform Boundaries

Decision:

AdventuresSuite will support future Creator storefronts through a Commerce
Engine. The first product scope will be Publications and photography, including
EPUB, PDF, printed books, and curated physical prints.

Adventure, Publication, Edition, Resource, Product, Offer, Order, Entitlement,
Fulfillment, and License remain distinct concepts. A public or generated file is
not automatically a Product, and purchasing a physical print does not transfer
copyright or reproduction rights.

Every Catalog, Product, Offer, storefront configuration, revenue allocation, and
fulfillment configuration is Creator-scoped. Customer identity may be
platform-wide, while Creator access to customer and order information remains
isolated and permission-based.

Payment processors, print laboratories, shipping services, and protected-file
delivery systems are provider adapters. Core platform contracts will not be
named for Bay Photo, Stripe, or another vendor. Bay Photo and other professional
laboratories may be evaluated as fulfillment partners, including their actual
drop-shipping, packaging, automation, service, and commercial capabilities.

Photography must pass rights, release, print-readiness, derivative, and Creator
approval requirements before sale or licensing. Paid artifacts and production
files must not be exposed through public `wwwroot` storage.

The first physical-print program should be a curated, manually fulfilled pilot.
Automation follows only after Adventures Studio validates samples, quality,
packaging, damage handling, turnaround, customer support, margins, and demand.

Before accepting payment, Adventures Studio must explicitly decide the merchant
of record and complete appropriate legal, tax, accounting, privacy, payment, and
consumer-protection review.

Reason:

Commerce can help Creators turn the work already invested in an Adventure into
durable publications and physical art. Separating content, rights, transactions,
and fulfillment protects the Adventure model, customer trust, Creator ownership,
and the platform's ability to change partners.

Status:

Approved
