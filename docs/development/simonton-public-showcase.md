# The Simonton Adventures Public Showcase

This showcase presents the real, approved public story of The Simonton
Adventures: its photographs, journeys, destinations, and editorial content. It
is intended for conversations with prospective customers and partners while
the private AdventuresSuite workspace continues to evolve.

## Content boundary

The site uses the existing JSON-driven Content Engine and the public content
owned by Creator `creator_tsa_01`. It does not duplicate or hardcode a second
showcase story. Updates made through the established public-content workflow
therefore remain the source of truth for the showcase.

Only content already intended for public presentation belongs here. Private
Adventure Plans, traveler profiles, reservations, loyalty numbers, protected
Resources, precise locations, authentication state, and unpublished drafts
must never be added to this deployment.

## Hosting boundary

`infrastructure/simonton-showcase/main.bicep` creates an independent Linux App
Service in the isolated `PublicShowcase` environment. It may share the existing
non-production App Service plan to avoid another fixed compute charge, but it
does not share identity, authentication, SQL, Key Vault, or private workspace
configuration with another application.

The environment serves normal public Creator pages. Requests for `/workspace`,
`/authentication`, and the fictional Planner `/showcase` are deliberately
returned as `404 Not Found`. Creator resolution uses only the explicitly
configured showcase host and Creator; production hosts must continue to fail
closed when unknown.

## Presentation and operations

- Present the experiences as the Simonton family's authentic travel story.
- Configure the private development workspace's `Web` navigation destination
  through `WorkspaceNavigation:SimontonAdventuresUrl`; never hardcode the Azure
  hostname in a Razor component. Missing or invalid configuration leaves the
  navigation item unavailable.
- Distinguish published inspiration from future booking or availability
  capabilities.
- Verify the homepage, current journey, destinations, local images, and private
  route denials after every deployment.
- Keep HTTPS-only transport, disabled basic publishing credentials, and no
  Managed Identity unless a reviewed future dependency requires one.
- Remove the App Service when it is no longer needed. Because it shares a plan,
  it also shares that plan's finite CPU and memory.

Before recurring deployment, add a dedicated GitHub Environment and immutable
public-showcase workflow. Do not retarget the normal development deployment or
place credentials in repository configuration.
