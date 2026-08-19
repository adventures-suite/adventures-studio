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

---

## 2026-08-09

### Playbooks and Calendar Events Are Authorized Planning Projections

Decision:

AdventuresSuite will produce an Adventure Travel Playbook from a specific,
authorized `AdventurePlan` version and selected protected Resources. The
Playbook is an immutable generated artifact with explicit audience/profile,
template, source versions, checksum, retention, stale state, and audit evidence;
it is not an alternate Planning source of truth or implicit publication.

Adventure Calendar Integration begins with privacy-safe ICS output and may
later add traveler-controlled device and connected-provider synchronization.
Planning remains authoritative. Each traveler explicitly consents to writes to
that traveler's calendar, events retain stable update identities and exact
destination-local time-zone semantics, and calendars exclude tickets, PINs,
private notes, precise breadcrumbs, and permanent protected-Resource URLs.

Reason:

Travelers need cohesive operational guides and useful calendar entries during
planning and travel, but documents, shared calendars, lock screens, provider
copies, and offline packages create secondary disclosure and stale-data risks.
Treating each output as a least-data, versioned projection preserves Planning
authority, Creator isolation, traveler autonomy, and deliberate publication.

Status:

Approved

---

## 2026-08-09

### Travel Readiness and Change Management Are Platform Requirements

Decision:

AdventuresSuite will provide Adventure countdowns, an explainable Travel
Readiness Dashboard, change-impact analysis, a protected Travel Document Inbox,
traveler-specific views and information policies, acknowledgment and action-
required workflows, Today and Next, contingencies, offline map/place
collections, smart reminders, planning decisions, comments and proposals,
travel-professional handoff, multi-currency budgets, deadline and cancellation-
window tracking, and safe plan templates and cloning.

Every Planned, Upcoming, or otherwise approved committed Adventure displays a
countdown in the Planning Workspace and AdventuresCompanion. The countdown is
derived from authoritative Planning schedule data. Date-only plans remain day-
level, local times require an IANA zone, offline state exposes freshness, and no
decrementing counter is persisted or allowed to drive lifecycle state.

Planning remains authoritative; Resource owns protected documents; Companion
presents minimized projections; notifications do not prove acknowledgment; and
AI extraction or impact proposals require validation and review. All operations
preserve provenance, stable identities, dependencies, traveler scope, consent,
revocation, retention, secure deep links, idempotency, reconciliation,
accessibility, localization, and audit boundaries.

Reason:

Travelers need more than a presented itinerary. They need to know when the
Adventure begins, whether it is ready, what changed, what affects them, and what
they must do next without duplicating Planning state, leaking protected travel
information, or creating unreliable automation.

Status:

Approved

---

## 2026-08-09

### Adventure Templates Instantiate Independent Customer-Owned Plans

Decision:

AdventuresSuite will support versioned Adventure Templates owned by the
platform, Creators, and travel-agency Creators. A template is a reusable
planning blueprint, not an Adventure Plan, booking, live inventory record, or
price guarantee.

Using a template creates a new private plan owned by the customer Creator with
new plan-owned identities and immutable provenance identifying the exact
template version, attribution, license decision, actor, time, and bounded
parameter summary. Published template versions are immutable. Later template
changes never silently mutate existing plans.

Template authorship, licensing, attribution, or catalog visibility grants no
access to an instantiated plan. Ongoing agency collaboration requires a
separate accepted, active, plan-scoped Planning Engagement. Agency booking and
fulfillment systems remain authoritative; AdventuresSuite does not become the
travel seller, agent, merchant, or GDS through templates.

Reason:

Templates make high-quality planning repeatable and give travel professionals
a valuable way to package expertise while preserving customer ownership,
privacy, explicit authorization, and the platform's partner-first strategy.

Status:

Approved

---

## 2026-08-09

### Planning Maps Are Authorized Progressive-Detail Projections

Decision:

The Creator Planning Workspace will provide a map that progresses from the
whole Adventure through journey or transportation segments, destination visits,
itinerary days, selected places, and possible points of interest. Accepted plan
state and candidate suggestions must remain visibly and structurally distinct.

The map is an authorized projection of Creator-owned Planning state, not an
alternate aggregate, implicit publication, booking claim, live-location view,
or turn-by-turn navigation system. Planning remains authoritative. Mapping,
places, geocoding, and routing dependencies stay behind provider-neutral
adapters and preserve source, freshness, geographic precision, attribution,
licensing, privacy, accessibility, and cost controls.

Reason:

Complex Adventures are easier to understand spatially, but map markers and
route lines can overstate certainty and expose private locations. Progressive
detail provides planning value while explicit status, authorization, and
provider boundaries protect the customer and the platform.

Status:

Approved

---

## 2026-08-09

### Uploaded Itineraries Produce Reviewed Journey Stop Proposals

Decision:

An authorized Creator may upload protected itinerary images or documents, or
paste itinerary text. AdventuresSuite will extract ordered Journey Stop
proposals containing places, local dates, arrival and departure times, proposed
IANA time zones, source evidence, confidence, and explicit versus inferred
state.

OCR, AI interpretation, geocoding, and time-zone resolution are untrusted and
cannot write Planning directly. Creator approval transactionally applies valid
proposals to private Destination Visits, Itinerary Days, transportation, and
schedule records. The public Content Engine `JourneyStop` remains a separate
model created only through explicit publication.

Reason:

Cruise itineraries are commonly delivered as screenshots, PDFs, and text.
Reviewed extraction eliminates repetitive data entry while preserving date and
time-zone correctness, source provenance, privacy, Creator authority, and the
private-to-public boundary.

Status:

Approved

---

## 2026-08-09

### Group Travel Uses Contextual Collaboration, Not General-Purpose Chat

Decision:

Creators may create group Adventures and invite authenticated travelers through
an Adventure-scoped participation relationship distinct from Creator membership
and professional engagement. Collaboration consists of contextual discussion
threads, structured advisory polls, explicit planner decisions, announcements,
and acknowledgments.

AdventuresSuite will not build general-purpose direct messaging, contacts,
social graphs, presence, voice, or video as part of this capability. Messages
and votes never mutate the Adventure Plan directly. An authorized planner must
record a decision, and the Planning Engine must validate and commit the
resulting operation.

Reason:

Groups need to discuss choices, express preferences, and understand important
changes in their itinerary context. Bounded collaboration creates that value
without turning AdventuresSuite into another messaging network or weakening
Creator authority, traveler privacy, and Planning consistency.

Status:

Approved

---

## 2026-08-09

### Companion Consumes Versioned JSON and Authorized Media

Decision:

AdventuresCompanion will retrieve traveler-specific state through versioned
JSON REST contracts and explicitly authorized media, document, map, or offline-
package delivery. It will never connect to Azure SQL or receive Dapper records,
persistence models, complete domain aggregates, provider credentials, or
permanent protected-resource URLs.

The server converts Dapper persistence results into application query
projections, applies authorization and traveler information policy, creates
purpose-built mobile DTOs, and only then serializes JSON. Offline state is a
minimized, encrypted, versioned, expiring, and revocation-aware projection
rather than a database replica.

Reason:

This boundary lets the server remain authoritative, prevents persistence and
private-field leakage, permits API and database schemas to evolve independently,
and gives Companion a secure, testable, offline-capable contract.

Status:

Approved

---

## 2026-08-09

### Push Signals Change; Companion Fetches Authoritative State

Decision:

AdventuresSuite will provide a durable Notification Engine with distinct public
audience and private Companion policy lanes. Companion supports critical
operational, action-required, informational, collaboration, audience, and
promotional categories with separate preferences, urgency, quiet hours,
digesting, safe previews, and retention.

Native push is a best-effort wake-up or navigation hint containing minimal
opaque identifiers. It never carries authoritative Adventure state or proves
identity, authorization, viewing, acknowledgment, acceptance, or completion.
Companion reauthenticates and retrieves current authorized JSON, and a server-
backed in-app notification center remains available when push is delayed,
duplicated, reordered, disabled, or lost.

Reason:

Travelers need timely and relevant information, but provider delivery is not
reliable enough to become state and lock-screen payloads create disclosure
risk. Durable intent, policy, outbox delivery, and authenticated retrieval make
notifications useful without weakening security or consistency.

Status:

Approved

---

## 2026-08-09

### Mobile APIs Use an Independent Deployable Host

Decision:

AdventuresCompanion and future external client APIs will run in a separate
ASP.NET Core application, initially `AdventuresSuite.Api`. The API will not run
inside or be proxied through the Blazor web host. It has its own process, Azure
App Service, Managed Identity, bearer-token pipeline, configuration, health,
observability identity, immutable deployment artifact, scaling policy, and
rollback.

The API and web hosts reuse approved domain, application, authorization, DTO,
and persistence libraries. Host-specific routes, browser components, cookies,
mobile bearer-token middleware, and composition remain separate. This is a
modular deployment boundary and does not establish one microservice per Engine.

Reason:

Mobile traffic, release cadence, availability, authentication, abuse controls,
and scaling differ from the Creator Workspace. Independent deployment prevents
mobile load or API changes from coupling web availability and lets each host
scale and roll back safely without duplicating authoritative business rules.

Status:

Approved

---

## 2026-08-09

### Shared Code Uses Cohesive Libraries and Preserves the API Boundary

Decision:

AdventuresSuite will use narrowly scoped contract, server application, domain,
and host-independent UI libraries rather than one universal Common project.
Companion contracts may be shared as source-level DTO definitions, but the
mobile application cannot reference server domain, application,
authorization, persistence, ASP.NET, Azure, SQL, Dapper, or identity-provider
projects.

The API generates OpenAPI from the approved Companion DTO allowlist. The
retained OpenAPI artifact remains the cross-process compatibility authority,
and CI-generated or verified mobile clients cannot bypass breaking-change and
consumer-contract tests merely because code is shared.

Reason:

Cohesive libraries reduce duplication without coupling an untrusted mobile
client to server implementation details. Preserving OpenAPI as the process
boundary keeps independent deployment, versioning, compatibility, and security
review explicit while still allowing Web and MAUI to reuse genuinely
host-independent presentation assets.

Status:

Approved
# ADR: Use a manual Azure Container Apps Job for database migrations

**Status: Superseded on 2026-08-12 by “Execute DbUp from a one-job ephemeral
private runner.”**

**Decision:** AdventuresSuite database migrations run as a finite, manually
started Azure Container Apps Job using an immutable image digest and dedicated
user-assigned migration identity. The temporary App Service/Kudu/VM bridge is
superseded but remains until the replacement proves both its SQL-free execution
channel and one reviewed migration.

**Rationale:** A Job provides authoritative execution identity/status, bounded
timeout, explicit retry/parallelism controls, private VNet egress, and retained
logs without interactive administration or coupling migration to application
startup. See `docs/architecture/database-migration-job.md`.

**Consequences:** Deployment and SQL bootstrap remain separate approvals; ACR,
Container Apps environment/subnet, two user-assigned identities, and logging add
development cost; only digest-addressed images may execute.

---

## 2026-08-12

### Execute DbUp from a One-Job Ephemeral Private Runner

Decision:

Retain the mature AdventuresSuite DbUp migration model and package it as a
deterministic, self-contained, attested protected-main artifact. A future
separately approved implementation will execute that artifact once on an
ephemeral GitHub self-hosted Azure VM in the existing VNet, using the existing
migration UAMI and Azure SQL private endpoint. Independent cleanup must delete
the runner after every outcome.

The Container Apps Job/ACR design is superseded and its active workflows,
images, and IaC are removed. DACPAC conversion, public SQL, temporary firewall
rules, SQL passwords, client secrets, persistent runner compute, ACR, automatic
retry, web/API startup migration, and destructive rollback are prohibited.

Reason:

The repository already has ordered immutable DbUp scripts, journaling,
application locking, per-script transactions, pre/post classification,
fingerprints, and verification. Reusing that model avoids an unjustified
conversion and removes a registry/container control plane that failed before
SQL execution. See `docs/architecture/private-sql-migration-execution.md` and
the retained postmortem.

Status:

Approved; runner implementation and all Azure/SQL operations remain separately
blocked.

## 2026-08-18

### AdventuresCompanion Is Localization-Ready from Its First Release

Decision:

AdventuresCompanion launches in United States English (`en-US`) and establishes
Spanish (`es`), French (`fr`), and Italian (`it`) as the next supported language
resources. Presentation uses a shared, resource-backed locale catalog with
deterministic regional and English fallback. Interface language remains
separate from Creator locale, destination time zone, currency, units, API wire
formats, authored content, and Planning state. AI translation is an untrusted
proposal, and sensitive product language requires human review.

Reason:

Establishing resource keys, fallback behavior, culture-aware formatting, and
test boundaries before the production mobile shell prevents hardcoded English
from becoming a costly cross-platform dependency while avoiding premature or
silent translation of private traveler content.

Status:

Approved

---
