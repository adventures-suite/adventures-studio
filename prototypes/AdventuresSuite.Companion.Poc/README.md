# AdventuresCompanion POC

This isolated .NET MAUI Blazor Hybrid project explores the traveler experience
before production mobile implementation begins. It is not part of the
production solution or deployment pipeline.

The POC uses the selected compass-pin concept as the shared AdventuresSuite
master mark, with `Companion` as the product descriptor. Simonton Adventures is
shown separately as the Creator presenting the selected Adventure.

The content provider must be selected explicitly. Set
`ADVENTURES_COMPANION_CONTENT_PROVIDER=Demo` to use the bundled fictional JSON,
or set it to `Api` together with an absolute HTTPS
`ADVENTURES_COMPANION_API_BASE_ADDRESS`. API failures remain visible API states;
the application never falls back to bundled JSON.

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

## TestFlight

The manual `Publish AdventuresCompanion TestFlight` GitHub Actions workflow
builds the iOS application with a unique GitHub run number, retains the signed
IPA, and uploads it to App Store Connect. The workflow uses the protected
`testflight` GitHub Environment and never stores Apple signing material in the
repository.

Required environment variables:

- `APPLE_TEAM_ID`
- `IOS_BUNDLE_ID` (`com.adventuresstudio.companion`)
- `APP_STORE_CONNECT_API_KEY_ID`
- `APP_STORE_CONNECT_ISSUER_ID`

Required environment secrets:

- `APPLE_DISTRIBUTION_CERTIFICATE_BASE64`
- `APPLE_DISTRIBUTION_CERTIFICATE_PASSWORD`
- `APPLE_PROVISIONING_PROFILE_BASE64`
- `APP_STORE_CONNECT_API_PRIVATE_KEY`

Keep the workflow manual until the POC, store disclosures, and tester-release
process have been reviewed. App Store submission is a separate operation.
