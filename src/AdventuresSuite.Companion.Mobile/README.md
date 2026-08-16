# AdventuresCompanion Mobile

This is the production mobile application boundary for AdventuresCompanion.
It preserves the proven .NET MAUI Blazor Hybrid experience from the isolated
POC while allowing the production shell, platform adapters, client integration,
accessibility, offline behavior, and release process to evolve independently.

The original POC remains under `prototypes/AdventuresSuite.Companion.Poc` as a
reference implementation. New Companion product slices belong here.

## Content providers

The content provider must be selected explicitly. The application never falls
back from API mode to fictional data.

For an internal fictional-data build:

```bash
ADVENTURES_COMPANION_CONTENT_PROVIDER=Demo dotnet run -f net10.0-maccatalyst
```

For API composition, provide a non-credentialed HTTPS origin:

```bash
ADVENTURES_COMPANION_CONTENT_PROVIDER=Api \
ADVENTURES_COMPANION_API_BASE_ADDRESS=https://api.example.invalid/ \
dotnet run -f net10.0-maccatalyst
```

Demo mode contains fictional editorial data only. It is an explicit internal
beta adapter, not a production fallback and not authoritative Planning state.
API mode currently has no mobile sign-in implementation; fail-closed API
responses remain visible until the separately gated PKCE authentication slice
is implemented.

## TestFlight

The manual `Publish AdventuresCompanion TestFlight` workflow builds this
project, validates its bundle identity and build number, retains the signed IPA,
and uploads it to App Store Connect. The existing bundle identifier is retained
so the production transition continues the current TestFlight application.

The protected `testflight` GitHub Environment supplies signing material and an
explicit `Demo` or `Api` provider selection. TestFlight publication does not by
itself activate production data, authentication, synchronization, location,
camera, calendar, or notification capabilities.
