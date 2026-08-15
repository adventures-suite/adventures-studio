# AdventuresSuite Experience and Design-System Direction

**Status:** Architecture and Product Direction

**Last Updated:** August 14, 2026

## Purpose

AdventuresSuite should provide beautiful, trustworthy, and easy-to-use
experiences for planners, travelers, public visitors, and future professional
partners. Quality comes from clear workflows, consistent visual language,
accessible behavior, and thoughtful states—not from maximizing the number of
components or adopting a commercial component suite wholesale.

The governing principle is:

> Build the AdventuresSuite experience; borrow complex behavior when doing so
> saves meaningful time and risk.

This direction establishes a deliberately small foundation. It is not approval
to build a general-purpose design framework, replace working interfaces, or
select a component vendor before a concrete need exists.

## Experience Priorities

Every significant screen should be:

- understandable without training or hidden conventions;
- calm and focused on the user's next meaningful action;
- visually cohesive with AdventuresSuite and the active Creator brand;
- responsive across the devices appropriate to its audience;
- accessible by keyboard, assistive technology, touch, and pointer;
- complete across loading, empty, partial, denied, expired, stale, offline, and
  failure states; and
- honest about proposals, bookings, availability, confidence, and system state.

When capability and simplicity conflict, prefer progressive disclosure over a
screen full of permanent controls. If a workflow needs substantial explanation,
first simplify the workflow rather than add more instructional UI.

## Audience-Specific Experiences

The platform shares a visual and component foundation without forcing every
audience into the same layout.

### Planners

Planner experiences are information-rich, efficient, and desktop-friendly while
remaining usable on smaller screens. They may use tables, timelines, maps,
context panels, multi-step forms, and dense lists when those structures improve
real planning work. Complexity should be staged through sensible defaults,
progressive disclosure, and clear hierarchy.

### Travelers

Traveler experiences are mobile-first, task-focused, and reassuring. They
prioritize what is happening next, what changed, what requires attention, and
what is available offline. They should avoid exposing Planner complexity that a
traveler does not need.

### Public Visitors

Public Creator sites remain editorial, visual, and story-led. Photography and
Creator voice carry the experience. Workspace controls and application density
must not leak into the public presentation merely because components are shared.

### Professionals and Other Future Users

Future professional surfaces should reuse proven foundations but preserve clear
ownership, proposal, approval, and customer-trust boundaries. A new audience is
not a reason to create an unrelated visual system.

## Small Shared Foundation

AdventuresSuite should maintain a focused set of semantic design tokens and
reusable Razor components. Initial tokens cover:

- typography, spacing, sizing, radius, elevation, and restrained motion;
- surface, text, border, focus, overlay, and navigation roles;
- light, dark, and system theme behavior;
- responsive breakpoints and touch targets; and
- semantic planning states such as proposed, reserved, confirmed, changed,
  cancelled, stale, warning, private, unavailable, and success.

Initial reusable components should address recurring needs such as:

- buttons, links, form fields, validation, and selection controls;
- cards, lists, list boxes, tables, timelines, and status indicators;
- navigation, tabs, breadcrumbs, drawers, menus, and tooltips;
- dialogs, confirmations, notifications, and contextual help; and
- loading, empty, partial, denied, expired, offline, and failure presentations.

Create or generalize a component when a pattern is proven across multiple
screens or when centralizing it materially improves accessibility, security, or
consistency. A single screen does not automatically justify a framework
abstraction.

Travel-specific components may compose these primitives into itinerary cards,
destination selectors, journey segments, traveler lists, readiness panels,
polls, booking summaries, and map/list alternatives. These compositions are
where AdventuresSuite should feel more useful than a generic administrative
dashboard.

## Build, Borrow, or Buy

Choose the smallest sustainable option for each concrete requirement.

### Build

Build presentation and travel-specific composition when it defines the product
experience, uses ordinary web behavior, or must integrate deeply with Creator
branding and domain state.

### Borrow

Use a mature accessible primitive or open-source component when behavior is
subtle and well understood, including focus management, overlays, menus,
tooltips, tabs, date selection, or keyboard-intensive selection. Evaluate its
accessibility, maintenance, bundle size, styling control, Blazor compatibility,
security posture, and license before adoption.

### Buy

Consider a commercial component only when it avoids substantial delivery or
maintenance risk for a proven need, such as a genuinely advanced data grid,
scheduler, charting surface, document tool, or rich editor. Commercial polish
alone is not sufficient justification.

Do not choose a platform-wide vendor from a speculative feature list. Record the
specific requirement, alternatives considered, licensing and operating cost,
accessibility evidence, theming fit, performance impact, and exit strategy.

## Provider and Vendor Boundary

Third-party controls must be wrapped behind cohesive AdventuresSuite Razor
components or narrow adapters when their types or conventions would otherwise
spread through product code. Domain and application contracts never depend on a
visual component vendor.

The wrapper owns AdventuresSuite terminology, authorization-safe states,
theming, accessibility expectations, telemetry policy, and fallback behavior.
This boundary permits replacement without redesigning every screen, but it
should remain thin; do not recreate an entire vendor API internally.

Client-side visibility never substitutes for authorization, entitlement,
consent, or lifecycle enforcement below the UI.

## Razor Component Structure

Stateful Razor pages and components use a colocated code-behind partial class.
The `.razor` file should express semantic markup, bindings, and component
composition. Its `.razor.cs` counterpart owns injected dependencies,
parameters, state, lifecycle methods, event handlers, and non-trivial
presentation logic. Component-specific styling remains in the colocated
`.razor.css` file when CSS isolation is appropriate.

This is a maintainability rule, not a requirement to split every fragment into
multiple files. A small presentation-only component may remain entirely in its
`.razor` file when it has no injected service, lifecycle behavior, event
handling, or meaningful state. Move code behind as soon as the component
becomes stateful or its inline code obscures the rendered structure. Prefer
extracting a cohesive child component when either the markup or its behavior
represents a reusable product concept; do not use code-behind to preserve a
monolithic page.

Code-behind remains part of the presentation layer. It may coordinate an
authorized application service and prepare view state, but it must not become
the sole enforcement point for Creator isolation, resource authorization,
entitlements, consent, lifecycle rules, validation, concurrency, audit intent,
or persistence transactions. Those rules remain below the UI and must hold for
every caller.

All public component classes, parameters, properties, and methods follow the
platform XML-documentation rule. Private members should carry comments when
their intent, security significance, state transition, or failure behavior is
not evident from the code. Tests should exercise stable rendered behavior and
important interactions rather than relying on whether implementation lives in
markup or code-behind.

## Quality Bar

Every shared component and major workflow should be verified in proportion to
its risk for:

- semantic markup, accessible names, focus order, and keyboard operation;
- WCAG 2.2 AA contrast and visible focus in light and dark themes;
- zoom, text resizing, reduced motion, and common viewport sizes;
- touch target size and behavior where mobile use is expected;
- loading, no-data, partial-data, error, denied, and unavailable states;
- long labels, realistic content, localization growth, and date/time clarity;
- performance, payload size, and avoidance of unnecessary client work; and
- no Creator, traveler, subscription, or resource disclosure through UI state.

Dark mode is a first-class workspace requirement, designed through semantic
tokens rather than inverted colors. Maps, charts, icons, images, focus states,
and overlays require deliberate treatment in both themes.

Automated tests should protect stable semantics and important interactions,
not freeze incidental CSS structure. Visual regression testing may be added for
high-value shared components once their design is stable. Major workflows
should also be observed with representative users; passing automated tests does
not prove that a screen is easy to understand.

## Evolution and Restraint

The design system grows from shipped product needs. Prefer a small, documented
catalog with excellent examples over hundreds of configurable components.
Before adding a new primitive or dependency, determine whether an existing
component, composition, or native control already satisfies the requirement.

Avoid:

- building a standalone design-system product ahead of AdventuresSuite needs;
- broad rewrites solely for visual consistency;
- mixing unrelated icon or component styles on the same surface;
- exposing every option at once because the domain supports it;
- making public storytelling resemble an enterprise dashboard; and
- sacrificing usability, accessibility, or performance for decorative effects.

## Decision Test

A design or component decision is successful when a new user can understand
what matters, identify the next action, complete it with confidence, and recover
from a problem without needing product training. The experience should feel
distinctly AdventuresSuite even when mature third-party behavior is used under
the surface.
