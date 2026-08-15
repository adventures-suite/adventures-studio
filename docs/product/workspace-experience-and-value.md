# AdventuresSuite Workspace Experience and Customer Value

**Status:** Product Direction

**Last Updated:** August 14, 2026

Experience and component decisions follow
`docs/architecture/experience-design-system.md`.

## Product Promise

AdventuresSuite should be exceptionally helpful at an affordable, understandable
price. Customers should feel that the platform delivers substantially more value
than its monthly cost through a cohesive experience that makes an Adventure
easier to plan, calmer to manage, richer to experience, and more meaningful to
preserve.

The product is not designed to maximize short-term revenue or manufacture
frustration so that customers upgrade. Revenue supports reliable operations,
security, support, and continuous improvement; it follows demonstrated customer
value and trust.

The reference question for product prioritization is:

> Would this have made a real Adventure easier to plan, calmer to manage, or
> richer to remember?

The Italy, Greece, and Croatia Adventure remains a practical reference for the
level of organization, readiness, confidence, and traveler usefulness the
platform should make broadly accessible.

## Simple Plan Direction

AdventuresSuite intends to offer no more than three understandable subscription
plans at launch and during normal product growth:

- **Free** — start planning a real Adventure and experience genuine value;
- **Explorer** — build and organize the complete Adventure; and
- **Navigator** — coordinate, prepare, and travel with greater intelligence,
  collaboration, and confidence.

These are customer-facing promises, not authorization roles or application
branch names. Exact prices, capability bundles, and allowances require separate
commercial approval. Explorer should be the complete and natural choice for
most travelers. Navigator should add meaningful convenience and advanced value;
it must not make Explorer deliberately incomplete.

Free is a real versioned subscription, not the absence of billing and not a
frustrating demonstration. It should remain useful enough for a customer to
plan a bounded real Adventure and understand the platform's value.

## Promotions and Temporary Access

Trials, previews, launch offers, support remedies, and promotions are
time-bounded capability grants layered over the customer's base plan. They do
not create hidden fourth or fifth plans.

Examples include Navigator access for 30 or 60 days, an Explorer first-Adventure
offer, or a bounded AI planning preview. Every offer states what it grants, when
it begins, when it expires, and what experience remains afterward.

Time-bound access uses trusted UTC effective and expiration timestamps. The UI
may derive a human-friendly remaining-time display, but it never persists
countdown ticks. Expiration, revocation, renewal, grace, and supersession are
explicit states or operations rather than implicit date arithmetic.

## Capability and Access Gates

The visible workspace is an authorized, entitlement-aware projection. A tool is
usable only when all applicable gates pass:

```text
authenticated actor
    AND valid Creator, traveler, or engagement relationship
    AND required permission for the resource and operation
    AND active Platform Capability grant and remaining allowance
    AND enabled feature rollout
    AND available service or dependency
```

No gate substitutes for another. A subscription does not create Creator
membership, a purchased seat does not choose a role, a role does not purchase a
capability, and a feature flag does not grant contractual access.

Workspace tools use stable capability identifiers. Application code never
branches on `Free`, `Explorer`, or `Navigator`. Plan versions package stable
capabilities and allowances, and published plan versions are immutable.

## Tool Discovery

The workspace distinguishes three presentation outcomes:

- **Available** — the actor may use the tool in the current context.
- **Discoverable subscription opportunity** — the tool is relevant and may be
  shown with a restrained explanation of the plan or temporary grant needed.
- **Unauthorized or irrelevant** — the tool is omitted so private resources,
  roles, relationships, or unavailable operations are not disclosed.

Hiding an icon is presentation, not enforcement. Every underlying query,
command, export, background job, and API operation independently re-evaluates
authorization and entitlement below the UI. Opening a screen before an expiry
does not authorize a mutation after expiry.

Each registered workspace tool should define:

- stable tool and Platform Capability identities;
- label, icon, route, group, and presentation order;
- required permission and resource context;
- applicable membership, traveler, or professional-engagement relationship;
- applicable lifecycle or planning state;
- required allowance, rollout, and service-availability gates;
- behavior when unavailable, expired, downgraded, or over allowance; and
- responsive and accessibility behavior.

## Planner Workspace Shell

The Planner uses a calm, responsive workspace shell with primary tools on the
left. The shell supports:

- an expanded navigation pane;
- a compact icon rail;
- user-adjustable width within accessible bounds;
- optional auto-hide that also responds to keyboard focus;
- an explicitly hidden state with a persistent accessible restore control; and
- a mobile overlay drawer rather than a permanently compressed work area.

The user's navigation width, display mode, and theme preference may persist per
user. A stale client preference never grants a tool; the server supplies the
current authorized tool projection.

On desktop, the working area may progressively support a left navigation pane,
central planning canvas, and contextual map, warnings, sources, or proposal
panel. Mobile reduces this to a useful single-column flow with contextual
drawers or dedicated screens.

## Icon Direction

Workspace tools use a cohesive AdventuresSuite SVG family rather than unrelated
third-party icon styles. Icons use consistent geometry and stroke weight,
inherit `currentColor`, scale cleanly, and remain legible in light and dark
themes. Labels normally appear beneath icons in the expanded navigation design.

The menu item owns the accessible name; decorative SVG content does not repeat
it. State and meaning never depend on the icon or color alone. Candidate visual
metaphors include a compass for Overview, connected waypoints for Route, a
calendar route for Itinerary, a travel pack for Packing, and layered map marks
for Maps.

## Dark Mode and Visual Tokens

Dark mode is a launch requirement for the private workspace. The design supports
light, dark, and system preferences through semantic tokens from the beginning;
it is not implemented by inverting finished light colors.

Tokens distinguish navigation, canvas, cards, drawers, overlays, borders,
focus, and text surfaces. Semantic states cover proposed, reserved, confirmed,
changed, cancelled, warning, stale, private, and unavailable content. Maps,
charts, and generated visualizations require compatible dark treatments.

Both themes meet WCAG 2.2 AA contrast and focus requirements, avoid color-only
meaning, respect reduced motion, and prevent a startup flash of the wrong theme
where practical.

## Customer Protection and Trust

AdventuresSuite earns trust through predictable behavior:

- pricing and ordinary limits are understandable on one screen;
- Free provides real value and upgrade prompts remain restrained;
- promotions disclose scope, duration, and post-expiration behavior;
- expiration, downgrade, payment failure, or allowance exhaustion never
  silently deletes, transfers, publishes, or crosses Creator boundaries;
- existing work remains recoverable and appropriately viewable or exportable;
- premium creation or execution may be restricted without holding customer data
  hostage;
- privacy, security, accessibility, and core recovery are not premium safety
  features; and
- AI remains optional, reviewable assistance and never invents authority.

Customer data outlives a temporary commercial state according to explicit
retention and recovery policy. Subscription expiry does not revoke Creator
membership, change an Adventure lifecycle state, or prove that a user lost
resource authorization.

## Allowances and Cost Stewardship

Some expensive capabilities may use transparent allowances, including AI,
storage, maps, document extraction, notifications, and high-volume exports.
Allowances are contractual quantities distinct from authorization and
operational telemetry. Normal customers should not encounter surprising hidden
throttles or charges.

Exceeding an allowance blocks or defers the relevant new operation according to
policy; it does not destroy existing work. Long-running or queued operations
carry Creator and capability context and re-evaluate time-sensitive entitlement
when they execute. Usage is never billed or denied from logs, metrics, analytics,
or caller-submitted quantities.

## Plan Evolution

New features may be added to Free, Explorer, or Navigator over time. A packaging
change creates an immutable new plan version. Whether existing subscribers
receive it automatically, opt into it, or retain a grandfathered version is an
explicit product decision supported by a controlled reconciliation operation.

Feature rollout remains separate from packaging. A capability can be entitled
but not yet released to a cohort, or released but not included in a Creator's
plan. Temporary grants can preview a capability without changing the base plan.

## Product Success

The desired customer response is not merely that AdventuresSuite has many
features. It is that the connected experience remembered important details,
showed what was missing, kept travelers informed, reduced manual work, and made
the Adventure feel manageable.

The primary product question is:

> Did AdventuresSuite make this Adventure easier, calmer, and more memorable
> than planning it without us?

Sustainable revenue, retention, and word of mouth should follow that outcome.
