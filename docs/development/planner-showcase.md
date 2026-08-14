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
start if the showcase is enabled outside the Development environment.

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
authentication bypass, external provider dependency, or production deployment
path. A future remotely hosted demonstration should use an explicitly approved
demo environment and synthetic Creator rather than relaxing this boundary.
