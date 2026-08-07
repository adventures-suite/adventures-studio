# Destination Authoring

Destination manifests are Creator-owned Content Engine records stored as JSON
during the current implementation phase. New or meaningfully updated
destinations should include an IANA `timeZone` when the destination has a
stable geographic time zone.

## Temporal JSON contract

Date-only travel fields use `YYYY-MM-DD` and deliberately contain no time or
offset:

- `plannedArrivalDate` and `plannedDepartureDate` describe the expected visit.
- `visitedFrom` and `visitedTo` describe when the visit actually occurred.

Both values in a range must be supplied together. A single-day visit repeats
the same date in both properties. Travel dates are formatted using the
Creator's locale for presentation, but they are never converted between time
zones.

Content lifecycle fields use ISO 8601 UTC timestamps ending in `Z`:

- `createdAtUtc` records when the content record was originally authored.
- `updatedAtUtc` records its latest meaningful authored-content change.
- `publishedAtUtc` records its first public publication.
- `lastPublishedAtUtc` records its latest meaningful public publication.

These values are optional authored metadata while JSON remains the storage
implementation. They become system-controlled when database-backed publishing
is introduced. Formatting JSON, building the application, deploying it, or
moving its files must never change these values. Deployment timestamps are
operational metadata and are not content timestamps.

`lastPublishedAtUtc` does not trigger subscriber notifications. Notification
delivery requires a future explicit publication event and policy decision.

## Example

```json
{
  "timeZone": "Europe/Madrid",
  "plannedArrivalDate": "2027-10-25",
  "plannedDepartureDate": "2027-10-29",
  "createdAtUtc": "2026-08-07T18:30:00Z",
  "updatedAtUtc": "2026-08-07T18:30:00Z"
}
```

Do not infer historical travel dates or publication timestamps. Leave an
optional property absent until an authoritative source is available.

## Journey visit and port-call schedules

The Destination date range summarizes the planned stay. Operational timing for
one particular itinerary visit belongs to the Journey segment that reaches the
destination. Author it as a nested `visitSchedule`:

```json
{
  "visitSchedule": {
    "timeZone": "America/St_Thomas",
    "plannedArrivalDate": "2027-05-20",
    "plannedArrivalTime": "07:00:00",
    "plannedGangwayDownTime": "08:00:00",
    "plannedGangwayUpTime": "17:00:00",
    "plannedDepartureDate": "2027-05-20",
    "plannedDepartureTime": "18:00:00"
  }
}
```

Dates are local `YYYY-MM-DD` values and times are local `HH:mm:ss` values. The
IANA time zone identifies the local clock; it does not cause date-only values
to be converted. Gangway times are optional, but down and up must be supplied
together. Leave unknown operational times absent rather than estimating them.

The schedule belongs to the Journey because another Adventure can visit the
same Destination on different dates and at different times. Existing authored
Journey date strings remain supported as a migration fallback.
