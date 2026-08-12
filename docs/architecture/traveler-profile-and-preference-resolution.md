# Traveler Profile and Group Preference Resolution

**Status:** Approved Architecture Direction

**Last Updated:** August 12, 2026

## Purpose

AdventuresSuite needs a reusable, traveler-controlled Travel Profile so
planning can account for preferences and future authorized booking handoffs can
provide the minimum supplier-required information. Adventure participation does
not automatically expose a traveler's complete profile.

Group planning reconciles multiple preferences without globally ranking people.
Safety, accessibility, legal requirements, hard constraints, scoped decision
authority, and explicit group decisions precede ordinary preferences.

## Identity and Ownership Boundaries

```text
Platform User
    authenticates and controls authorized access

Traveler Profile
    reusable private information and defaults

Adventure Traveler
    represents that traveler in one Adventure Plan

Adventure Traveler Override
    trip-specific preferences and constraints

Supplier Booking Projection
    minimum consented fields for one external booking purpose
```

These records are not interchangeable. A Traveler Profile is not Creator
membership; a Planning `Traveler` is not proof of an authenticated person; one
user may manage a dependent through explicit guardian or delegate authority;
and a supplier projection never grants general profile access.

The traveler controls the reusable profile. Guardian or delegate authority has
explicit scope, effective period, revocation, and audit evidence.

## Profile Information

A future Travel Profile may contain:

- legal and preferred names;
- birth date and a supplier-required gender marker when necessary;
- contact information, home location, locale, and time zone;
- accessibility and assistance requirements;
- dietary preferences and allergies;
- seating, cabin, room, meal, and transportation preferences;
- Known Traveler Number, redress number, and similar identifiers;
- airline, cruise, lodging, rail, rental-car, and other loyalty memberships;
- passport and identity-document references;
- emergency contacts;
- travel-insurance preferences; and
- communication and notification preferences.

The platform collects data progressively rather than requesting everything
during onboarding merely because a future supplier might need it.

## Data Classification

| Class | Examples | Direction |
| --- | --- | --- |
| General preference | Window seat, travel pace, preferred transport | Private profile data |
| Personal information | Legal name, birth date, address, phone | Restricted and purpose-limited |
| Sensitive identifier | Loyalty number, Known Traveler Number | Protected field or reference |
| Highly protected document | Passport number, expiry, document image | Protected-data or Resource boundary |
| Special-category information | Medical, accessibility, dietary or religious information | Explicit purpose and least visibility |

Highly protected values do not belong in ordinary Planning tables, notes,
logs, audit metadata, analytics, URLs, AI prompts, calendar events, push
payloads, or broadly synchronized offline data.

Self-entered, document-extracted, human-reviewed, and supplier-verified values
remain distinguishable. OCR and AI extraction produce proposals only.

## Progressive Collection and Consent

1. Create a lightweight traveler identity.
2. Add ordinary preferences when they improve planning.
3. Request loyalty information only when it can improve an offer or experience.
4. Request legal identity only when preparing an authorized booking handoff.
5. Request passport details only when the itinerary or supplier requires them.
6. Show booking readiness without forcing premature disclosure.
7. Before transmission, show the supplier, purpose, fields, and recipient.

Consent records traveler or guardian authority, supplier or professional,
purpose, exact field allowlist, policy version, time, expiration, and
revocation. Participation, membership, payment, or a prior booking never
implies consent for another disclosure.

## Loyalty Memberships

Loyalty information is structured rather than stored in notes:

- program category and operator identity;
- protected membership number or reference;
- member name;
- tier or status;
- expiration when applicable;
- verification state and last-verified time; and
- traveler consent and visibility policy.

Entering a number does not prove that it is valid, transferable, eligible, or
accepted by a supplier.

## Transportation Preferences

Transportation preference is a typed part of the Travel Profile and may be
overridden for one Adventure. It can describe:

- preferred, acceptable, avoided, and prohibited modes;
- rail, air, cruise, private car, rental car, rideshare, public transit,
  walking, cycling, or another supported mode;
- maximum walking distance and driving duration;
- direct travel versus willingness to connect;
- cabin, seating, or service-class preference;
- overnight-travel and motion-sickness considerations;
- mobility, accessibility, luggage, and equipment constraints;
- cost-versus-convenience and environmental preferences;
- willingness and eligibility to drive; and
- preference strength: required, strongly preferred, preferred, acceptable,
  or avoid.

A reusable preference is a default, not an eternal rule. The Adventure-specific
override is evaluated first for that trip.

## Group Resolution Order

AdventuresSuite does not rank travelers globally. Candidate transportation and
other group choices follow this order:

1. Safety, legal, accessibility, and medical requirements.
2. Hard traveler constraints.
3. Adventure-specific requirements and traveler overrides.
4. Planner-approved group policies and recorded decisions.
5. Strong traveler preferences.
6. Ordinary preferences.
7. Cost, duration, convenience, and sustainability optimization.

An ordinary preference never overrides a hard constraint. A protected
constraint must be resolved through a safe alternative, appropriate traveler or
guardian confirmation, or exclusion of the unsuitable candidate.

## Decision Authority

Decision authority is scoped responsibility, not personal importance:

- **Primary Planner** makes final authorized Planning decisions.
- **Co-Planner** may decide within delegated scope.
- **Traveler** supplies preferences, availability, and advisory votes.
- **Guardian or Delegate** manages approved information for another traveler.
- **Travel Professional** proposes choices within an active engagement.
- **Observer** sees only permitted information and does not decide.

Authority may differ by subject. Transportation authority does not grant
authority over lodging, budgets, protected documents, profiles, or publication.
Votes and scores remain advisory; adoption is a separate validated, audited
Planning mutation.

## Conflict Explanation

The system exposes tradeoffs instead of silently selecting a winner:

> Rail is preferred by two travelers. Air is acceptable to all four travelers
> and satisfies one traveler's mobility constraint. The available rail option
> requires two transfers that do not meet the recorded accessibility need.

The planner can choose another option, split the group, request a poll, or
record a decision. Explanations reveal the minimum necessary information; a
traveler may mark an option unavailable without disclosing a private reason.

## Traveler Sets and Subgroups

Transportation applies to an explicit traveler set, not automatically to every
participant. Scenarios include different origins, arrival dates, cabins,
vehicles, accessibility arrangements, equipment movement, and separate
excursions followed by group reconnection.

Subgroups are Adventure-scoped organization, not tenants or independent
authorization grants. Membership, visibility, and decision policy are explicit
and versioned.

## Planning Evaluation Boundary

```text
Candidate transportation
        ↓
Remove prohibited or infeasible choices
        ↓
Evaluate hard constraints
        ↓
Apply Adventure transportation policy
        ↓
Score scoped traveler preferences
        ↓
Expose conflicts and tradeoffs
        ↓
Authorized Planner decides
```

Preferences inform proposals. They never silently mutate the itinerary, expose
a profile, or book transportation. Recommendations identify satisfied
requirements, relevant conflicts, freshness, assumptions, and decision
authority.

## Required Tests Before Implementation

- profile ownership, guardian/delegate scope, expiry, and revocation;
- Creator, Adventure, traveler, subgroup, and engagement isolation;
- field-level consent and minimum supplier projection;
- no implicit disclosure from participation, voting, or membership;
- Adventure override precedence without modifying reusable defaults;
- hard constraints consistently preceding preference scores;
- deterministic resolution independent of traveler enumeration order;
- equal-authority conflicts remaining visible rather than silently resolved;
- subgroup choices affecting only explicit traveler sets;
- inaccessible or prohibited candidates never winning by aggregate score;
- explanation redaction that does not reveal sensitive reasons; and
- no Planning mutation or booking before an authorized recorded decision.
