# Adventure Travel Playbook

**Status:** Approved Platform Capability

**Last Updated:** August 9, 2026

## Purpose

The Adventure Travel Playbook is a private, traveler-ready output generated
from an authorized, versioned `AdventurePlan`. It turns structured Planning
state and selected protected Resources into an understandable operational guide
for preparation, travel, and offline use.

The initial reference is `ITALY_MASTER.docx`: a comprehensive travel package
containing an overview, flights, accommodations, day-by-day plans, dining,
transportation, cruise and excursion details, weather and packing guidance,
practical strategy, and supporting tickets and vouchers.

The Playbook is not the Planning source of truth. It is a generated snapshot.

```text
AdventurePlan + verified reservations + selected Resources + output profile
    -> authorized generation
    -> versioned Adventure Travel Playbook
```

## Ownership and Engine Boundaries

- Planning Engine owns authoritative itinerary and operational state.
- Resource Engine owns tickets, vouchers, labels, images, and generated files.
- Rendering/Export infrastructure owns DOCX, PDF, ICS-adjacent links, and mobile
  packaging behind provider-neutral contracts.
- AdventuresCompanion consumes a minimized, encrypted offline projection.
- AI may propose narrative or organization; it never becomes the authority for
  confirmed operational facts.
- Content Engine receives only a separate Creator-approved publication
  transformation. A Playbook is not public content.

Every generation operation carries explicit `CreatorId`, `AdventurePlanId`,
actor, permission, source plan version, output profile, and selected Resource
scope. Authorization is enforced below the UI before data assembly and again
for protected Resource inclusion.

## Output Profiles

The same plan may produce different least-data outputs:

| Profile | Purpose | Default sensitivity |
| --- | --- | --- |
| Creator Master | Complete private operational reference | Highest |
| Traveler Playbook | Information approved for one traveler | Private |
| Companion Offline | Minimized mobile/offline package | Private, encrypted |
| Print Edition | Deliberately selected printable guide | Private |
| Shareable Edition | Redacted plan summary | No operational secrets |
| Memory Edition | Post-travel narrative and approved media | Publication candidate |

Profiles are explicit allowlists, not a sequence of best-effort redactions.
Creating a shareable or memory edition requires a new authorized transformation;
renaming a private file never makes it safe to share.

## Candidate Sections

- cover, travelers, date range, and travel rhythm;
- trip overview and route;
- flights and airport transfers;
- accommodations and check-in guidance;
- daily itinerary in destination-local time;
- dining plans and reservation windows;
- rail, cruise, driving, and local transportation;
- excursions and meeting guidance;
- weather, packing, tasks, and travel-readiness checks;
- luggage shipment and other logistics;
- Creator-approved advice, warnings, and contingency notes; and
- a protected document appendix containing selected Resources.

Sections are omitted when their data is absent or the selected profile does not
permit them. Generation never invents a reservation, confirmation, time, price,
meeting location, validity condition, or traveler assignment.

## Generation Record

Each generated artifact records or references:

- stable generation identity;
- Creator, Adventure Plan, and authorized actor;
- source plan version and generation time in UTC;
- template and output-schema versions;
- output profile and intended traveler audience;
- included section and Resource identities with versions;
- redaction/allowlist policy version;
- format, checksum, size, retention, and expiration;
- generation status and safe failure category; and
- superseded or stale state when authoritative inputs change.

Generated artifacts are immutable. Regeneration creates a new version. The
workspace and Companion clearly identify stale output rather than presenting it
as current.

## Formats and Delivery

Delivery evolves incrementally:

1. deterministic private workspace preview;
2. standards-compliant PDF;
3. DOCX for deliberate Creator-controlled final editing;
4. minimized AdventuresCompanion offline package; and
5. separately approved redacted/shareable and memory editions.

Downloads use authenticated, short-lived delivery and are not stored under
`wwwroot`. Offline packages are encrypted, revocation-aware, retention-bound,
and cleared according to Companion policy. Email attachments and permanent
public URLs are not the default delivery mechanism.

## Protected Documents and Prohibited Data

Tickets, barcodes, QR codes, confirmation references, room or booking PINs,
traveler identities, cabin details, and shipping labels may expose operational
access or personal information. They remain protected Resources and are
excluded unless the actor, profile, traveler audience, and purpose explicitly
permit inclusion.

Playbook content, document images, QR values, reservation references, and
download URLs never enter ordinary logs, traces, metrics, analytics, AI prompts,
support identifiers, or unrestricted audit metadata. Public and shareable
profiles exclude secrets and use secure deep links when protected detail is
needed.

## AI Boundary

AI may propose introductions, summaries, packing guidance, organization, and
traveler-friendly wording. Proposed text is untrusted until reviewed. Confirmed
facts come from authoritative Planning records or verified Resources, retain
source/freshness state where relevant, and cannot be silently replaced by AI
text.

## Audit and Verification

Durable audit records prove generation, sensitive export, delivery, revocation,
and deletion without copying document content or secret values. Required tests
cover Creator and traveler isolation, IDOR, profile allowlists, stale versions,
concurrent regeneration, prohibited-data canaries, protected Resource access,
checksum integrity, expiration, offline clearing, large output bounds, and
rendering/accessibility quality for each supported format.

## Delivery Sequence

- Planning Phase 4 introduces deterministic preview and the first private PDF.
- AI phases may add reviewed narrative proposals without changing authority.
- Resource Engine private storage enables protected appendices and generated
  artifact delivery.
- AdventuresCompanion adds minimized offline access.
- Preserve and Publish creates separately reviewed public or memory editions.
