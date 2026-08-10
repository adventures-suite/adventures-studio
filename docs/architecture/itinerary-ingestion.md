# Itinerary Ingestion and Journey Stop Proposals

**Status:** Approved Platform Requirement and Architecture Direction
**Last Updated:** August 9, 2026

## Purpose

An authorized Creator can upload an image, screenshot, PDF, or pasted text of a
cruise or other travel itinerary. AdventuresSuite extracts the sequence of
places, dates, arrival and departure times, time-zone evidence, sea or travel
days, and source notes into reviewable Journey Stop proposals.

The capability reduces manual entry. It does not treat OCR, AI, a supplier
document, or pasted text as authoritative Planning state.

## Authority Boundary

```text
Protected upload or pasted text
    -> malware/type/size validation
    -> OCR and layout extraction when required
    -> structured itinerary interpretation
    -> place and time-zone resolution proposals
    -> Creator review and correction
    -> approved private Planning mutation
    -> optional later public publication transformation
```

Each arrow is an explicit operation with status, provenance, validation, and
safe failure behavior. No extraction stage may silently create, overwrite,
publish, book, confirm, or cancel travel.

## Terminology and Existing Models

The current public Content Engine has a `JourneyStop` presentation model. A
private uploaded itinerary must not write that record directly.

During ingestion, `JourneyStopProposal` describes a candidate stop extracted
from one source. Approval creates or updates the appropriate private Planning
records, initially `DestinationVisit`, `ItineraryDay`, transportation, and
schedule data. A later Creator-approved publication operation may deliberately
transform selected Planning facts into public Content Engine `JourneyStop`
records.

This naming preserves the user concept—capturing Journey Stops—without erasing
the private Planning and public Content ownership boundary.

## Accepted Inputs

Initial input types may include:

- pasted itinerary text;
- PNG, JPEG, or HEIC screenshots and photographs;
- searchable or scanned PDF itineraries; and
- office documents only after the protected Resource pipeline explicitly
  supports and safely converts them.

Every uploaded file is registered as a protected Resource outside `wwwroot`.
The Resource record retains Creator, uploader, checksum, media type, size,
received time, malware-scan state, extraction state, retention, supersession,
and authorized plan linkage.

Limits for file type, page count, dimensions, size, decompression, OCR time,
concurrency, and retries must be bounded before enabling a format.

## Extracted Journey Stop Proposal

A proposal may contain:

- source-relative sequence and page or text-range evidence;
- raw place label and proposed normalized place identity;
- port, city, region, country, terminal, or sea/travel-day classification;
- local arrival date and time;
- local departure date and time;
- all-day, overnight, embarkation, disembarkation, tender, or sea-day status;
- proposed IANA time-zone identifier and the evidence used to infer it;
- cruise line, ship, voyage, or itinerary label when present;
- explicit versus inferred field state;
- confidence per field, warnings, alternatives, and unresolved ambiguities; and
- source document identity, checksum, extraction method/version, and UTC
  processing time.

Raw source text may be retained only within the protected extraction boundary
and according to the approved retention policy. It must not be copied into
ordinary logs, audit metadata, analytics, or public content.

## Dates, Times, and Time Zones

- Preserve local dates and local times separately until an authoritative
  instant and IANA zone are known.
- Never apply the Creator's home time zone to a cruise stop by default.
- Distinguish an absent time from midnight and an approximate time from an
  exact time.
- Preserve the source's written time and date evidence for review.
- Detect overnight stays, date-line crossings, duplicate local dates, and
  arrival times later than departure times.
- Time-zone inference from a place or coordinates remains a proposal and must
  expose ambiguity, especially near borders, ports, and daylight-saving
  transitions.
- Never invent arrival or departure values merely to complete a record.

## Place Resolution

Place resolution is provider-neutral and may use approved Content, geocoding,
ports, places, or research adapters. It must preserve the raw source label,
candidate identities, provider/source, retrieval time, geographic precision,
confidence, and attribution/licensing requirements.

Unresolved or ambiguous places remain visible for Creator correction. Similar
names, ports serving a different city, terminal changes, and country or region
mismatches must not be resolved by string similarity alone.

Approved stops can feed the layered Planning map, but candidate coordinates and
routes remain visibly proposed until accepted.

## Review Experience

The Creator receives a structured review before mutation:

- source image/text beside the proposed stop sequence;
- field-level highlighting and confidence;
- warnings for missing dates, times, zones, or place ambiguity;
- duplicate and overlap detection against the current plan;
- editable corrections with preserved original evidence;
- select all, per-stop, and per-field acceptance where deterministic;
- an exact before/after Planning preview; and
- a clear distinction among add, update, ignore, duplicate, and conflict.

Approval uses optimistic concurrency. If the plan or source changes, the
proposal becomes stale and must be regenerated or reconciled. Applying approved
changes and required audit intent is atomic.

## AI and Security

OCR output, document text, barcodes, metadata, embedded links, and AI results
are untrusted input. Documents may contain prompt injection, malformed content,
tracking links, hidden layers, misleading labels, or sensitive confirmation
information.

- Use isolated, least-privilege processing with no implicit network or provider
  access.
- Do not follow document links or execute macros, scripts, attachments, or
  embedded instructions.
- Send only the minimum authorized content to an approved extraction provider.
- Define provider retention, training, region, encryption, and deletion terms.
- Never expose ticket codes, booking PINs, payment data, traveler identities,
  or protected-resource URLs in prompts, telemetry, or review URLs.
- Reject unsupported formats and unsafe files without partial Planning writes.

## Audit and Observability

Required audit coverage includes upload registration, extraction request and
outcome, review, correction, approval, rejection, and Planning mutation. Audit
metadata uses opaque resource/proposal identities, safe result categories, and
counts; it does not copy images, source text, confirmation values, or extracted
private content.

Operational metrics may measure bounded processing duration, pages, proposal
counts, confidence bands, corrections, failures, and provider cost without
Creator, traveler, place, route, or document-content dimensions.

## Incremental Delivery

1. Define framework-independent ingestion, source evidence, extracted field,
   Journey Stop proposal, confidence, and review contracts.
2. Add deterministic pasted-text fixtures for cruise stops, sea days,
   overnights, missing times, ambiguous places, and time-zone transitions.
3. Add protected image/PDF upload and Resource registration without extraction.
4. Add deterministic fake OCR and itinerary interpreters with review UI.
5. Add approved OCR/document-analysis and place/time-zone adapters behind
   provider-neutral contracts.
6. Apply accepted proposals transactionally to private Planning records.
7. Integrate accepted stops with map, Playbook, calendar, readiness, and
   Companion projections.

## Definition of Done

- Supported text and image itineraries produce ordered Journey Stop proposals.
- Places, dates, arrival/departure times, and time zones preserve field-level
  source evidence, confidence, and explicit/inferred status.
- Ambiguous, missing, contradictory, and stale values fail visibly and safely.
- No OCR, AI, or provider result mutates Planning without Creator approval.
- Approval is Creator-scoped, concurrency-safe, transactional, and audited.
- Private proposals cannot become public Content Engine Journey Stops without
  explicit publication.
- Malicious documents, prompt injection, duplicate uploads, provider failure,
  cross-Creator access, prohibited data, and retention are tested.
- The workflow is keyboard-accessible and understandable without viewing the
  original image.
