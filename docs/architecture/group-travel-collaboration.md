# Group Travel and Contextual Collaboration

**Status:** Approved Platform Requirement and Architecture Direction
**Last Updated:** August 9, 2026

## Purpose

AdventuresSuite supports Adventures planned for families, friends, clubs,
customers, and other groups. An authorized Creator can invite travelers,
collect preferences, discuss specific Planning subjects, run structured votes,
communicate important changes, and record decisions.

AdventuresSuite does not become a general-purpose chat or social network.
Collaboration exists to help a group make, understand, and acknowledge
Adventure decisions.

## Governing Principles

> Travelers express preferences; authorized planners make Planning decisions.

> Discussion or poll -> authorized decision -> validated Planning mutation.

A message, reaction, vote, poll result, or announcement never directly changes
the authoritative Adventure Plan. The Planning Engine remains the sole owner of
plan state and applies only authorized, validated operations.

## Participation Boundary

A Planning `Traveler` describes a participant in an Adventure but does not by
itself authenticate a person. A `TravelerParticipation` relationship binds an
authenticated `UserId`, one customer Creator, one `AdventurePlanId`, and the
appropriate `TravelerId` after an explicit invitation and acceptance flow.

Traveler participation:

- is Adventure-scoped and never grants Creator membership;
- has explicit status, effective period, expiration, and revocation;
- uses a version so stale sessions or cached projections fail closed;
- receives only traveler permissions and information-policy-filtered views;
- does not allow enumeration of other Creator resources or Adventures; and
- ends immediately for future access when revoked or the traveler is removed.

One user may participate in multiple Adventures and may separately hold Creator
membership. Those authorization bases remain distinct.

A reusable Traveler Profile remains separate from both Planning `Traveler` and
`TravelerParticipation`. Adventure-specific preferences may override profile
defaults without changing them. Group choices use scoped decision authority,
hard-constraint precedence, explicit traveler sets, and visible conflict
resolution rather than globally ranking travelers. See
`docs/architecture/traveler-profile-and-preference-resolution.md`.

## Group Structure

A group Adventure may organize travelers into optional, explicit subgroups such
as households, rooms, cabins, vehicles, activity groups, or custom cohorts.
Subgroups help target proposals, tasks, polls, announcements, and information
without creating new tenancy boundaries.

Membership in one subgroup must not reveal another subgroup's private
discussion, costs, room assignments, surprises, or traveler-private details.
Subgroup visibility and planner override rules are explicit and auditable.

## Bounded Collaboration Primitives

### Contextual Discussion Threads

A thread attaches to an Adventure or a specific Planning subject, such as:

- destination visit or itinerary day;
- proposed activity or accommodation;
- transportation segment;
- AI or professional proposal;
- poll, decision, task, or material change; or
- an approved announcement.

Threads support bounded text comments, replies, mentions, and protected
attachments through the Resource Engine. They do not introduce contacts,
general direct messaging, public rooms, social graphs, presence indicators,
voice calls, or video calls.

### Structured Polls

A `PlanningPoll` contains a clear question, eligible participants, one or more
typed options, response rules, deadline, visibility policy, status, and the
Planning subject it informs.

Polls may support choices such as preferred dates, destinations, activities,
excursions, lodging, transportation, budget ranges, or meal options. Response
semantics may include ranked preference, approval, interest, availability,
abstention, or bounded multiple selection.

Results are advisory. A planner records the final `PlanningDecision`, including
the selected outcome and optional rationale. Adoption creates a separate
validated Planning operation. Majority vote never overrides authorization,
constraints, accessibility, safety, availability, professional responsibility,
or the Creator's decision authority.

### Announcements and Acknowledgments

Authorized planners may issue Adventure-scoped or subgroup announcements for
approved information such as itinerary changes, deadlines, meeting points, and
required actions. An announcement may request acknowledgment, but delivered,
viewed, acknowledged, accepted, and completed remain distinct states.

Announcements are not emergency services. Sensitive details are minimized in
push, email, SMS, and lock-screen previews; authenticated deep links lead to the
authorized context.

## Privacy and Information Policy

- Voting may be named, anonymous-to-peers, or aggregated according to an
  explicit policy; the platform still retains the minimum authoritative actor
  evidence required for integrity and abuse handling.
- A traveler may respond "unable to participate" without disclosing medical,
  financial, accessibility, or other private reasoning to the group.
- Planner-only notes, surprise activities, budgets, confirmation details, and
  protected documents never enter group discussion implicitly.
- Attachments are protected Resources with malware scanning, retention,
  authorization, and short-lived access.
- Notifications contain the minimum safe preview and honor traveler channel,
  quiet-hour, time-zone, and accessibility preferences.
- Messages, votes, and attachments do not become public memories or Content
  Engine records without a separate, explicit preserve-and-publish selection.

## Moderation, Safety, and Retention

Before external groups are enabled, the platform must define reporting,
blocking, content removal, participant removal, rate limits, anti-spam controls,
retention, legal hold, export, deletion, and support-access procedures.

Edits and deletions require visible semantics appropriate to the record type.
Required decision and audit evidence must survive according to policy without
retaining unnecessary conversation content indefinitely. AdventuresSuite does
not promise end-to-end encryption unless a separately reviewed architecture
actually provides it.

## External Messaging Boundary

AdventuresSuite may offer privacy-safe share links or "Share to Messages/email"
actions so groups can continue using their preferred communication tools.
External messages are delivery conveniences, not authoritative conversation or
Planning state. Replies remain outside AdventuresSuite unless a separately
approved, authenticated, consented, provider-neutral integration imports them.

## AI Boundary

AI may summarize an authorized thread, identify unresolved questions, or draft
a poll or Planning proposal only with explicit purpose and least-data context.
Summaries link back to their source messages, expose freshness, remain
reviewable, and never replace the original record or commit Planning changes.
Private conversations are excluded from AI by default.

## Audit and Reporting

Required audit coverage includes invitations, acceptance, revocation, subgroup
changes, poll creation and closure, decision recording, announcements requiring
acknowledgment, moderation, export, and sensitive administrative access.

Audit metadata does not copy message bodies, vote reasoning, attachments, or
traveler-private data. Authorized reporting may show bounded participation,
response, acknowledgment, and decision-cycle measures without exposing content
or enabling cross-Creator comparison of identifiable travelers.

## Incremental Delivery

1. Define provider-neutral traveler participation, invitation, subgroup,
   information-policy, and authorization contracts.
2. Add contextual comments on proposals and itinerary subjects.
3. Add structured polls, eligibility, response privacy, deadlines, closure, and
   explicit planner decisions.
4. Add announcements, safe notifications, and acknowledgments.
5. Add Companion participation through minimized, revocation-aware APIs and
   offline projections.
6. Add protected attachments, moderation, export, retention, and support tools.
7. Add optional authorized AI summaries and external share actions.

Real-time transport is an implementation detail and is not required for the
first useful collaboration release. Durable, contextual, refreshable threads
and polls should be proven before near-real-time delivery is considered.

## Definition of Done

- Travelers can participate without receiving Creator membership.
- Every operation is Creator-, Adventure-, participant-, and subject-scoped.
- Revocation and subgroup visibility changes take effect immediately.
- Poll eligibility, privacy, deadlines, concurrency, and result integrity are
  deterministic and tested.
- Votes remain advisory and cannot mutate Planning directly.
- Discussions remain contextual rather than becoming general-purpose chat.
- Announcements distinguish delivery, viewing, acknowledgment, and completion.
- Private content cannot leak through notifications, telemetry, AI, exports,
  attachments, or public routes.
- Moderation, retention, deletion, audit, accessibility, and cross-Creator
  negative tests pass.
