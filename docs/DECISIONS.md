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

### Planning Persistence Uses Azure SQL

Decision:

The Planning Engine will use Azure SQL Database in hosted environments and
Dapper with `Microsoft.Data.SqlClient` inside its infrastructure adapter.
Azure App Service will authenticate through Managed Identity; database secrets
must not be committed or stored in global application settings.

Local development and required database integration tests will use a disposable
SQL Server container. CI will use the same SQL Server engine family so migrations,
constraints, concurrency behavior, and provider semantics are tested rather than
approximated through an in-memory or SQLite substitute.

Planning domain and application contracts remain independent of Dapper,
`Microsoft.Data.SqlClient`, connection strings, and Azure types. Every repository
operation and transaction begins with explicit `CreatorId`. The infrastructure
implementation must enforce Creator-scoped keys, indexes, and predicates even
when an identifier is unique in the current data set.

Reason:

Azure SQL aligns with the existing App Service and Managed Identity architecture
and the expected persistence direction of other transactional AdventuresSuite
engines. Dapper keeps SQL, Creator predicates, transaction boundaries, and
optimistic-concurrency conditions explicit. A matching local and CI SQL Server
topology gives higher confidence in migrations and tenant isolation than a
behaviorally different test provider, while provider-neutral contracts preserve
the option to replace infrastructure without rewriting the Planning domain.

Status:

Approved

---

## 2026-08-07

### Planning Is Private and AI Uses a Proposal Boundary

Decision:

AdventuresSuite will introduce a Planning Engine as the authoritative owner of
private, structured Adventure Plans. An AdventurePlan is operational planning
state and is distinct from a public Adventure, Volume, Journey, Destination, or
other Content Engine record.

Every planning record, repository operation, AI request, proposal, cache key,
index, and background operation must preserve stable Creator identity. Planning
data is private by default and may contain traveler information, reservation
references, budgets, private notes, and unpublished dates.

Public content is created only through an explicit, Creator-approved
publication transformation that selects safe fields. The platform must not
publish a private aggregate directly or infer publication from planning status.

The AI Planning Copilot is not a system of record. It returns validated,
structured proposals against a known plan version. A Creator reviews and
accepts or rejects proposed operations before the Planning Engine applies them
transactionally. Provider-specific SDKs, prompts, model names, and response
types remain behind platform adapters.

Interactive planning data will move to durable database storage when the
persistence phase begins because it requires authorization, transactions,
concurrency control, audit history, and private records. Existing public
editorial content may remain JSON during the transition.

Reason:

Separating private operational planning from public storytelling prevents
accidental disclosure and allows each model to evolve for its actual purpose.
Requiring human approval for AI proposals preserves Creator judgment, makes
changes auditable, and prevents a model or provider format from becoming the
platform's domain or mutation boundary.

Detailed direction is defined in:

- `docs/architecture/planning-engine.md`
- `docs/architecture/ai-planning-copilot.md`
- `docs/product/creator-planning-workspace.md`
- `docs/development/planning-engine-implementation-plan.md`

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

---

## 2026-08-07

### Travel Professionals Are Partners Through Scoped Engagements

Decision:

AdventuresSuite will enable travel professionals and agencies to collaborate on
customer-owned Adventure Plans as partners. The platform will augment their
service rather than compete with or replace them.

An agency may be represented by a Creator for its own brand, staff, templates,
and Resources. The customer remains a separate Creator and sole owner of the
Adventure Plan, memories, Resources, and Publications. This introduces neither
shared ownership nor a parallel tenancy boundary.

Access requires an explicit, accepted, active `PlanningEngagement` scoped to one
customer Creator and one Adventure Plan. Agency membership alone grants no
customer access. Engagements are least-privilege, time-bounded, revocable, and
audited. Professional changes default to customer-approved proposals;
direct-edit permission is a stronger explicit grant.

Agency CRM, GDS, supplier, booking, commission, and fulfillment systems remain
authoritative for their concerns and integrate through provider-neutral
adapters. Commercial terms are deferred. Current Planning persistence work will
not add speculative partner fields or tables.

Reason:

This model gives professionals a richer customer experience while preserving
Creator ownership, privacy, auditability, and the existing tenant boundary.

Status:

Approved

---

## 2026-08-07

### Human Authentication and Creator Resource Authorization Are Separate

Decision:

AdventuresSuite will treat human user identity, Creator identity, Creator
membership, Azure workload identity, and future Planning Engagement identity as
separate concepts.

Authentication establishes a stable platform user. Authorization evaluates an
explicit permission for one operation against an authoritatively owned,
Creator-scoped resource. Authentication, a resolved public host, agency
membership, or possession of an Adventure Plan identifier does not independently
grant access.

Core authorization contracts and permission vocabulary remain independent of
an identity provider and ASP.NET Core. Server-side application and persistence
boundaries enforce resource-aware policies; UI checks are advisory only. Agency
membership never grants customer-plan access without a future accepted, active,
matching Planning Engagement. Proposal permission remains weaker than direct
edit permission.

Reason:

Separating these identities and decisions prevents cross-Creator disclosure,
IDOR, host-context confusion, stale membership grants, and accidental agency
access while allowing authentication providers and framework adapters to evolve.

Status:

Approved

---

## 2026-08-07

### Observability Is Provider-Neutral and Privacy-Preserving

Decision:

AdventuresSuite will treat observability as a shared platform capability.
Application code uses standard .NET structured logging, tracing, and metrics
abstractions with OpenTelemetry as the instrumentation and export boundary.
Azure Monitor and Application Insights are the initial Azure destination, but
core contracts remain independent of that provider.

Operational logs, distributed traces, metrics, security telemetry, durable audit
records, business events, and product analytics are distinct signal types.
Operational telemetry does not replace audit or business-event persistence.

Telemetry carries consistent service, release, environment, correlation,
operation, outcome, and carefully controlled opaque Creator context. Private
Creator content, sensitive traveler information, credentials, raw requests, SQL
parameters, and raw AI exchanges are prohibited. Metrics use low-cardinality
dimensions and never use Creator, user, or resource identifiers as dimensions.

Operational export is best-effort and bounded. Required audit behavior remains
transactional and follows the Identity and Authorization architecture. Azure
environments use separate destinations, access, retention, sampling, alerts,
dashboards, and budgets.

Reason:

A consistent observability boundary makes the platform diagnosable and
operable while preventing vendor coupling, cross-Creator leakage, accidental
sensitive-data storage, alert noise, and uncontrolled telemetry cost.

Status:

Approved

---

## 2026-08-07

### Audit and Reporting Are Required Platform Capabilities

Decision:

Every AdventuresSuite Engine will participate in a governed platform audit and
reporting model. Append-oriented security and compliance audit, versioned domain
events, product analytics, rebuildable reporting projections, and operational
telemetry remain distinct data products with separate reliability, access,
privacy, retention, and failure semantics.

Required audit intent commits atomically with protected state or through a
transactional outbox. Durable events are minimal, Creator-scoped, versioned, and
consumed idempotently. Creator reports read authorized projections rather than
unrestricted operational tables; platform-wide reports require separate
authority. Azure SQL is the initial storage option, while analytical platforms
are deferred until measured scale or query needs justify their governance and
cost.

The capability is delivered incrementally. Current authentication work adds
only the provider-neutral audit vocabulary required by its approved slice and
does not introduce speculative persistence, event infrastructure, or reporting
UI.

Reason:

Building evidence and reportability into Engine boundaries preserves security,
Creator isolation, consent, financial reconciliation, AI lineage, and future
analytics without retrofitting unreliable logs or broad database access later.

Status:

Approved

---

## 2026-08-07

### Human Authentication Uses Microsoft Entra External ID

Decision:

AdventuresSuite will use Microsoft Entra External ID in an external tenant as
the initial customer and travel-professional human identity provider. The web
application will use browser-delegated OpenID Connect authorization code flow
with PKCE. A validated issuer and subject map through an infrastructure adapter
to stable platform `UserId`; provider email, display name, claims, and object
identifiers do not become Creator ownership or authorization keys.

The application owns its secure session, revocation, Creator membership, and
resource authorization. External ID establishes human identity only. Azure
Managed Identity remains workload identity and cannot satisfy human decisions.
Production and non-production identity configuration are separated, automated
tests use deterministic fake identities, and no package or login UI is added
until the Slice 5 integration design is approved.

Reason:

External ID provides Azure-aligned CIAM for consumers and business customers,
standards-based OIDC, provider-managed credentials and recovery, social and
enterprise federation options, and MFA capabilities without making the
platform's durable identity and authorization model provider-specific.

Status:

Approved

---

## 2026-08-08

### AdventuresCompanion Is the First Mobile Application

Decision:

AdventuresSuite will introduce iOS and Android through AdventuresCompanion,
built with .NET MAUI Blazor Hybrid. The first mobile experience supports active
travel rather than duplicating the full Creator Workspace. It consumes a
versioned OAuth-protected API, reuses host-independent .NET and Razor assets
where appropriate, accesses native device capabilities through adapters, and
maintains only a minimized encrypted offline projection.

Companion may capture GPS breadcrumbs only when the authenticated traveler
explicitly enables capture on that device and grants operating-system
permission. Capture is off by default, Adventure-scoped, visible, pausable,
stoppable, retention-bound, and private. Device permission, AdventuresSuite
consent, synchronization, collaborator sharing, and publication are separate
decisions. No other actor can remotely enable tracking, and raw trails cannot be
published without a privacy-reducing review and transformation.

Reason:

MAUI Blazor Hybrid preserves AdventuresSuite's .NET investment while allowing
native iOS and Android capabilities. A Companion-first product delivers the
highest travel value, and explicit traveler control prevents location and
offline features from becoming surveillance or accidental publication paths.

Status:

Approved

---

## 2026-08-08

### Platform Billing and Entitlements Are Separate from Authorization and Commerce

Decision:

AdventuresSuite will use a provider-neutral Platform Billing and Entitlements
capability for SaaS plans, add-ons, seats, allowances, and paid feature access.
Identity, Creator membership, resource authorization, Platform Entitlement,
feature rollout, and service availability remain independent gates. Plans are
immutable versioned bundles of stable capabilities and never become roles or
application conditionals based on marketing names.

`PlatformEntitlement` means a Creator's right to use an AdventuresSuite
capability. `CommerceEntitlement` means a shopper's right to access a
Creator-sold digital product. Platform Billing and Creator Commerce do not share
orders, subscriptions, entitlements, payment state, or reporting. A Billing
Account may fund multiple Creators without gaining access to any of them, and a
seat never creates membership or permission.

Payment providers remain adapters. Webhooks are untrusted signed,
replay-protected, idempotent input processed through a transactional inbox and
reconciliation. AdventuresSuite does not store payment-card data or derive
billable usage from operational telemetry. Billing failure does not
automatically delete or unpublish Creator work.

Reason:

Separating commercial rights from security authorization allows Adventures
Studio to evolve pricing, grandfather plans, offer add-ons, serve agencies, and
change providers without weakening Creator isolation or scattering plan-name
checks through the platform.

Status:

Approved

---

## 2026-08-08

### Development Authentication Infrastructure Uses Private Azure Boundaries

Decision:

Slice 5F development uses a separate External ID external tenant, dedicated
Azure SQL database, private VNet integration and private endpoints, private Key
Vault and Data Protection Blob storage, and separate system-assigned Managed
Identities for application DML and migration DDL. SQL, Key Vault, Blob, and the
migration application do not expose public data-plane ingress.

Azure live state is not sufficient documentation. Reviewed infrastructure as
code must reproduce or reconcile supported Azure resources, and versioned
runbooks govern External ID, certificate registration and rotation, SQL
contained-user bootstrap, migration execution, Data Protection, smoke tests,
recovery, and teardown.

The migration app shares development compute, remains stopped by default, runs
one exact immutable migrator artifact through an approved private execution
path, and returns to stopped state after success or failure. The web application
cannot execute migrations, grant itself database access, or obtain DDL
authority. A public firewall exception is not the default workaround for a
hosted runner that cannot reach private endpoints.

Reason:

This design preserves least privilege, environment isolation, reproducibility,
and operational evidence while preventing the application, migration workflow,
or CI runner from becoming an unreviewed administrative path into private
identity and Planning data.

Status:

Approved
