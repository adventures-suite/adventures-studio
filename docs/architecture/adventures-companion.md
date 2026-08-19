# AdventuresCompanion Mobile Architecture

**Version:** 1.0

**Status:** Approved Direction

**Last Updated:** August 10, 2026

## Purpose

AdventuresCompanion is the first AdventuresSuite native mobile application. It
supports travelers during an active Adventure on iOS and Android, including
when connectivity is intermittent or unavailable.

The Companion is not a miniature copy of the Creator Workspace. Its first job
is to place approved Planning information, timely assistance, memory capture,
and traveler-controlled location breadcrumbs in the traveler's hand.

## Product and Technology Direction

AdventuresCompanion will use .NET MAUI Blazor Hybrid. Shared, host-independent
Razor components and .NET contracts may be reused across web and mobile, while
navigation, offline behavior, camera, location, notifications, secure storage,
and other device experiences use platform-specific implementations behind
provider-neutral interfaces.

Initial targets are iOS and Android. The architecture does not require every
web component to be reused, and mobile projects do not reference the Blazor Web
App host.

## Presentation State and Appearance

Mobile presentation state remains local to the Companion shell and shared
host-independent components. It does not enter Companion API DTOs, Planning
state, synchronization cursors, or server authorization decisions.

### Language and Localization

The initial release locale is United States English (`en-US`). The supported
language direction then adds neutral Spanish (`es`), French (`fr`), and Italian
(`it`) resources. Regional preferences such as `es-MX`, `fr-CA`, and `it-IT`
use the reviewed neutral-language resource until a distinct regional variant is
approved. Unsupported or malformed preferences fall back to `en-US`.

Interface language remains distinct from Creator locale, authored-content
language, destination language and time zone, currency, and measurement unit.
The Companion resolves presentation language in this order: explicit traveler
preference, device preferences in their declared order, Creator default, then
`en-US`. Choosing an interface language never translates or mutates private
Planning content, traveler text, protected Resources, or published content.

Shared and native presentation obtains product-owned text from versioned
resources rather than hardcoded component strings. Dates, times, numbers,
plural forms, and accessible labels use the resolved presentation culture;
API dates, local times, UTC timestamps, IANA zones, money, units, identifiers,
enums, cache keys, and persistence remain in their explicit culture-neutral
formats. Missing resource keys fail tests, and a missing translation uses the
reviewed English fallback rather than displaying a key or guessing a
translation.

Machine or AI translation is an untrusted proposal. Legal, privacy, safety,
permission, emergency, booking, accessibility, and store-disclosure language
requires human review before release. Localization acceptance tests cover text
expansion, truncation, dynamic type, screen-reader output, pluralization,
offline availability, and locale changes without cross-Creator cache leakage.

An injected appearance service owns two distinct values:

- the traveler's preference: `System`, `Light`, or `Dark`; and
- the currently effective palette: `Light` or `Dark`.

`System` is the default. The service observes live iOS and Android appearance
changes and updates the effective palette while System remains selected.
Explicit Light and Dark preferences persist in ordinary platform preferences,
not credential storage, and ignore subsequent system appearance changes until
System is selected again. Platform adapters apply the effective palette to
native window, status/navigation bar, splash and other chrome surfaces; the
shared shell applies it to Blazor content before the first interactive frame.
Startup must use a platform-appropriate pre-render or bootstrap value so a
saved or effective dark palette is not preceded by a visible light frame.

A single versioned semantic-token vocabulary supplies surface, text, border,
accent, focus, status, scrim, elevation, map, and interactive-state roles for
both palettes. Components consume semantic roles rather than literal colors.
Pages, navigation, dialogs, cards, controls, loading/error/empty states, native
chrome, maps, and overlays switch as one coherent presentation. Automated token
checks and rendered accessibility tests enforce applicable WCAG contrast for
normal and large text, meaningful graphics, controls, focus indicators, and
selected/disabled states. Images and maps use a tokenized scrim, outline, or
other tested treatment where their content cannot guarantee contrast.

Transient popovers, dropdowns, and overlays use one explicit open/closed state
owned by their component. The Memories Adventure selector closes after a
selection, selector reactivation, outside pointer activation, Escape, or the
applicable platform Back action. Dismissal is idempotent and atomically removes
the visual layer, hit-test surface, event subscriptions, and Back registration;
sets the accessibility expanded state to false; and restores focus to the
selector when appropriate. Back and Escape are handled only while open, and
outside activation dismisses without activating an obscured control in the
same gesture. Component disposal and page navigation perform the same cleanup
so no invisible overlay can block later interaction.

## Initial Capabilities

- today and upcoming itinerary views;
- local-time and time-zone-aware schedule presentation;
- approved transportation, accommodation, reservation, task, reminder, map,
  and essential-reference summaries;
- offline access to a minimized selection of plan information;
- minimized offline Adventure Travel Playbook access;
- explicit Add to Device Calendar for selected authorized itinerary items;
- journaling and photography prompts;
- camera and photo-library capture with later Resource Engine upload;
- push notifications for approved meaningful plan changes; and
- optional, traveler-controlled GPS breadcrumb capture.

Full Creator authoring, professional administration, commerce administration,
and unrestricted plan editing are not initial Companion capabilities.

## Platform Boundaries

```text
AdventuresCompanion
    ↓ versioned mobile API
Identity + Creator membership + resource authorization
    ↓
Planning Engine / Resource Engine / Notification Engine
    ↓
Azure SQL structured state + Azure Blob Storage media
```

Azure SQL remains authoritative. The device keeps only an authorized,
minimized, encrypted offline projection. Mobile operations carry explicit
Creator and resource scope; the server reauthorizes every read, mutation,
synchronization, upload, and notification-registration operation.

Companion receives only versioned JSON API contracts and authorized media,
document, map, or offline-package delivery. Dapper records, SQL/persistence
models, complete domain aggregates, provider credentials, and permanent
protected URLs never cross the mobile boundary. Server application services
query through Dapper adapters, apply authorization and traveler information
policy, create purpose-built mobile DTOs, and only then serialize JSON. See
`docs/architecture/companion-api-sync.md`.

The public Content Engine and private Planning Engine remain distinct. Mobile
capture never becomes public merely because it was synchronized.

Companion owns private device-capture sessions, breadcrumb trails, installation
registrations, and offline synchronization state. A trail references an
Adventure Plan but is not embedded as a Planning aggregate child and does not
expand the plan transaction. The Planning Engine remains authoritative for the
itinerary and approved plan state; the Resource Engine owns synchronized media;
the Publication boundary owns any deliberate public transformation.

A Planning `Traveler` record does not establish platform identity or mobile
access. Access requires an authenticated `UserId` plus a future explicit,
revocable authorization basis for that Adventure and operation. Display names,
email addresses, device identifiers, and possession of a plan link never bind a
traveler to an account or grant access.

For Group Travel, that authorization basis may be an accepted, active,
Adventure-scoped `TravelerParticipation`. Companion may then expose minimized
contextual discussions, polls, announcements, and acknowledgments permitted by
the traveler's information policy. Participation does not grant Creator
membership, and a vote or message cannot mutate Planning. See
`docs/architecture/group-travel-collaboration.md`.

## Mobile Authentication

The mobile application is a public client and cannot safely hold a client
secret or confidential-client certificate. It uses browser-delegated OpenID
Connect/OAuth authorization-code flow with PKCE through the operating system's
approved user agent.

The current workspace cookie is not reused by the mobile application. A future
versioned AdventuresSuite API accepts appropriately scoped mobile access tokens
and maps validated external identity to the same stable platform `UserId`.
Tokens establish API identity and audience only; Creator membership, roles,
permissions, plan ownership, and engagement authority remain server-owned and
are evaluated for every operation.

Credentials and refresh material, when required by the approved flow, use
operating-system secure storage. Tokens never enter application logs,
analytics, URLs, location records, or general local preferences.

## Offline Data and Synchronization

Offline data is an explicitly selected projection, not a replica of the
Planning database. Each record carries Creator scope, resource identity,
version, synchronization state, and minimum necessary fields.

Synchronization uses:

- versioned API contracts;
- JSON snapshots and incremental changes using projection versions, conditional
  requests, or opaque cursors;
- idempotency identities for retryable mutations;
- optimistic concurrency and explicit conflict results;
- bounded background work and retry;
- per-Creator partitioning and cache keys;
- revocation-aware access and local-data clearing; and
- deterministic handling of partial upload and interrupted synchronization.

The device database and staged private media are encrypted using approved
platform facilities. Logout, account disablement, lost-device response, plan
access revocation, and retention expiry define local-data removal behavior.

## GPS Breadcrumb Capability

A breadcrumb trail is a private sequence of location observations captured by
the traveler's device during an Adventure. It may later help the traveler
remember a route, organize photographs, reconstruct a day, or deliberately
create a map for a publication.

Location capture is always:

- off by default;
- enabled by an authenticated traveler on that traveler's device;
- protected by operating-system location permission;
- limited to an explicit Adventure and capture session;
- visibly active, pausable, and stoppable;
- subject to an approved retention and synchronization policy; and
- private until separately reviewed and published.

Creator, administrator, professional, or support access cannot remotely enable
another person's device tracking. Acceptance of a plan, membership, engagement,
terms of service, or general notification consent is not location consent.

### Consent State

The platform records the minimum evidence necessary to prove the user's choice:

- consenting `UserId` and device installation identity;
- Adventure Plan and Creator scope;
- consent policy version;
- requested precision and foreground/background mode;
- granted, denied, limited, or revoked status;
- UTC decision time; and
- stop/revocation time and safe reason category when applicable.

Operating-system permission remains authoritative for device access. Platform
consent cannot override an OS denial or limitation. The application explains
why location is requested immediately before the OS prompt and continues to
work with reduced capability when permission is declined.

### Capture Model

A breadcrumb observation contains only the fields required for the approved
experience, such as:

- opaque trail, segment, and observation identities;
- Creator, Adventure Plan, user, and device-installation scope;
- UTC observation time;
- latitude and longitude at the approved precision;
- horizontal accuracy and provider-safe capture quality;
- optional altitude, speed, or course only when separately justified; and
- synchronization and retention state.

Location labels, inferred places, geofences, activity recognition, and raw
device-provider payloads are not collected by default. Points are sampled at a
policy-controlled interval or meaningful-distance threshold to balance route
quality, battery, storage, privacy, and cost. The product must not imply
continuous or emergency-grade tracking.

Background capture requires a separately justified user experience, the
appropriate OS permission, persistent user-visible indication where the
platform provides it, and App Store/Play policy review. A foreground-only mode
must remain useful.

### Privacy and Control

The traveler can inspect capture state, pause, resume, stop, and request deletion
of eligible private breadcrumbs. The platform clearly distinguishes:

- device permission;
- AdventuresSuite capture consent;
- synchronization to the private Adventure;
- sharing with selected collaborators; and
- publication to an audience.

Each step is separate and revocable where legally and technically applicable.
Publication uses a deliberate transformation that can reduce precision, remove
sensitive portions, omit home or lodging areas, simplify a route, and exclude
time information. Raw breadcrumb records are never published wholesale.

Precise location never appears in logs, traces, metric dimensions, product
analytics, notification payloads, support identifiers, or ordinary audit
metadata. Location-data access, export, sharing, publication, retention change,
and deletion are protected operations with safe audit evidence that excludes
coordinates.

## Media Capture

New photographs and media are staged in protected device storage. The Resource
Engine issues narrowly scoped, short-lived upload authorization so the device
can upload directly to private Azure Blob Storage. After successful upload, SQL
stores Creator-owned Resource identity, metadata, rights, accessibility, and
storage-provider reference. Permanent storage credentials and signed delivery
URLs are not embedded in the application or content records.

Location association with media is optional and separately reviewable. Exported
or published media must not retain precise EXIF location unless the Creator
explicitly approves it under the publication policy.

## Notifications

Push-provider details remain behind a Notification Engine adapter. Device
registration is user- and installation-scoped, revocable, environment-specific,
and never treated as identity or authorization. Push payloads contain no private
itinerary, reservation, location, or authentication data; the app fetches
authorized detail after activation.

Companion maintains a server-backed in-app notification center because push
delivery is best-effort. Operational travel, action-required, collaboration,
and optional audience/promotional notifications remain distinct categories with
separate policy and preferences. Notification delivery, opening, viewing,
acknowledgment, acceptance, and task completion are different facts.

## Travel Playbook and Device Calendar

Companion presents an authorized, minimized Travel Playbook projection rather
than downloading the Creator Master by default. Offline sections and protected
documents are explicitly selected, encrypted, versioned, expiration-aware, and
cleared under revocation policy. The device shows when its package is older than
the authoritative plan.

Add to Device Calendar is an explicit traveler action behind a platform adapter.
The app requests OS calendar permission just in time, lets the traveler choose
items, target calendar, and reminders, and remains useful when permission is
denied. Planning state remains authoritative; device-calendar edits do not
silently mutate the plan.

Calendar entries use destination-local time and stable event identities. They
exclude ticket QR/barcode values, booking or room PINs, private notes, precise
breadcrumbs, another traveler's data, and permanent protected-Resource URLs.
See `docs/architecture/adventure-travel-playbook.md` and
`docs/architecture/adventure-calendar-integration.md`.

## Countdown, Readiness, and Active Travel

Companion displays a countdown for every Adventure whose authoritative
Planning state is Planned, Upcoming, or another explicitly approved committed
state. It derives the value from the synchronized start date and optional local
time/IANA zone; it never stores decrementing ticks or invents a midnight instant
for date-only plans. The presentation becomes `Starts today` or `In progress`
at the applicable boundary and remains accessible, localized, and reduced-
motion safe.

Companion also provides traveler-specific readiness, Today and Next, material-
change acknowledgment, required actions, approved contingencies, smart
reminders, and selected offline maps/places. These are minimized, encrypted,
revocation-aware projections. Unknown, unverified, and stale information is
visibly distinct from ready or confirmed information.

See `docs/architecture/adventure-readiness-and-change-management.md`.

## Security and Abuse Prevention

The mobile threat model must cover lost or shared devices, rooted/jailbroken
devices, token theft, malicious deep links, replay, offline tampering,
cross-Creator synchronization, insecure backups, screenshot exposure, location
stalking, coerced consent, background tracking, notification leakage, media
upload abuse, and API enumeration.

Device integrity signals may inform risk decisions but never replace server
authorization. Biometric gates may protect selected local experiences but do
not establish platform identity. Safety messaging states that Companion is not
an emergency, rescue, medical, or guaranteed tracking service.

## Observability, Audit, and Reporting

Mobile operational telemetry follows the platform observability taxonomy and
contains no precise location or private plan content. Location metrics are
aggregate and low-cardinality, such as permission outcome category, capture
health, upload latency, synchronization age, and battery-impact bands.

Durable audit records prove protected consent, sharing, export, publication,
and deletion actions without coordinates. Creator reports use authorized,
privacy-preserving projections. Product analytics requires approved purpose,
consent, minimum fields, retention, and deletion semantics.

## Delivery Dependencies

Implementation follows, rather than interrupts, the current security and
Planning sequence. Prerequisites include:

- completed human authentication and Creator membership;
- Planning application-service authorization;
- a useful web Planning Workspace;
- a versioned, OAuth-protected mobile API;
- Resource Engine private upload and delivery;
- private generated-artifact delivery and Travel Playbook profile policy;
- approved offline storage and synchronization design;
- mobile privacy and threat-model review; and
- Apple and Google developer, signing, privacy, and distribution readiness.

Detailed increments are defined in
`docs/development/adventures-companion-implementation-plan.md`.

The appearance service, semantic-token contract, transient-overlay behavior,
and their platform adapters are presentation dependencies of the MAUI shell.
They require no Companion API contract or server persistence change.

## References

- [.NET MAUI supported platforms](https://learn.microsoft.com/dotnet/maui/supported-platforms)
- [ASP.NET Core Blazor Hybrid](https://learn.microsoft.com/aspnet/core/blazor/hybrid/)
- [Blazor Hybrid and Razor class-library architecture](https://learn.microsoft.com/aspnet/core/blazor/hybrid/class-libraries-best-practices)
