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
