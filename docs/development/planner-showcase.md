# Planner Development Showcase

The Planner showcase provides a believable, presentation-ready product story
while private workspace authentication and authorization continue to evolve. It
is a fictional, read-only projection; it is not a shortcut into a Creator
workspace and it never reads or writes Planning persistence.

## Run locally

Start the web application with the Development environment and open
`http://localhost:5018/showcase`:

```shell
dotnet run --project src/TheSimontonAdventures.Web
```

`appsettings.Development.json` enables the route for local development. The
application returns `404 Not Found` when the switch is disabled and refuses to
start if the showcase is enabled outside the Development or isolated Showcase
environment.

## Presentation guidance

- Tell viewers that all travelers and planning details are fictional.
- Use Overview to introduce the Adventure, Itinerary to show day-level planning,
  Travelers to explain preference resolution, and Readiness to show the path to
  departure.
- The left navigation can collapse and the color-theme control demonstrates
  workspace dark mode.
- Treat every recommendation as a proposal awaiting human approval. Nothing in
  the showcase represents live price, availability, a reservation, or a
  booking.

The page includes a permanent development-showcase banner and search-engine
exclusion metadata so screenshots or demonstrations cannot reasonably be
mistaken for customer state or a public offer.

## Maintain the story

The presentation fixture lives in
`src/TheSimontonAdventures.Web/Showcase/Fixtures/adventure.json`. Keep it
fictional, internally consistent, and free of personal or customer data. Image
references must use existing local `/images/` assets. The fixture loader rejects
incomplete stories and non-local image paths.

This route deliberately has no forms, mutation endpoints, database dependency,
authentication bypass, or external provider dependency. The separately hosted
Showcase environment redirects its root to this route and returns `404 Not
Found` for ordinary application routes. It must use synthetic content, an
independent App Service, and no production secrets or persistence settings.

## Azure showcase boundary

`infrastructure/planner-showcase/main.bicep` defines the public showcase as a
separate Linux App Service on the existing non-production plan. It has HTTPS
only, disabled basic publishing credentials, no Managed Identity, no SQL or Key
Vault settings, no External ID configuration, and no production secrets. Its
only permitted dynamic routes are the showcase, Blazor transport, and minimal
health endpoint; required local static assets are allowlisted separately.

The shared plan avoids a second fixed compute charge, but the showcase shares
the plan's finite CPU and memory. Stop or remove the showcase app when it is no
longer needed. Before recurring deployments, add a dedicated GitHub Environment
and immutable showcase deployment workflow; do not retarget the normal
development workflow or its live App Service.
