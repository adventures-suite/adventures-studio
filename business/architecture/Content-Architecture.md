# Content Architecture

**Version:** 1.0 Draft  
**Status:** Living Document  
**Owner:** Founders  
**Last Updated:** August 2026

---

# Purpose

This document defines how Adventures Studio organizes content.

It is intentionally independent of implementation.

Whether content is stored as JSON, SQL, APIs, cloud storage, or future technologies should not change this architecture.

This document defines the logical organization of Adventures Studio.

---

# Guiding Principle

Every file represents one business object.

One object.

One source of truth.

Never duplicate ownership.

---

# Content Hierarchy

Publisher

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

---

# Publisher

Owns Adventures.

Examples:

- Adventures Studio
- Steve & Dianne
- Independent Creator
- Tour Company
- Family

Publishers create Adventures.

---

# Adventure

Represents a complete travel experience.

Examples:

- Italy • Greece • Croatia
- Alaska Expedition
- Japan
- Route 66

Owns:

- Volumes
- Journeys
- Destinations
- Memories

---

# Volume

Represents editorial content.

Volumes primarily organize published material.

Examples:

Volume I

Volume II

Photography Edition

Anniversary Edition

Books are generated from Volumes.

---

# Journey

Represents one way of experiencing an Adventure.

Examples:

Our Mediterranean Adventure

Cruise Only

Land Tour

Photography Tour

Food & Wine Tour

Multiple Journeys may exist within one Adventure.

---

# Journey Segment

Represents movement.

Examples:

Flight

Train

Cruise

Walking

Taxi

Journey Segments connect locations.

They describe movement.

Not destinations.

---

# Destination

Represents a place.

Destinations are reusable.

Many Journeys may reference the same Destination.

Destinations own:

- Story
- Photography
- Guide
- Resources
- Experiences

---

# Experience

Represents something people do.

Examples:

Restaurant

Museum

Tour

Hike

Excursion

Cooking Class

Wine Tasting

Experiences happen inside Destinations.

---

# Memory

Represents something preserved.

Examples:

Journal

Photo

Video

Reflection

Voice Memo

GPS Timeline

Memories belong to Adventures.

---

# Ownership Rules

Every business object has exactly one owner.

Publishers own Adventures.

Adventures own Volumes.

Adventures own Journeys.

Journeys own Journey Segments.

Adventures own Destinations.

Destinations own Experiences.

Adventures own Memories.

Ownership should never be duplicated.

---

# Content Principles

One file should represent one business object.

Business objects reference one another.

Objects should never duplicate information owned elsewhere.

Content should be reusable.

Relationships should be explicit.

The structure should remain stable for decades.

---

# Future Growth

This architecture is intentionally designed to support:

- Multiple Publishers
- Creator Marketplace
- Companion
- Artificial Intelligence
- APIs
- Mobile Applications
- Printed Books
- Interactive Experiences

Future technology should extend the architecture.

It should never replace it.

---

# Final Principle

The content architecture should feel inevitable.

Every new feature should naturally fit into this model.

If it doesn't...

The architecture should be questioned before the feature is built.