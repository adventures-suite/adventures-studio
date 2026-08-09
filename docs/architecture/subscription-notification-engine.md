# Subscription and Notification Engine

**Version:** 1.0

**Status:** Approved Direction

**Last Updated:** August 7, 2026

---

# Purpose

The Subscription and Notification Engine enables people to follow Creators and
Adventures and to receive relevant updates as those Adventures progress.

Subscribers are not merely entries in a mailing list. They are audience
participants who may follow an Adventure from planning through publication and
preservation.

---

# Core Principle

> Notify subscribers about meaningful published changes, not every edit.

Creator Studio may save drafts frequently. Draft saves, previews, validation,
and internal review are authoring activity and must not notify subscribers.

A subscriber-facing notification is created only after a successful public
publication or another explicit, approved public event. A Creator may choose to
publish a minor correction silently.

---

# Subscription Scope

The first supported scope should be a Creator subscription.

Future scopes may include:

- Creator
- Adventure
- Series or topic
- Destination
- Publication or edition

The initial product should prefer a small, understandable subscription model
over prematurely exposing every possible scope.

---

# Notification Events

Candidate subscriber events include:

- New Adventure announced
- Planned itinerary meaningfully updated
- Adventure moved to Upcoming
- Adventure started
- New destination, story, gallery, or video published
- Adventure completed
- Adventure or edition published
- Book, guide, or downloadable resource released
- Creator-designated significant correction

Each event must identify its Creator, event type, subject, publication, and
occurrence time. Events must be durable, auditable, and safe to process more
than once.

---

# Publishing Choices

Creator Studio should eventually support explicit publication choices:

- Publish normally
- Publish and notify subscribers
- Publish silently
- Schedule publication and notification

Notification policy may supply sensible defaults, but the Creator should retain
editorial control over whether a meaningful update is announced.

---

# Core Concepts

## Subscriber

A Subscriber represents a person who has established a verified audience
relationship with one or more Creators.

Subscriber identity may eventually be platform-wide, but a Creator may access
only the audience relationship and information authorized within that Creator
boundary.

## Subscription

A Subscription records a Subscriber's consent to follow a target.

It includes:

- Creator identity
- Subscriber identity or verified delivery address
- Subscription scope and target identity
- State
- Consent and confirmation timestamps
- Channel and frequency preferences
- Creation and cancellation timestamps

## Notification Event

A Notification Event is a durable statement that a subscriber-relevant public
change occurred. It is created from a successful publication or explicit
platform event, not from an uncommitted draft edit.

## Notification Delivery

A Notification Delivery records an attempt to deliver one Notification Event to
one Subscriber through one channel. It supports deduplication, retries,
suppression, diagnostics, and delivery history.

---

# Creator Ownership and Privacy

Every Subscription, Notification Event, Notification Policy, template, delivery,
and audience query must be scoped by `CreatorId`.

A Subscriber may follow multiple Creators. That does not permit one Creator to
inspect another Creator's subscriber list, preferences, engagement, or delivery
history.

Creators may receive audience insights such as subscriber counts, growth, and
aggregated delivery health. AdventuresSuite remains responsible for platform
identity, consent evidence, suppression, unsubscribe enforcement, delivery
safety, and privacy controls.

The Engine must support:

- Verified opt-in
- Immediate unsubscribe
- Per-Creator preferences
- Suppression and bounce handling
- Rate limiting
- Idempotent delivery and deduplication
- Audit history
- Data export and deletion
- Accessible preference management
- Quiet hours and digest delivery when introduced

Legal and policy requirements must be reviewed before production messaging is
enabled in any jurisdiction.

---

# Reliability Model

Publication and notification intent must not drift apart.

When database-backed publishing is introduced, AdventuresSuite should use a
transactional outbox. The publication state change and its Notification Event
are committed together. A background process then places delivery work onto a
durable queue.

```text
Creator publishes content
        ↓
Publication and outbox event commit together
        ↓
Notification policy selects eligible audience
        ↓
Subscriber consent and preferences are applied
        ↓
Durable delivery work is queued
        ↓
Channel provider attempts delivery
        ↓
Outcome is recorded and retried or suppressed as appropriate
```

This prevents notifications for failed publications and prevents successful
publications from silently losing their notification event.

Delivery must be asynchronous. Public requests and Creator Studio publication
must not wait for every message provider to respond.

---

# Channels

The first channel should be email.

Future channels may include:

- In-application notifications
- Mobile push
- SMS for explicitly appropriate use cases
- Web push
- Creator-configured digests

Channel providers are adapters. Domain concepts, consent, audience selection,
and delivery history must not depend on a particular vendor.

---

# Engine Relationships

```text
Creator Engine
    establishes ownership and Creator Context

Identity and Permission capabilities
    establish Subscriber and Creator-user identity

Content Engine and Creator Studio
    publish meaningful changes

Subscription and Notification Engine
    selects consenting audiences and orchestrates delivery

Rendering Engine
    renders Creator-branded message content

Analytics capability
    reports privacy-appropriate aggregate outcomes
```

The Subscription and Notification Engine does not decide whether content is
public. It consumes completed publication events from the owning content and
publishing capabilities.

---

# Expected Azure Direction

When implementation is justified, the likely architecture is:

- Azure SQL for Subscribers, Subscriptions, consent, policies, outbox events,
  and delivery records
- Azure Service Bus for durable asynchronous delivery work
- Azure Communication Services or another approved transactional email provider
- Managed Identity for service-to-service access

These technologies are an expected direction rather than a commitment. Engine
contracts must preserve storage and provider independence.

---

# Delivery Phases

## Phase 1: Architecture Foundation

- Define publication domain events
- Preserve Creator identity in every event and background operation
- Define Subscriber, Subscription, consent, and delivery contracts
- Define transactional outbox requirements

## Phase 2: Creator Email Subscription

- Subscribe to one Creator
- Verify the email address and consent
- Manage preferences
- Unsubscribe immediately
- Notify for explicitly selected publication events
- Record delivery outcomes

## Phase 3: Adventure Following

- Subscribe to a specific Adventure
- Follow lifecycle transitions
- Receive planning, departure, active-travel, and publication updates
- Support immediate or digest frequency

## Phase 4: Additional Channels and Insights

- In-application and mobile notifications
- Additional channels where justified
- Creator audience dashboards
- Privacy-appropriate engagement insights

---

# Current Implementation Status

This document establishes an approved future platform capability.

The current JSON-backed public application does not collect subscriber data or
send notifications. Implementation should begin only with the identity,
database-backed publication, consent, security, and operational foundations
needed to support it safely.
