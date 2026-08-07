# Adventure Lifecycle

**Version:** 1.1

**Status:** Approved

**Last Updated:** August 7, 2026

---

# Purpose

Every Adventure follows a lifecycle.

This document defines that lifecycle.

Every feature in AdventuresSuite should support one or more stages.

The lifecycle is one of the core concepts of the platform.

---

# Philosophy

An Adventure is not simply a completed trip.

It begins long before departure.

It continues long after returning home.

AdventuresSuite exists to support the complete journey.

---

# The Adventure Lifecycle

Dream

↓

Plan

↓

Travel

↓

Preserve

↓

Publish

↓

Share

↓

Remember

Each stage builds naturally upon the previous one.

Subscribers may follow a Creator or Adventure across these stages. Following is
a cross-cutting audience relationship rather than a separate lifecycle stage.

---

# Stage 1

## Dream

Every Adventure begins as an idea.

Examples:

"We want to visit Spain."

"We've always wanted to cruise Alaska."

"We should take the kids to Yellowstone."

At this stage AdventuresSuite should help users:

Discover destinations

Compare ideas

Estimate budgets

Explore possibilities

Save inspiration

Nothing is committed yet.

---

# Stage 2

## Plan

Planning transforms ideas into reality.

Planning includes:

Flights

Hotels

Cruises

Transportation

Tours

Restaurants

Photography planning

Packing

Budgeting

Travel documents

Reservations

Maps

Weather

The AI assists throughout planning.

Destination plans use date-only arrival and departure values. They describe
the expected local calendar dates and may change as the itinerary develops.
They are not instants and are not converted between time zones.

Journey visits add operational local timing for a particular itinerary. Cruise
port calls may include arrival, gangway down, gangway up, and departure times.
These remain provisional during planning, use the destination's IANA time
zone, and must not be copied into the reusable Destination as permanent timing.

---

# Adventure Status

During planning an Adventure progresses through:

Draft

↓

Planned

↓

Upcoming

Only one Adventure should normally become Current.

---

# Stage 3

## Travel

The Adventure is now active.

Status:

Current

The website reflects the active journey.

AdventuresSuite becomes a travel companion.

Examples:

Today's itinerary

Maps

Reservations

Packing reminders

Photography suggestions

Journal prompts

Daily summaries

Travel continues until the adventure ends.

After travel, a destination may record the authoritative date-only range that
was actually visited. Planned and visited ranges remain separate so the
preserved story can reflect what occurred without rewriting the original plan.

---

# Stage 4

## Preserve

After returning home the focus changes.

Now the goal becomes preserving memories.

Activities include:

Writing

Organizing photographs

Building destination pages

Captions

Maps

Resources

Journals

Reflections

Nothing should be lost.

---

# Stage 5

## Publish

Publishing transforms memories into lasting artifacts.

Outputs include:

Website

Book

PDF

EPUB

Print

Interactive editions

The Adventure remains the source of truth.

Publishing should never require duplicate work.

---

# Stage 6

## Share

Publishing is only the beginning.

Users may share:

Websites

Books

QR codes

Photography

Resources

Public journals

The platform should make sharing effortless.

---

# Stage 7

## Remember

The final stage never ends.

Families return to Adventures for years.

Children discover them.

Grandchildren inherit them.

Books become heirlooms.

The website becomes a living archive.

This is the true purpose of the platform.

---

# Lifecycle Ownership

Dream

Adventure Advisor

Plan

AdventuresSuite

Adventure Advisor

Travel

AdventuresSuite

Adventure Advisor

Adventure Maps

Preserve

AdventuresSuite

Adventure Photos

Adventure Advisor

Publish

Adventure Publisher

Adventure Web

Share

Adventure Web

Adventure Publisher

Remember

AdventuresSuite

---

# Adventure States

Every Adventure exists in one state.

Draft

Internal work only.

Planned

Public.

Planning has begun.

Upcoming

Travel is approaching.

Current

Adventure in progress.

Homepage focuses on this Adventure.

Published

Completed.

Available forever.

Only one Adventure should normally be Current.

Multiple Adventures may be Published.

---

# Homepage Behavior

Homepage always emphasizes:

Current Adventure

Journey Timeline

Featured Destinations

Published Adventures

Future Adventures

The homepage should evolve automatically as Adventure status changes.

---

# Subscriber Experience Across the Lifecycle

Subscribers should be able to follow meaningful public progress without being
notified about internal authoring activity.

Examples include:

Dream and Plan

New Adventure announcements and Creator-selected planning updates.

Upcoming and Travel

Approaching departure, Adventure start, and meaningful active-travel updates.

Preserve and Publish

New stories, destinations, galleries, editions, and completed publications.

Share and Remember

New resources, retrospective stories, and significant Creator-designated
revisions.

Notifications occur only after successful publication or another explicit
public event. Minor corrections may be published silently.

Destination audit timestamps and publication timestamps are content metadata,
not deployment metadata. During JSON-backed authoring they are maintained by
authors and must change only for meaningful content or publication activity.
They become system-controlled with database-backed publishing. In particular,
`LastPublishedAtUtc` does not automatically produce a subscriber notification;
notification requires an explicit public domain event and applicable policy.

---

# AI Across The Lifecycle

Dream

Recommendation

Plan

Assistant

Travel

Companion

Preserve

Storyteller

Publish

Publisher

Remember

Historian

AI evolves with the Adventure.

---

# Design Goal

AdventuresSuite should feel like one continuous experience.

Not a collection of unrelated applications.

The traveler should never feel like they are switching systems.

---

# Success

AdventuresSuite succeeds when users naturally move from dreaming about an Adventure to preserving it forever without ever leaving the ecosystem.

---

# Guiding Principle

An Adventure is not an event.

It is a story.

AdventuresSuite exists to help tell it.
