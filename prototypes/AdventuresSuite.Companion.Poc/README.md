# AdventuresCompanion POC

This isolated .NET MAUI Blazor Hybrid project explores the traveler experience
before production mobile implementation begins. It is not part of the
production solution or deployment pipeline.

The POC uses the selected compass-pin concept as the shared AdventuresSuite
master mark, with `Companion` as the product descriptor. Simonton Adventures is
shown separately as the Creator presenting the selected Adventure.

The content provider must be selected explicitly. No mode is the default.
API failures remain visible API states; the application never falls back to
bundled JSON.

## Provider configuration

Local development can use process environment variables:

```bash
ADVENTURES_COMPANION_CONTENT_PROVIDER=Demo dotnet run -f net10.0-maccatalyst
```

API mode additionally requires an absolute, non-credentialed HTTPS origin:

```bash
ADVENTURES_COMPANION_CONTENT_PROVIDER=Api \
ADVENTURES_COMPANION_API_BASE_ADDRESS=https://api.example.invalid/ \
dotnet run -f net10.0-maccatalyst
```

Installed applications use non-secret assembly metadata supplied at package
time. CI and Android package builds pass the same values as MSBuild properties:

```bash
dotnet publish AdventuresSuite.Companion.Poc.csproj \
  -f net10.0-android -c Release \
  -p:CompanionContentProvider=Api \
  -p:CompanionApiBaseAddress=https://api.example.invalid/
```

For a deliberate packaged demo, pass only
`-p:CompanionContentProvider=Demo`. Do not pass tokens, credentials, signed
URLs, or other secrets through either property. The API origin is public
application configuration and is supplied by the build environment rather than
committed to source.

The TestFlight workflow reads `COMPANION_CONTENT_PROVIDER` and optional
`COMPANION_API_BASE_ADDRESS` GitHub Environment variables, validates them, and
passes them into the signed IPA. Other CI jobs use the same `-p:` properties.

## What it demonstrates

- mobile-first navigation and visual direction;
- an Adventure switcher with one Current and two Planned Adventures;
- Creator-owned hero photography resolved from the existing Resource catalog;
- a derived Adventure countdown;
- Today and Next;
- a route timeline built from existing Volume 3 JSON;
- explainable readiness;
- a privacy-minimized Italy Travel Playbook derived from `ITALY_MASTER.docx`;
- offline, calendar, document, memory, and breadcrumb interaction concepts.

## Deliberate limitations

The POC has no authentication, authorization, API, SQL, encrypted storage,
calendar write, notification, camera, or location implementation. Buttons for
those capabilities show safe explanatory prototype feedback only. The bundled
JSON is public editorial content and does not model production private Planning
data.

## Run on Mac Catalyst

Install and select full Xcode, then run:

```bash
dotnet build -f net10.0-maccatalyst
dotnet run -f net10.0-maccatalyst
```

Android and iOS can be selected through the normal MAUI tooling after their
respective simulator/emulator prerequisites are configured.

## TestFlight transition

This POC is preserved as a reference and is no longer the TestFlight build
target. The manual `Publish AdventuresCompanion TestFlight` workflow now builds
`src/AdventuresSuite.Companion.Mobile`, which carries the proven experience
forward in the production mobile boundary while retaining the existing bundle
identifier and protected signing process.

Required environment variables:

- `APPLE_TEAM_ID`
- `IOS_BUNDLE_ID` (`com.adventuresstudio.companion`)
- `APP_STORE_CONNECT_API_KEY_ID`
- `APP_STORE_CONNECT_ISSUER_ID`
- `COMPANION_CONTENT_PROVIDER` (`Demo` or `Api`)
- `COMPANION_API_BASE_ADDRESS` (required only for `Api`)

Required environment secrets:

- `APPLE_DISTRIBUTION_CERTIFICATE_BASE64`
- `APPLE_DISTRIBUTION_CERTIFICATE_PASSWORD`
- `APPLE_PROVISIONING_PROFILE_BASE64`
- `APP_STORE_CONNECT_API_PRIVATE_KEY`

Keep the workflow manual until the production mobile shell, store disclosures,
and tester-release process have been reviewed. App Store submission is a
separate operation.
