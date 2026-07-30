# The Simonton Adventures Website Architecture

## 1. Purpose

The Simonton Adventures website is a long-term companion platform for the book series.

The website will support:

* Multiple travel-book volumes
* Destination companion pages
* QR-code links from printed books
* Travel photography
* Historical and cultural information
* Personal travel stories
* Interactive maps
* Travel resources
* Future books and destinations

The architecture must allow new volumes and destinations to be added without creating a new custom Razor page for every location.

---

## 2. Architectural Principles

### 2.1 Destinations are content

A destination such as Venice, Florence, Dubrovnik, Santorini, or the Acropolis should be represented primarily as structured content.

Destination content will initially be stored in JSON files and rendered through reusable Blazor components.

We will not create a separate large Razor page for every destination unless a destination requires a genuinely unique experience.

### 2.2 One reusable destination route

All standard destination pages will be rendered through a shared route:

```text
/volumes/{VolumeSlug}/{CountrySlug}/{DestinationSlug}
```

Examples:

```text
/volumes/italy-greece-croatia/italy/venice
/volumes/italy-greece-croatia/greece/acropolis
/volumes/italy-greece-croatia/croatia/dubrovnik
```

### 2.3 Reusable components over duplicated markup

Common destination sections will be implemented as reusable Razor components, including:

* Hero
* Introduction
* Story sections
* Facts
* Highlights
* Travel tips
* Gallery
* Map
* Continue the Journey
* Previous and next destination navigation

### 2.4 Content storage is abstracted

Pages and components will not directly read JSON files.

They will request content through:

```csharp
ITravelContentService
```

The first implementation will be:

```csharp
JsonTravelContentService
```

This allows the JSON implementation to be replaced later by a database, content-management system, API, or cloud-storage solution without rewriting the page components.

### 2.5 Build only what is currently needed

The project will begin as a single Blazor Web App.

The following will not be added until they solve a real requirement:

* Separate API project
* Database
* Entity Framework
* User accounts
* Administrative portal
* Shopping cart
* Comments
* Newsletter integration
* AI travel assistant
* Full content-management system

---

## 3. Application Structure

The initial solution structure is:

```text
TheSimontonAdventures.sln

src/
└── TheSimontonAdventures.Web/

tests/
└── TheSimontonAdventures.Web.Tests/

docs/
└── architecture/
```

The application will remain a single web project until additional projects are justified.

---

## 4. Recommended Project Folders

```text
src/
└── TheSimontonAdventures.Web/
    │
    ├── Components/
    │   ├── Layout/
    │   ├── Pages/
    │   ├── Destinations/
    │   └── Shared/
    │
    ├── Content/
    │   └── Volumes/
    │
    ├── Models/
    │
    ├── Services/
    │
    ├── wwwroot/
    │   ├── css/
    │   └── images/
    │
    ├── Program.cs
    └── TheSimontonAdventures.Web.csproj
```

### Components/Layout

Contains the site-wide layout and navigation components.

Examples:

```text
MainLayout.razor
NavMenu.razor
Footer.razor
```

### Components/Pages

Contains routable page components.

Examples:

```text
Home.razor
About.razor
Volumes.razor
VolumePage.razor
DestinationPage.razor
NotFound.razor
```

### Components/Destinations

Contains reusable destination presentation components.

Examples:

```text
DestinationHero.razor
DestinationIntroduction.razor
DestinationStory.razor
DestinationFacts.razor
DestinationHighlights.razor
DestinationTips.razor
DestinationGallery.razor
DestinationMap.razor
DestinationNavigation.razor
ContinueTheJourney.razor
```

### Components/Shared

Contains smaller reusable UI components that are not destination-specific.

Examples:

```text
PageHeader.razor
SectionHeading.razor
LoadingState.razor
```

### Content/Volumes

Contains volume manifests and destination JSON files.

Example:

```text
Content/
└── Volumes/
    └── volume-1/
        ├── volume.json
        └── destinations/
            ├── acropolis.json
            ├── dubrovnik.json
            ├── florence.json
            ├── ravenna.json
            ├── santorini.json
            ├── split.json
            └── venice.json
```

### Models

Contains strongly typed content models.

Examples:

```text
Volume.cs
VolumeDestinationReference.cs
Destination.cs
DestinationSection.cs
DestinationFact.cs
DestinationHighlight.cs
DestinationTip.cs
GalleryImage.cs
```

### Services

Contains content-loading and application services.

Examples:

```text
ITravelContentService.cs
JsonTravelContentService.cs
```

### wwwroot

Contains public static assets.

Examples:

```text
wwwroot/
├── css/
│   ├── app.css
│   ├── tokens.css
│   ├── typography.css
│   └── utilities.css
│
└── images/
    └── volumes/
```

---

## 5. Volume Architecture

Each book volume will have one volume manifest.

Example:

```text
Content/Volumes/volume-1/volume.json
```

The volume manifest will include:

* Volume number
* Volume slug
* Title
* Subtitle
* Description
* Cover image
* Published status
* Ordered destination references

Volume 1 is:

```text
Italy, Greece & Croatia
```

Its slug is:

```text
italy-greece-croatia
```

Its page route will be:

```text
/volumes/italy-greece-croatia
```

The volume manifest controls destination order. Destination order will not be hard-coded into Razor components.

---

## 6. Destination Architecture

Each standard destination will have one JSON content file.

Example:

```text
Content/Volumes/volume-1/destinations/acropolis.json
```

A destination may include:

* Volume slug
* Country
* Country slug
* City
* Destination slug
* Title
* Subtitle
* Summary
* Hero image
* Hero image alternative text
* Published status
* Story sections
* Facts
* Highlights
* Travel tips
* Gallery images
* Search and sharing metadata

The shared destination page will receive route parameters, request content from `ITravelContentService`, and compose reusable destination components.

The shared page must not contain Acropolis-specific, Venice-specific, or other destination-specific text.

---

## 7. Routing

### Volume route

```text
/volumes/{VolumeSlug}
```

Example:

```text
/volumes/italy-greece-croatia
```

### Destination route

```text
/volumes/{VolumeSlug}/{CountrySlug}/{DestinationSlug}
```

Example:

```text
/volumes/italy-greece-croatia/greece/acropolis
```

### QR redirect route

Printed QR codes should use stable short routes:

```text
/go/{DestinationSlug}
```

Example:

```text
/go/acropolis
```

The short route will redirect to the canonical destination page.

This allows the canonical page structure to change later without invalidating printed QR codes.

---

## 8. Content Service

All content access will go through:

```csharp
ITravelContentService
```

The interface will support:

```csharp
Task<IReadOnlyList<Volume>> GetVolumesAsync(
    CancellationToken cancellationToken = default);

Task<Volume?> GetVolumeAsync(
    string volumeSlug,
    CancellationToken cancellationToken = default);

Task<Destination?> GetDestinationAsync(
    string volumeSlug,
    string countrySlug,
    string destinationSlug,
    CancellationToken cancellationToken = default);
```

The initial implementation is:

```csharp
JsonTravelContentService
```

The service is responsible for:

* Locating content files
* Deserializing JSON
* Matching volume and destination slugs
* Returning strongly typed models
* Returning `null` when content is not found
* Keeping file-system logic outside the Razor components

---

## 9. CSS Strategy

The website will not use a separate CSS file for every destination.

### Global styles

Global design rules belong under:

```text
wwwroot/css/
```

Recommended files:

```text
tokens.css
typography.css
app.css
utilities.css
```

### Design tokens

Shared values such as colors, spacing, content widths, typography, and border radii will be stored as CSS custom properties.

Example:

```css
:root {
    --color-background: #f7f4ee;
    --color-surface: #ffffff;
    --color-text: #252525;
    --color-muted: #6c6a65;
    --color-accent: #9a6e3a;
    --color-dark: #243238;

    --content-width: 72rem;
    --reading-width: 52rem;
    --space-section: clamp(4rem, 8vw, 7rem);
}
```

### Component-scoped CSS

CSS isolation may be used for substantial reusable components.

Examples:

```text
DestinationHero.razor.css
DestinationGallery.razor.css
DestinationFacts.razor.css
NavMenu.razor.css
```

### Page-specific CSS

Page-specific CSS is allowed only for pages with genuinely unique layouts.

Examples:

```text
Home.razor.css
VolumePage.razor.css
```

Standard destinations should not have files such as:

```text
Acropolis.razor.css
Venice.razor.css
Santorini.razor.css
```

---

## 10. Image Organization

All deployed website images belong under:

```text
wwwroot/images/
```

Images will be organized by volume, country, and destination.

Example:

```text
wwwroot/
└── images/
    └── volumes/
        └── volume-1/
            └── greece/
                └── acropolis/
                    ├── hero.jpg
                    ├── parthenon-wide.jpg
                    ├── caryatids.jpg
                    ├── athens-overlook.jpg
                    └── steve-dianne-acropolis.jpg
```

Image filenames should be descriptive.

Preferred:

```text
parthenon-wide.jpg
caryatids.jpg
athens-overlook.jpg
```

Avoid:

```text
IMG_1847.jpg
DSC00381.jpg
final-final-edit2.jpg
```

Website images should be optimized copies. Full-resolution originals should remain in the private photo archive.

Every meaningful image must have descriptive alternative text.

---

## 11. QR-Code Architecture

QR codes printed in the books must point to permanent short URLs.

Example:

```text
https://thesimontonadventures.com/go/acropolis
```

They must not point to:

* Localhost
* Temporary Azure URLs
* Image files
* Internal numeric IDs
* Version-specific addresses
* Unstable query strings

The short redirect route protects printed QR codes from future website restructuring.

---

## 12. Search and Sharing Metadata

Each destination should eventually support:

* Browser page title
* Meta description
* Canonical URL
* Social-sharing title
* Social-sharing description
* Social-sharing image
* Structured data where useful

This metadata should be stored with the destination content rather than hard-coded into the shared page.

---

## 13. Rendering Strategy

The website should use the simplest rendering mode that satisfies each feature.

Public destination content should initially use static server-side rendering wherever practical.

Interactive rendering should be added only to components that require it, such as:

* Interactive maps
* Advanced galleries
* Search
* Filters
* Trip planners
* User-specific tools

The entire site should not become interactive merely because one component needs interactivity.

---

## 14. Testing Strategy

The initial test project will be:

```text
tests/TheSimontonAdventures.Web.Tests
```

Priority tests include:

* JSON content deserialization
* Volume lookup by slug
* Destination lookup by route
* Duplicate slug detection
* Missing required field detection
* Invalid volume references
* Missing destination files
* Duplicate display-order detection
* Missing image-reference detection
* Route matching

Content validation should prevent malformed content from reaching production unnoticed.

---

## 15. Git Strategy

The repository will use:

```text
main
develop
feature/*
hotfix/*
release/*
```

### main

Contains production-ready code.

### develop

Contains integrated development work intended for the next release.

### feature branches

Used for individual features.

Examples:

```text
feature/destination-engine
feature/acropolis-content
feature/home-page
feature/volume-page
feature/gallery
feature/qr-redirects
```

### Commit format

Use Conventional Commit-style messages.

Examples:

```text
feat: add travel content models
feat: create destination routing engine
feat: add Acropolis destination content
style: implement destination typography
fix: correct destination slug matching
refactor: move content loading behind service
docs: update architecture decisions
test: validate volume manifests
```

---

## 16. Initial Implementation Sequence

The destination engine will be built in this order:

1. Create architecture documentation
2. Add content models
3. Add `ITravelContentService`
4. Implement `JsonTravelContentService`
5. Register the service in dependency injection
6. Add Volume 1 manifest
7. Verify content loading
8. Create the generic destination route
9. Create reusable destination components
10. Add Acropolis content
11. Add Acropolis images
12. Add shared destination styling
13. Add validation tests
14. Merge the completed feature into `develop`

The destination engine should be stable before multiple destination pages are added.

---

## 17. Deferred Capabilities

The architecture should permit these features later, but they are not part of the initial implementation:

* Database-backed content
* Administrative content editor
* Separate web API
* User accounts
* Personalized travel planning
* Newsletter subscriptions
* Comments
* E-commerce
* Book purchasing
* Affiliate travel resources
* Interactive route maps
* Video galleries
* Audio narration
* AI travel companion
* Mobile application

These features will be evaluated only when there is a clear requirement.

---

## 18. Architecture Decision Summary

The current architectural baseline is:

* One Blazor Web App
* One reusable destination route
* JSON-based content initially
* Strongly typed content models
* Content accessed through `ITravelContentService`
* Reusable destination components
* Shared global styles and component-scoped CSS
* No stylesheet per standard destination
* Organized multi-volume content
* Organized multi-volume image storage
* Descriptive canonical URLs
* Permanent short QR redirect routes
* `main`, `develop`, and `feature/*` Git workflow
* Additional infrastructure added only when needed

This document is the source of truth for the technical direction of The Simonton Adventures website.

Any significant architectural change should be documented here before or during implementation.
