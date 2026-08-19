# Domain Model

**Version:** 1.4

**Status:** Approved

**Last Updated:** August 2026

---

# Purpose

The Domain Model defines the core business concepts of AdventuresSuite.

Every feature, service, API, AI workflow, mobile application, publishing capability, and user interface should build upon these concepts.

These concepts should remain stable over the lifetime of the platform.

Technologies will evolve.

User interfaces will evolve.

Artificial Intelligence will evolve.

The language of AdventuresSuite should remain consistent.

---

# Philosophy

AdventuresSuite is not organized around books.

It is not organized around websites.

It is organized around Adventures.

Everything else is derived from an Adventure.

Books are one expression of an Adventure.

Journeys are one way of experiencing an Adventure.

The Adventure remains the source of truth.

---

# Core Domain

Creator

↓

Adventure

↓

Volume

↓

Journey

↓

Journey Segment

↓

Destination

↓

Experience

↓

Memory

Every future capability within AdventuresSuite should naturally fit somewhere
within this hierarchy.

---

# Creator

A Creator is the tenant and ownership boundary for Adventures and related
content.

Examples include:

- The Simonton Adventures
- Independent Travelers
- Families
- Professional Photographers
- Travel Bloggers
- Tour Companies
- Destination Organizations

The platform must not assume The Simonton Adventures is the only Creator.

Creators create and own Adventures. A Creator may publish its own content or
grant publishing capabilities to authorized Users. Publisher describes that
role or capability; it is not a separate ownership boundary.

---

# Adventure

An Adventure represents a complete travel experience.

It owns:

- title
- subtitle
- description
- lifecycle
- travel dates
- cover artwork
- hero image
- Volumes
- Journeys
- Destinations
- Memories

Examples:

- Italy • Greece • Croatia
- Alaska Expedition
- Spain
- Japan

An Adventure is the primary business object of the platform.

---

# Adventure Lifecycle

Every Adventure progresses through:

Draft

↓

Planned

↓

Upcoming

↓

Current

↓

Published

↓

Archived

Only one Adventure should normally have the status of Current.

---

# Volume

A Volume represents a published chapter within an Adventure.

Volumes primarily organize editorial content.

Examples:

- Volume I
- Volume II
- Anniversary Edition
- Photography Edition

Books are generated from Volumes.

Volumes are editorial.

Journeys are experiential.

---

# Journey

A Journey represents one way of experiencing an Adventure.

Examples:

- Our Mediterranean Adventure
- Cruise Only
- Land Tour
- Food & Wine Journey
- Photography Journey

Multiple Journeys may exist within one Adventure.

Journeys organize movement.

They do not own destinations.

---

# Journey Segment

A Journey is composed of one or more Journey Segments.

A Journey Segment represents movement between two locations.

Examples:

- Flight
- Train
- Cruise
- Ferry
- Water Taxi
- Walking
- Car
- Bus

A Journey Segment may include:

- Origin
- Destination
- Travel Mode
- Transportation Details
- Coordinates
- Waypoints
- Timing
- Reservations
- Notes

Journey Segments tell the story of movement.

---

# Destination

A Destination represents a meaningful place that can be explored.

Destinations are reusable.

Multiple Journeys may reference the same Destination.

A Destination owns:

- hero image
- homepage image
- homepage summary
- story
- photography
- guide
- reflections
- resources
- maps
- experiences

Destinations belong to Adventures.

Not Journeys.

---

# Experience

Experiences happen within Destinations.

Examples include:

- Walking Tours
- Museums
- Restaurants
- Wine Tastings
- Cooking Classes
- Gondola Rides
- Excursions
- Scenic Viewpoints

Experiences enrich Destinations.

They do not replace the Story.

---

# Story

Every Destination contains one Story.

The Story is divided into Sections.

Each Section may contain:

- heading
- narrative
- editorial photography
- reflections

The Story remains the emotional center of every Destination.

---

# Memory

Memories preserve the experience.

Examples include:

- Journal Entries
- Reflections
- Photography
- Videos
- Voice Notes
- GPS Timeline
- Milestones

Memories exist to preserve emotion.

Not information.

Reflections become one type of Memory.

---

# Photography

Photography belongs primarily to Destinations.

Photography includes:

- Hero
- Homepage
- Story Images
- Gallery

Future:

- Panoramas
- Video
- Drone
- 360°
- Spatial Media

Photography should continue driving the visual experience.

---

# Guide

The Guide provides practical information.

Examples:

- Facts
- Highlights
- Travel Tips

Future:

- Accessibility
- Transportation
- Costs
- Best Time
- Safety
- Planning Notes

The Guide supports the Story.

It should never replace it.

---

# Resources

Resources extend the experience.

Examples:

- Official Websites
- Museums
- Maps
- Historical References
- Travel Planning

Future:

- Reservations
- Tickets
- Downloads
- Affiliate Partners
- Planning Services

---

# Book

A Book is generated from an Adventure.

Books are outputs.

They are not primary business objects.

Future publishing formats include:

- Print
- PDF
- EPUB
- Interactive
- Companion Edition

The Adventure remains the source of truth.

---

# User

A User is an authenticated person who may receive permissions within one or
more Creators.

Future roles include:

- Traveler
- Owner
- Editor
- Contributor
- Administrator

Users act on Creator-owned content according to their permissions.

User identity does not establish a separate content-ownership boundary.

---

# Organization

An Organization that owns Adventures is represented by a Creator.

Examples:

- Family
- Travel Company
- Church
- School
- Mission Organization
- University

Organizations support collaboration through Users and permissions within that
Creator.

---

# Subscriber

A Subscriber is a person who has established a verified audience relationship
with one or more Creators.

A Subscriber is not automatically a Creator User and receives no authoring
permissions. Subscriber identity may be platform-wide, while each audience
relationship remains isolated by Creator.

---

# Subscription

A Subscription records a Subscriber's consent to follow a Creator, Adventure,
or future supported target.

A Subscription belongs to a Creator boundary and includes its target, state,
consent evidence, delivery preferences, and lifecycle timestamps.

The first supported target should be Creator. Adventure-level following should
be introduced after the foundation is proven.

---

# Notification Event

A Notification Event records a meaningful subscriber-relevant public change,
such as a new Adventure announcement, lifecycle transition, or publication.

Draft saves and internal authoring changes are not Notification Events.

Notification Events are durable and auditable. Delivery is asynchronous and
must preserve Creator identity, consent, deduplication, and delivery history.

---

# Publication

A Publication is an approved output derived from an Adventure, such as a web
edition, EPUB, PDF, print-ready book, or photography collection.

A Publication is not automatically a commercial Product.

---

# Edition

An Edition identifies a particular version, format, language, or release of a
Publication. Its generated files are protected or public Resources according to
their publication and commercial state.

---

# Product

A Product is a Creator-owned commercial item offered through a storefront. It
may reference one or more Publications, Resources, services, or physical product
configurations.

Products do not own Adventure source content.

---

# Offer

An Offer defines the price, currency, availability, market, effective dates,
terms, and purchasable configuration of a Product.

---

# Order

An Order records a customer transaction and its payment and fulfillment state.
Orders retain a stable AdventuresSuite identity independent of external payment
and fulfillment provider identifiers.

---

# Entitlement

An Entitlement records a customer's right to access a purchased digital
Publication, Resource, or protected experience.

---

# Fulfillment

Fulfillment delivers a purchased Product. It may grant protected digital access
or coordinate manufacture and shipment through a provider-neutral adapter.

---

# License

A License records defined usage rights granted for a Resource such as a
photograph. Purchasing a physical print does not grant copyright or reproduction
rights unless an explicit License states otherwise.

---

# AI

Artificial Intelligence is not a Domain Object.

It is a Platform Capability.

AI assists users in creating, organizing, preserving, and publishing Adventures.

AI may interact with:

- Adventure
- Journey
- Destination
- Experience
- Memory
- Photography
- Publishing

AI never owns content.

Creators own content.

---

# Relationships

Creator

└── Adventures

&nbsp;&nbsp;&nbsp;&nbsp;├── Volumes

&nbsp;&nbsp;&nbsp;&nbsp;├── Journeys

&nbsp;&nbsp;&nbsp;&nbsp;│   └── Journey Segments

&nbsp;&nbsp;&nbsp;&nbsp;├── Destinations

&nbsp;&nbsp;&nbsp;&nbsp;│   ├── Story

&nbsp;&nbsp;&nbsp;&nbsp;│   ├── Experiences

&nbsp;&nbsp;&nbsp;&nbsp;│   ├── Photography

&nbsp;&nbsp;&nbsp;&nbsp;│   ├── Guide

&nbsp;&nbsp;&nbsp;&nbsp;│   ├── Resources

&nbsp;&nbsp;&nbsp;&nbsp;│   └── Maps

&nbsp;&nbsp;&nbsp;&nbsp;├── Memories

&nbsp;&nbsp;&nbsp;&nbsp;└── Books

---

# Ownership Rules

Creator owns Adventures and all Creator-specific content.

Publisher is a role or capability exercised within Creator scope.

Users receive permissions within one or more Creators and do not create a
parallel ownership boundary.

Organizations that own content are represented by Creators.

Adventure owns:

- lifecycle
- publication state
- Volumes
- Journeys
- Destinations

Journey owns:

- Journey Segments

Destination owns:

- Story
- Photography
- Guide
- Resources
- Experiences

Book owns formatting.

AI owns nothing.

---

# Design Rule

Whenever a new feature is proposed, ask:

**Which Domain Object owns this?**

If the answer is unclear, the feature probably needs to be redesigned.

---

# Guiding Principle

Keep the Domain Model simple.

Keep responsibilities clear.

Protect the language of the platform.

A stable Domain Model creates software that can evolve for decades.
