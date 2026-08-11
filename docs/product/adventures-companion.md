# AdventuresCompanion Product Direction

**Status:** Approved Direction

**Last Updated:** August 10, 2026

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

For a future Group Travel increment, Companion also provides the traveler's
authorized discussions, open polls, submitted preferences, announcements, and
acknowledgments in their itinerary context. It does not become a general chat
client, and votes or messages do not directly change the Adventure Plan.

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

## Memories Adventure Selector

The Memories page includes an Adventure selector for choosing the trip whose
private memories are being viewed or captured. When its dropdown is open, the
traveler can dismiss it by:

- tapping or clicking outside the dropdown;
- choosing an Adventure;
- activating the selector again;
- pressing Escape on a keyboard-capable platform; or
- invoking the applicable platform Back action.

Every dismissal path closes the same selector state, removes its overlay and
event handlers, updates the exposed expanded/collapsed accessibility state, and
returns focus to the selector when focus restoration is applicable. Selecting
an Adventure first applies that choice and then closes the dropdown. Dismissal
must not navigate away, change the selected Adventure, discard memory input, or
leave an invisible surface that intercepts page interaction.

The selector remains keyboard and screen-reader operable. Its label, current
selection, expanded state, options, and focus order are programmatically
available, and Back or Escape is consumed only while the dropdown is open.

## Appearance

AdventuresCompanion supports three appearance choices: `System`, `Light`, and
`Dark`. `System` is the default and follows operating-system appearance changes
while the app is running. An explicit Light or Dark choice overrides the system
appearance and persists across launches until the traveler changes it.

All Companion presentation uses shared semantic design tokens rather than
page-specific light or dark colors. The same active palette covers pages,
navigation, dialogs, cards, controls, loading/error/empty states, native chrome,
maps, and overlays. Text, icons, focus indicators, selected and disabled states,
map controls, and content placed over imagery meet applicable accessible
contrast requirements in both palettes.

The saved choice and effective startup palette are resolved before the first
interactive frame wherever the platform permits. Launch, resume, navigation,
and live System-theme changes must not briefly render the wrong palette or
leave native and Blazor surfaces in different themes. Appearance changes do not
alter private data, authorization, synchronization, or Companion API contracts.

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

Companion obtains its experience from versioned JSON REST responses and
authorized media or document delivery. It never connects to SQL or consumes
Dapper/persistence models. Offline content is a minimized encrypted JSON/media
projection with visible freshness and revocation behavior, not a database
replica.

Push notifications keep the experience relevant but carry only a safe signal
and deep-link hint. Companion retrieves the current authorized JSON after the
traveler opens or refreshes the notification. The in-app notification center
remains useful when native push is delayed, disabled, duplicated, or lost.

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
- The Memories Adventure selector closes through every supported dismissal path,
  restores accessible state and focus, and never leaves a page-blocking overlay.
- System, Light, and Dark modes render consistently across shared and native
  surfaces without startup theme flash and meet accessible contrast criteria.
