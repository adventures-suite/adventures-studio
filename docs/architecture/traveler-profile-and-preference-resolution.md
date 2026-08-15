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

## Traveler Relationships and Profile Lifecycle

Traveler-related roles remain explicit even when one person fills several:

- authenticated profile owner;
- Adventure participant;
- child or dependent;
- guardian or delegated profile manager;
- booking contact;
- emergency contact;
- planner or decision-maker; and
- person authorizing or providing payment externally.

The design must support travelers without platform accounts, duplicate-profile
detection, safe user-initiated linking, and reviewed merge or unlink behavior.
It must never merge people through name, email, household, booking, or document
similarity alone.

Removing a traveler from an Adventure removes that plan relationship and its
future authorization; it does not silently delete the reusable profile. When a
dependent becomes able to control their own profile, authority transfers
through an explicit, verified, auditable process. Multiple guardians, disputed
authority, profile-owner incapacity, and revoked delegation fail closed rather
than selecting a default controller.

## Identity Correctness

Supplier-facing identity may require more precision than ordinary display:

- legal, preferred, middle, family, and suffix names;
- multiple family names, diacritics, and non-Latin scripts;
- supplier-required transliteration;
- previous names when legitimately required;
- birth date;
- citizenship or residence only for a defined purpose;
- a supplier-required gender marker only when necessary; and
- exact-name comparison with a selected identity document.

AdventuresSuite may warn about a mismatch but never claims that self-entered or
extracted identity is verified. Normalization for search or display never
changes the preserved source value used for supplier review.

## Preference Vocabulary and Scope

Profile data distinguishes:

- **Requirement:** must be satisfied.
- **Constraint:** limits feasible choices.
- **Preference:** improves suitability.
- **Avoidance:** undesirable but potentially acceptable.
- **Prohibition:** must not be selected.
- **Capability:** the traveler can perform an activity such as driving.
- **Eligibility:** the traveler is legally or contractually permitted.
- **Unknown:** the traveler has not answered.
- **Declined:** the traveler intentionally did not disclose the value.

Unknown, declined, no preference, false, and not applicable are different
states. The application must not collapse them into a default boolean.

A preference or constraint may be scoped by Adventure, traveler set, trip
length, travel mode, domestic or international context, daytime or overnight
travel, or another bounded context. It records strength, source, effective
period, last confirmation, verification state, and optional private rationale.
Stale profile values prompt review instead of silently remaining permanent.

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

Balances and redemption authority are outside the initial profile. Future
support must account for supplier groups, alliances, multiple memberships,
household or companion benefits, name mismatches, expiring status, and explicit
permission before transmitting, crediting, transferring, or redeeming anything.

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

The correct result may be that no single option satisfies every requirement.
The evaluation must expose that outcome instead of manufacturing a winner.

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

Planning authority, booking authorization, payment authorization, and supplier
confirmation are distinct. A Primary Planner cannot spend another traveler's
money merely because the planner may approve an itinerary. Future cost-sharing
must explicitly identify who can view prices, set or exceed budgets, authorize
external purchase, provide payment, receive a refund, and view allocations.

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

Group policy must also define unresolved cases: incompatible hard constraints,
planner disagreement, expired decision deadlines, non-response, a late-joining
or departing traveler, a changed requirement after selection, and whether a
material change requires acknowledgment by affected travelers.

## Accessibility and Private Reasons

The profile records the operational accommodation needed rather than requiring
a diagnosis. For example, `step-free boarding required` is preferable to a
medical-condition label.

The private reason, group-visible consequence, supplier disclosure, and
emergency-use projection are separate fields and permissions. Other travelers
may need to know that an option is unsuitable without learning why. Sensitive
information requires purpose-specific consent, least visibility, retention,
revocation, and audit that records the action without copying the value.

## Documents, Readiness, and Authoritative Guidance

Future booking readiness may consider passport expiration, destination-specific
validity windows, visas or travel authorizations, driver eligibility, parental
consent documents, and other requirements. Documents retain supersession,
expiration, source, verification, and review state.

AdventuresSuite provides checklists, freshness, and links to authoritative
sources. It does not present itself as an immigration, medical, insurance, or
legal authority and does not invent requirements when authoritative evidence is
missing.

## Emergency and Offline Access

Travel-day and offline projections contain only the minimum information
explicitly approved for that traveler and device. Membership, participation,
or another traveler's concern does not create emergency access.

Any future emergency or break-glass design requires a separate security,
privacy, legal, product, revocation, offline-removal, lost-device, and audit
review. No implicit break-glass path is approved by this document.

## Data Lifecycle and Traveler Rights

Every field or protected reference defines purpose, owner, authorized readers,
retention, correction, export, deletion, recovery, and supplier-disclosure
behavior. The design distinguishes:

- removing a traveler from one Adventure;
- deleting an Adventure-specific override;
- revoking a guardian, planner, or professional grant;
- deleting or closing a reusable profile;
- preserving minimum required audit evidence; and
- attempting deletion from or correction with an external supplier.

Audit records state that an allowlisted field category was accessed or shared;
they never copy the protected value. Supplier copies and their retention remain
externally governed and visible to the traveler.

## Recommendation Transparency

Planning recommendations explain, without disclosing private reasons:

- why an option was included or excluded;
- which requirements and preferences it satisfies;
- which traveler set was evaluated;
- what information is unknown, stale, or declined;
- what assumptions and external freshness apply;
- which conflicts remain unresolved; and
- who has authority to decide.

No opaque aggregate score may hide an infeasible choice, protected constraint,
or material tradeoff.

## Profile Experience

The profile must feel helpful rather than like mandatory paperwork:

- begin with a lightweight identity and ordinary preferences;
- ask contextual questions only when they improve the current Adventure;
- explain why each sensitive field is requested;
- provide `Not sure`, `Not applicable`, and `Prefer not to say` where valid;
- show reusable values and Adventure overrides together without conflating them;
- use a readiness explanation rather than a punitive completion percentage;
- periodically confirm stale information instead of repeatedly collecting it;
  and
- provide one clear view of who can access which field categories and why.

## Incremental Delivery

### Layer 1: Traveler Identity and Ordinary Preferences

Deliver reusable profile ownership, traveler linkage, progressive collection,
ordinary preference vocabulary, freshness, and privacy controls without highly
protected booking data.

### Layer 2: Group Constraint and Decision Resolution

Deliver Adventure overrides, explicit traveler sets, hard constraints,
subgroups, conflict explanations, polls, scoped planner authority, and recorded
decisions without booking or payment authority.

### Layer 3: Protected Booking Readiness

Only after dedicated security, privacy, legal, retention, Resource, and
commercial review, add legal identity, loyalty identifiers, document
references, verification, and consented minimum supplier projections.

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
- explanation redaction that does not reveal sensitive reasons;
- no Planning mutation or booking before an authorized recorded decision;
- duplicate-profile and merge attempts that cannot join different people;
- dependent authority transfer and multiple-guardian fail-closed behavior;
- unknown, declined, false, and not-applicable values remaining distinct;
- identity normalization that preserves exact supplier-facing source values;
- stale preference and document review without invented validity;
- explicit no-solution results for incompatible hard constraints;
- planning approval that never implies booking or payment authority;
- traveler removal that cannot delete or expose the reusable profile;
- audit, export, correction, deletion, and supplier-copy lifecycle behavior;
- offline revocation and lost-device behavior before protected projections; and
- deterministic, redacted recommendation explanations.
