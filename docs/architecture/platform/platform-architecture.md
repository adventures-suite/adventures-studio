# AdventuresSuite Platform Architecture

**Version:** 1.2

**Status:** Draft

**Last Updated:** August 2026

---

## 1. Purpose

AdventuresSuite is the technology platform created and operated by Adventures Studio.

The platform enables creators to create, manage, publish, brand, address, render, discover, and share meaningful adventures and related content.

The Simonton Adventures is the first creator implementation on AdventuresSuite.

The platform architecture must support many creators over time without requiring a separate application, codebase, or deployment for each creator.

---

## 2. Relationship Between Company, Platform, and Creator

The platform distinguishes between three primary concepts.

```text
Adventures Studio
    owns and operates

AdventuresSuite
    provides reusable platform capabilities

Creators
    own their brands, content, resources, and audiences
```

### Adventures Studio

Adventures Studio is the company responsible for:

- Platform strategy
- Product development
- Platform operations
- Infrastructure
- Security
- Creator services
- Business partnerships
- Platform growth

### AdventuresSuite

AdventuresSuite is the reusable software platform.

It provides capabilities for:

- Creator management
- Branding
- Content management
- Resource management
- Private Adventure planning
- Public addressing
- QR generation
- Rendering
- Publishing
- Discovery
- Planning
- Media
- Analytics
- Audience subscriptions and notifications
- Creator storefronts and commerce
- Artificial intelligence
- Future platform services

### Creator

A Creator is a tenant of AdventuresSuite.

A Creator may be:

- An individual traveler
- A family
- An author
- A photographer
- A travel blogger
- A tour operator
- A nonprofit organization
- A church
- A university
- A historical organization
- A media company
- Another organization publishing meaningful journeys

The Simonton Adventures is the first Creator.

---

## 3. Core Platform Principle

> Everything presented through AdventuresSuite belongs to a Creator and is delivered through reusable platform capabilities.

A Creator owns:

- Identity
- Brand
- Domains
- Content
- Resources
- Private Adventure Plans
- Media
- Products
- Publishing configuration
- Audience relationships
- Creator-specific settings

The platform provides the engines and infrastructure that manage those assets.

---

## 4. Multi-Tenant Architecture

AdventuresSuite is designed as a multi-tenant platform.

Each Creator is a tenant.

Creator-owned data must be logically isolated from data belonging to other Creators.

Every creator-owned record should eventually include a stable Creator identity.

Conceptually:

```text
Platform
    ├── Creator A
    │   ├── Brand
    │   ├── Content
    │   ├── Resources
    │   └── Addresses
    │
    ├── Creator B
    │   ├── Brand
    │   ├── Content
    │   ├── Resources
    │   └── Addresses
    │
    └── Creator C
        ├── Brand
        ├── Content
        ├── Resources
        └── Addresses
```

A slug such as:

```text
acropolis
```

may exist for more than one Creator because the complete address is scoped by Creator identity or Creator domain.

The combination must be unique:

```text
Creator + Address Slug
```

---

## 5. Creator Identity

Every Creator should eventually have a stable internal identity independent of:

- Display name
- Brand name
- Domain name
- URL
- Subscription plan
- Storage location

A Creator may change its public name or domain without changing its internal identity.

Example:

```text
CreatorId:
    stable platform identity

DisplayName:
    The Simonton Adventures

PrimaryDomain:
    thesimontonadventures.com
```

Creator identity provides the tenancy boundary for content, resources, addresses, branding, permissions, analytics, and future services.

---

## 6. Creator Branding

Each Creator controls its own public brand.

Brand configuration may include:

- Display name
- Primary logo
- Alternate logo
- Light-background logo
- Dark-background logo
- Favicon
- Primary color
- Secondary color
- Accent colors
- Background colors
- Typography
- Layout preferences
- Button treatment
- Image treatment
- Writing voice
- Copyright text
- Social links
- Brand guidelines

The Rendering Engine uses the Creator's brand configuration when presenting content.

The same platform software may therefore produce visually distinct experiences for different Creators.

```text
Shared AdventuresSuite application
        ↓
Creator-specific brand
        ↓
Creator-specific experience
```

Brand assets such as logos, favicons, and style guides are reusable Creator resources.

---

## 7. Content

Content is the authored material that communicates an adventure, story, idea, place, or experience.

Examples include:

- Adventure
- Volume
- Journey
- Journey segment
- Destination
- Experience
- Story
- Story section
- Reflection
- Journal entry
- Quote
- Guide
- Timeline
- Map narrative
- Planning content
- Photography collection
- Video
- Audio
- Podcast
- Transcript
- Caption
- Interactive experience
- Companion content
- Future content types not yet defined

The platform must not depend on a fixed, exhaustive list of content types.

New content types should be introduced through extensible models and capabilities rather than by bypassing the platform architecture.

---

## 8. Resources

A Resource is a reusable asset, reference, file, service, or supporting item associated with a Creator or with Creator content.

Examples include:

- Logo
- Photograph
- Video file
- Audio file
- Thumbnail
- PDF
- EPUB
- Document
- Map file
- GPX file
- Ticket
- Reservation
- External website
- Booking link
- Restaurant link
- Hotel link
- Social profile
- Downloadable file
- Brand guideline
- Template
- Future resource types not yet defined

Resources may be:

- Stored directly by AdventuresSuite
- Stored in Azure Blob Storage
- Delivered through a content delivery network
- Hosted by a streaming provider
- Referenced through an external URL
- Generated dynamically
- Managed by another approved system

The platform owns the Resource record and its metadata even when the underlying asset is stored externally.

Resource metadata may include:

- Stable resource identity
- Creator identity
- Resource type
- Title
- Description
- Storage provider
- Storage location
- Public URL
- Media type
- File size
- Dimensions
- Duration
- Thumbnail
- Alternative text
- Attribution
- Copyright
- Usage rights
- Publication state
- Processing state
- Related content

---

## 9. Content and Resource Relationship

Content and Resources are related but distinct.

```text
Content
    communicates meaning

Resources
    support, enrich, store, or represent content
```

Examples:

```text
Destination Content
    uses
Hero Photograph Resource
```

```text
Experience Content
    uses
Video Resource
```

```text
Creator Brand
    uses
Logo Resource
```

```text
Journey Segment Content
    references
Train Ticket Resource
```

```text
Planning Content
    references
Reservation Resource
```

A Resource may be reused by multiple content items.

A content item may reference multiple Resources.

---

## 10. Core Platform Engines

An Engine is the implementation of a reusable platform capability.

Each major responsibility must have one clear owning Engine.

Features compose Engine capabilities rather than duplicating their responsibilities.

### 10.1 Creator Engine

The Creator Engine owns:

- Creator identity
- Tenant configuration
- Brand configuration
- Domains
- Themes
- Creator settings
- Creator status
- Subscription information
- Permissions
- Tenant boundaries

### 10.2 Content Engine

The Content Engine owns the content lifecycle.

Responsibilities include:

- Creating content
- Validating content
- Storing content
- Retrieving content
- Updating content
- Relating content
- Versioning content
- Publishing content
- Archiving content
- Managing content identity

The Content Engine does not render content.

### 10.3 Resource Engine

The Resource Engine owns reusable resource records and resource metadata.

Responsibilities may include:

- Creating resource records
- Locating resources
- Validating resources
- Storing resource metadata
- Relating resources to content
- Managing media references
- Managing rights and attribution
- Supporting multiple storage providers
- Tracking processing state
- Providing resource URLs

The Resource Engine does not determine how a resource is presented.

### 10.4 Address Engine

The Address Engine owns stable public addresses.

Responsibilities include:

- Creator-scoped slugs
- Canonical addresses
- Aliases
- Permanent redirects
- Deep links
- Public route resolution
- Reserved addresses
- Address validation

The Address Engine resolves:

```text
Creator + Slug
    ↓
Addressable target
```

### 10.5 QR Engine

The QR Engine generates QR assets for stable public addresses.

Responsibilities include:

- SVG generation
- PNG generation
- Print-safe output
- Address validation
- Bulk generation
- Future QR manifests
- Future scan analytics

The QR Engine does not determine canonical routes.

It asks the Address Engine for a stable public address.

### 10.6 Rendering Engine

The Rendering Engine presents Creator content and resources.

Potential rendering channels include:

- Website
- Mobile application
- EPUB
- PDF
- Printed book
- API
- Email
- AI context
- Companion experience
- Future channels

The Rendering Engine combines:

```text
Creator Brand
    +
Content
    +
Resources
    +
Rendering Context
```

to produce an appropriate presentation.

### 10.7 Identity and Authorization

Identity and Authorization establish human platform identity and evaluate
explicit permissions for operations on Creator-owned resources.

Responsibilities include stable user identity, Creator membership, permission
and policy evaluation, session and revocation semantics, resource ownership
checks, and authenticated audit actors. Human identity, Creator identity, and
Azure workload identity remain separate.

Authentication does not grant Creator access by itself. Public host resolution
does not substitute for private authorization, and agency membership does not
grant access to a customer Adventure Plan.

Detailed direction is defined in
`docs/architecture/identity-authorization.md` and
`docs/architecture/security.md`.

### 10.8 Planning Engine

The Planning Engine owns private, structured Adventure Plans used during Dream,
Plan, and Travel.

Responsibilities include:

- Adventure Plans and planning status
- Travelers and planning preferences
- Destination visits and local date/time context
- Daily itineraries and activities
- Transportation and accommodations
- Reservation planning state
- Tasks, notes, packing, and budgets
- Planning validation, audit history, and concurrency
- Explicit selection of planning facts for later publication

Planning records are Creator-owned and private by default. They are distinct
from public Content Engine records. Planning status never grants public
visibility, and the Planning Engine does not publish content directly.

The AI Engine may produce structured proposals for a plan, but only the
Planning Engine can apply Creator-approved operations to authoritative state.

Detailed direction is defined in `docs/architecture/planning-engine.md`.

### 10.9 Partner Collaboration Engine

The Partner Collaboration Engine enables a customer to invite a travel
professional or agency to help with one customer-owned Adventure Plan.

Responsibilities include plan-scoped invitations and engagements, delegated
permissions, proposals and approvals, expiration, revocation, audit history,
attribution, and provider-neutral agency integrations.

The customer Creator remains the sole owner. Agency membership alone never
grants customer access. An engagement is an authorization relationship, not
shared ownership or a new tenancy model.

Detailed direction is defined in
`docs/architecture/partner-collaboration-engine.md`.

### 10.10 Discovery Engine

The Discovery Engine helps users find and explore content.

Potential capabilities include:

- Search
- Navigation
- Recommendations
- Related content
- Geographic discovery
- Topic discovery
- Creator discovery
- Adventure discovery
- Planning suggestions
- Future personalized discovery

### 10.11 Commerce Engine

The Commerce Engine owns Creator storefront catalogs and commercial
transactions for products derived from AdventuresSuite Publications and
Resources.

Responsibilities include:

- Creator-scoped catalogs and storefront configuration
- Products, Offers, prices, availability, and terms
- Orders, refunds, and commercial audit history
- Digital Entitlements and protected delivery coordination
- Physical fulfillment coordination
- Payment, tax, and fulfillment provider adapters
- Creator-scoped revenue and commerce reporting

The Commerce Engine does not own Adventure source content, Publication
artifacts, Resource rights, or physical manufacturing. It references those
capabilities and coordinates approved providers.

Detailed direction is defined in `docs/architecture/commerce-engine.md` and
`docs/architecture/photography-commerce-and-licensing.md`.

### 10.12 Subscription and Notification Engine

The Subscription and Notification Engine owns Creator-audience subscription
relationships and subscriber-facing delivery orchestration.

Responsibilities include:

- Verified subscriptions and consent evidence
- Creator and Adventure subscription targets
- Subscriber preferences and unsubscribe enforcement
- Notification policies
- Durable publication events
- Audience selection
- Delivery orchestration, retries, suppression, and deduplication
- Creator-scoped delivery history and aggregate insights

The Engine responds to meaningful completed publications and explicit public
events. It does not notify subscribers for draft saves, previews, or internal
authoring changes.

Every audience relationship, event, template, background operation, and delivery
must preserve Creator identity and tenant isolation. Subscriber identity may be
shared by the platform, but one Creator must never receive another Creator's
audience data.

Detailed direction is defined in
`docs/architecture/subscription-notification-engine.md`.

### 10.13 AI Engine

The AI Engine provides reusable artificial-intelligence capabilities across AdventuresSuite.

It supports Creators and travelers without replacing the Creator's ownership, judgment, or voice.

Responsibilities may include:

- Content assistance
- Writing assistance
- Content summarization
- Content classification
- Metadata generation
- Caption generation
- Transcript processing
- Search enrichment
- Semantic discovery
- Recommendations
- Planning assistance
- Companion conversations
- Creator voice preservation
- Content quality checks
- Image and media analysis
- Future generative and agent-based capabilities

The AI Engine consumes Creator Context, Content Engine data, Resource Engine data, and platform permissions.

It must not bypass:

- Creator ownership
- Tenant isolation
- Publication rules
- Content permissions
- Resource rights
- Platform safety controls

The AI Engine should preserve the Creator's voice and assist rather than replace the Creator.

For Planning Engine mutations, the AI Engine returns structured proposals. It
does not write authoritative plans directly. Proposal review, stale-version
detection, Creator approval, and transactional application are platform
boundaries defined in `docs/architecture/ai-planning-copilot.md`.

```text
Creator Context
    +
Content Engine
    +
Resource Engine
    +
Platform permissions
        ↓
AI Engine
        ↓
Assistance, discovery, planning, enrichment, or companion experience
```

---

## 11. Engine Ownership Rule

Before implementing a feature, AdventuresSuite must identify which Engine owns each responsibility.

Examples:

```text
Creator logo management
    → Creator Engine and Resource Engine
```

```text
Video metadata
    → Content Engine and Resource Engine
```

```text
Public slug resolution
    → Address Engine
```

```text
QR image generation
    → QR Engine
```

```text
Page presentation
    → Rendering Engine
```

```text
Search ranking
    → Discovery Engine
```

No responsibility should be implemented independently in multiple features when it belongs to a reusable Engine.

---

## 12. Domain and Host Resolution

Creators may use their own public domains.

Examples:

```text
thesimontonadventures.com
anothercreator.com
travelstories.example.org
```

DNS directs each approved Creator domain to the AdventuresSuite hosting infrastructure.

DNS does not determine the final content route.

It directs the request to the platform.

AdventuresSuite then resolves the incoming host to a Creator.

```text
Incoming request
    ↓
Host:
thesimontonadventures.com
    ↓
Creator Engine
    resolves domain
    ↓
Creator:
The Simonton Adventures
```

The remaining path is then resolved within that Creator's address space.

```text
Host:
thesimontonadventures.com

Path:
/go/acropolis
    ↓
Creator Resolver
    identifies The Simonton Adventures
    ↓
Address Engine
    resolves "acropolis" for that Creator
    ↓
Target content or experience
```

This permits multiple Creators to use the same slug without conflict.

---

## 13. Public URL Strategy

The preferred public QR address is Creator-branded.

Example:

```text
https://thesimontonadventures.com/go/acropolis
```

Another Creator may use:

```text
https://anothercreator.com/go/acropolis
```

Both requests may be served by the same AdventuresSuite deployment.

The host determines the Creator.

The slug determines the addressable target within that Creator.

Development environments may use platform-hosted domains until Creator domains are configured.

Example:

```text
https://adventures-suite-dev.example.net/go/acropolis
```

Production printed materials should use a permanent Creator-controlled domain whenever possible.

---

## 14. Request Resolution Pipeline

A typical public request follows this sequence:

```text
HTTP Request
    ↓
Host Resolution
    ↓
Creator Engine
    ↓
Creator Context
    ↓
Address Engine or Application Route
    ↓
Content Engine
    ↓
Resource Engine
    ↓
Rendering Engine
    ↓
Creator-branded response
```

For a QR redirect:

```text
QR Scan
    ↓
Creator Domain
    ↓
Creator Engine
    ↓
Address Engine
    ↓
Canonical target
    ↓
Rendering Engine
```

---

## 15. Creator Context

After the incoming host is resolved, AdventuresSuite should establish a Creator Context for the request.

Creator Context may include:

- Creator identity
- Creator slug
- Creator status
- Primary domain
- Current domain
- Brand configuration
- Theme
- Locale
- Time zone
- Feature availability
- Subscription capabilities
- Publishing settings

Downstream Engines should consume Creator Context rather than repeatedly resolving the Creator.

---

## 16. Tenant Isolation

Creator data must remain isolated.

Platform services should never return another Creator's private or unpublished data because of a missing filter or ambiguous lookup.

Important rules include:

- Every creator-owned record includes Creator identity.
- Creator-scoped queries require Creator identity.
- Public addresses resolve within Creator context.
- Resource access respects Creator ownership.
- Administrative permissions are Creator-scoped.
- Caches include Creator identity in their keys.
- Search indexes preserve Creator boundaries.
- Analytics preserve tenant boundaries.
- Background processing preserves Creator context.
- Subscriptions, notification events, and delivery history preserve Creator
  identity and consent boundaries.
- Catalogs, Products, Offers, Orders, Entitlements, and fulfillment operations
  preserve Creator identity and customer authorization boundaries.

Tenant isolation must be enforced by platform design rather than by developer convention alone.

---

## 17. Storage Independence

Platform consumers must not depend directly on a specific storage implementation.

Initial storage may include:

- JSON files
- Local static assets
- Application configuration

Future storage may include:

- Relational databases
- Document databases
- Azure Blob Storage
- Content delivery networks
- Search indexes
- External media providers
- Content-management systems
- Hybrid storage

Engines expose abstractions that allow storage systems to evolve without rewriting unrelated capabilities.

---

## 18. Rendering Independence

Content and Resources must not be coupled to one presentation channel.

The same content may eventually be rendered as:

- A website page
- A mobile view
- A printed book section
- An EPUB chapter
- A PDF
- An AI Companion response
- A social-sharing card
- An email
- An API response
- A future immersive experience

Presentation-specific logic belongs in the Rendering Engine or channel-specific renderers.

---

## 19. Extensibility

AdventuresSuite must support future capabilities that have not yet been defined.

The architecture should favor:

- Stable identities
- Explicit ownership
- Composable capabilities
- Extensible content types
- Extensible resource types
- Storage abstraction
- Rendering abstraction
- Creator-scoped addressing
- Backward-compatible public links
- Incremental implementation

The platform should not attempt to predict every future feature.

It should provide durable boundaries that allow future features to be added without bypassing or destabilizing existing capabilities.

---

## 20. Initial Implementation

The platform vision is broader than the initial implementation.

The current implementation remains intentionally simple:

- One Blazor Web App
- One initial Creator
- The Simonton Adventures as the flagship implementation
- JSON-backed content
- Static image resources
- Reusable Razor components
- Existing destination and experience models
- Existing short QR routes
- Azure App Service hosting
- GitHub Actions deployment
- GitHub Environments
- OIDC authentication through Azure Managed Identity

The current implementation should evolve incrementally toward the platform architecture.

A large rewrite is not required.

---

## 21. Migration Strategy

Platform capabilities will be introduced one responsibility at a time.

The migration approach is:

1. Preserve working behavior.
2. Introduce an abstraction around existing behavior.
3. Move one consumer to the abstraction.
4. Build and test.
5. Deploy and verify.
6. Continue incrementally.

Existing models and services should not be replaced solely for architectural purity.

They should evolve when a platform capability provides clear value.

---

## 22. The Simonton Adventures

The Simonton Adventures is:

- The first Creator
- The flagship customer
- The first production implementation
- The first proving ground for platform capabilities
- A consumer-facing brand
- A Creator with its own content, resources, domain, and visual identity

Features are first proven through The Simonton Adventures.

Once validated and generalized, they become reusable AdventuresSuite capabilities available to other Creators.

---

## 23. Architecture Principles

AdventuresSuite follows these principles:

- The platform is multi-tenant.
- Every Creator owns its brand, content, resources, and audience.
- Every creator-owned object is scoped to a Creator.
- Private planning state is distinct from public editorial content.
- Every major responsibility has one owning Engine.
- Features compose capabilities.
- Engines expose abstractions.
- Content is independent of presentation.
- Resources are reusable.
- Public addresses are permanent.
- Creator domains determine Creator context.
- Storage implementations may evolve.
- Rendering channels may evolve.
- New capabilities are introduced incrementally.
- Existing working behavior is preserved during migration.
- Additional infrastructure is added only when justified.
- AI assists Creators but does not bypass content ownership, review, or publishing controls.

---

## 24. Architectural Decision Summary

The current platform direction is:

```text
Adventures Studio
    ↓
AdventuresSuite
    ↓
Creator
    ├── Brand
    ├── Content
    ├── Resources
    ├── Addresses
    ├── Products
    └── Audience
```

Core platform capabilities are organized through:

```text
Creator Engine
Content Engine
Resource Engine
Planning Engine
Address Engine
QR Engine
Rendering Engine
Discovery Engine
Commerce Engine
Subscription and Notification Engine
AI Engine
```

The Simonton Adventures remains the first Creator and flagship implementation.

This document is the source of truth for the long-term AdventuresSuite platform architecture.

The existing website architecture document remains the source of truth for the current implementation of The Simonton Adventures website.

Significant platform-level architectural changes should be documented here before or during implementation.
