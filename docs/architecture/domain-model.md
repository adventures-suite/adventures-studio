# Domain Model

**Version:** 1.0

**Status:** Approved

**Last Updated:** July 31, 2026

---

# Purpose

The Domain Model defines the core business concepts of Adventure Platform.

Every feature, service, API, AI workflow, and user interface should build upon these concepts.

These concepts should remain stable over the lifetime of the platform.

---

# Philosophy

Adventure Platform is not organized around books.

It is not organized around websites.

It is organized around Adventures.

Everything else is derived from an Adventure.

---

# Core Domain

Adventure

↓

Journey Stops

↓

Destinations

↓

Stories

↓

Photography

↓

Reflections

↓

Resources

↓

Book

---

# Adventure

An Adventure represents a complete travel experience.

It owns:

- title
- subtitle
- description
- status
- travel dates
- cover artwork
- hero image
- timeline
- destinations

Examples:

- Italy • Greece • Croatia
- Icon Cruise
- Spain
- Japan

An Adventure is the highest level object in the system.

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

Only one Adventure should normally have the status of Current.

---

# Journey Stop

A Journey Stop represents one stop along the adventure timeline.

Examples:

- Phoenix
- Venice
- Florence
- Ravenna
- Dubrovnik
- Athens
- Santorini

Journey Stops define:

- order
- map position
- navigation
- travel flow

They do not contain stories.

---

# Destination

A Destination represents a place that can be explored.

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

A Destination belongs to exactly one Adventure.

---

# Story

Every Destination contains one Story.

The Story is divided into Sections.

Each section may contain:

- heading
- paragraphs
- editorial photography
- reflections

The Story is always the emotional center of the destination.

---

# Reflection

Reflections capture the personal experience.

Examples:

Steve's Notes

From Dianne's Journal

Reflections exist to preserve emotion.

Not information.

Multiple reflections may exist within a destination.

---

# Photography

Photography belongs to Destinations.

Photography includes:

Hero

Homepage

Story Images

Gallery

Future:

Panoramas

Video

360°

Drone

Photography should drive the visual experience.

---

# Guide

The Guide provides practical information.

Examples:

Facts

Highlights

Travel Tips

Future:

Accessibility

Best Time

Transportation

Costs

The Guide supports the Story.

It should never replace it.

---

# Resources

Resources extend the experience.

Examples:

Official websites

Museums

Maps

Historical references

Travel planning

Future:

Affiliate partners

Reservations

Tickets

Downloads

---

# Book

A Book is generated from an Adventure.

Books are outputs.

Not primary objects.

Future publishing formats include:

Print

PDF

EPUB

Interactive

The Adventure remains the source of truth.

---

# User

A User owns one or more Adventures.

Future roles:

Traveler

Editor

Contributor

Administrator

Organization

---

# Organization

Organizations may own multiple Adventures.

Examples:

Family

Travel Company

Church

School

Mission Organization

University

Organizations support multi-user collaboration.

---

# AI

Artificial Intelligence is not a domain object.

It is a platform service.

AI assists users in creating and managing Adventures.

AI may interact with:

Adventure

Destination

Story

Reflection

Photography

Publishing

AI never owns content.

The user owns the content.

---

# Relationships

Adventure

├── Journey Stops

├── Destinations

│   ├── Story

│   ├── Reflections

│   ├── Photography

│   ├── Guide

│   ├── Resources

│   └── Maps

└── Book

---

# Ownership Rules

Adventure owns:

- timeline
- order
- lifecycle
- publication state

Destination owns:

- presentation
- photography
- summaries
- stories
- reflections
- homepage content

Book owns:

- formatting

AI owns nothing.

---

# Design Rule

Whenever a new feature is proposed, ask:

Which domain object owns this?

If the answer is unclear, the feature probably needs to be redesigned.

---

# Guiding Principle

Keep the domain simple.

Keep responsibilities clear.

A clean domain model creates software that can evolve for decades.