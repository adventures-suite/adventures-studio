# AdventuresCompanion Product Direction

**Status:** Approved Direction

**Last Updated:** August 9, 2026

## Product Promise

AdventuresCompanion helps travelers confidently experience an Adventure and
preserve meaningful memories without requiring continuous connectivity.

It is the first AdventuresSuite mobile application for iOS and Android. The
Companion begins with the active traveler experience rather than reproducing
the full Creator Workspace on a smaller screen.

## First Experience

The initial product centers on:

- what is happening today and next;
- an accessible countdown for every Planned, Upcoming, or otherwise approved
  committed Adventure;
- correct local time and time-zone context;
- essential itinerary, transportation, accommodation, reservation, map, task,
  and reminder information;
- selected information available offline;
- an authorized, minimized Adventure Travel Playbook available offline;
- explicit Add to Device Calendar for selected itinerary items;
- journaling, photography, and memory prompts;
- safe photo capture and later upload;
- meaningful, privacy-safe plan-change notifications; and
- optional GPS breadcrumbs controlled by the traveler; and
- traveler-specific readiness, material-change acknowledgment, required
  actions, contingencies, and smart reminders.

## Traveler-Controlled Breadcrumbs

When a traveler chooses to enable breadcrumbs, Companion can privately capture
the route traveled during an Adventure. The trail can later help reconstruct a
day, place photographs, remember an unexpected stop, or create a reviewed map
for a story or book.

The experience must make these truths unmistakable:

- tracking is off by default;
- the traveler chooses whether and when it runs;
- denying location does not prevent core Companion use;
- active capture is visible and can be paused or stopped;
- private synchronization is not public sharing;
- collaborators do not automatically receive precise location;
- publishing a route is a separate review and approval step; and
- AdventuresCompanion is not an emergency tracking or rescue service.

The traveler should be able to review a private trail, correct or remove
eligible points or segments, choose a privacy-reduced presentation, and decide
whether any route becomes part of an Adventure publication.

## Experience Principles

- Calm and useful during travel, not notification-heavy.
- Offline-capable for the information that matters most.
- Local-time aware across time-zone transitions.
- Accessible, touch-friendly, and usable in motion without encouraging unsafe
  interaction.
- Private by default and explicit about location, media, and sharing.
- Battery-conscious and transparent about background activity.
- Helpful when permission, connectivity, or device capability is limited.
- Consistent with Creator ownership and traveler autonomy.
- Calendar access is optional, traveler-controlled, and useful without
  provider-specific account connection.

## Relationship to the Platform

Companion reads approved Planning Engine state. It does not infer a booking,
change authoritative plans without an authorized operation, expose private
records through public content, or use a mobile token as Creator permission.

The Planning Engine remains authoritative when Companion presents a Travel
Playbook or creates a device-calendar entry. Companion identifies stale output,
never treats device-calendar edits as plan changes, and keeps ticket codes,
booking PINs, private notes, and protected-document URLs out of calendar
content.

Countdowns are derived locally from the last authorized Adventure start
projection and trusted device time. Date-only plans remain day-level; Companion
does not invent a departure time. Offline countdown and Today and Next views
show stale state when the underlying projection is overdue for synchronization.

Memories and media captured during travel remain private Resources until the
Creator deliberately preserves, authors, and publishes selected material. A
future AI Companion may propose assistance, but AI remains advisory and cannot
enable tracking, grant sharing, or publish a route.

## Initial Success Measures

- Travelers can access essential information without connectivity.
- Time-zone and schedule presentation remains correct during the Adventure.
- Permission denial produces a complete reduced-capability experience.
- Breadcrumb capture is understandable, visible, battery-conscious, and under
  traveler control.
- No precise location leaks through notifications, telemetry, analytics, or
  public content.
- Captured memories can later enter the Creator's preservation workflow without
  duplicate entry.
