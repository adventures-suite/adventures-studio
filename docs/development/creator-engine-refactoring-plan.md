# Creator Engine Refactoring Plan

**Status:** Approved for incremental implementation

**Last Updated:** August 2026

## Objective

Introduce the Creator Engine as AdventuresSuite's tenancy foundation while
preserving the current The Simonton Adventures experience and JSON-driven
content architecture.

This is an incremental refactoring plan. Do not perform a large rewrite or move
all content before the logical ownership boundary is working.

Read these documents before implementation:

- `docs/DECISIONS.md`
- `docs/architecture/creator-engine.md`
- `docs/architecture/multi-tenant-architecture.md`
- `docs/architecture/platform/platform-architecture.md`
- `AGENTS.md`

## Working Rules

- Keep the application deployable after every phase.
- Preserve existing public behavior unless a phase explicitly changes it.
- Add abstractions around working behavior before replacing storage.
- Use strongly typed Creator identity in core engine contracts.
- Do not use ambient Creator Context inside background or storage operations.
- Do not add authentication, a database, or an admin UI in this refactor.
- Do not reorganize all volume and destination files in the first phase.
- Add tests before removing compatibility paths.
- Update architecture documentation when implementation decisions differ from
  this plan.

## Phase 1: Creator Domain Foundation

Implement:

- `CreatorId`
- `Creator`
- `CreatorStatus`
- `CreatorBrand`
- `CreatorFeatures`
- Immutable `CreatorContext`
- One The Simonton Adventures Creator manifest
- JSON serialization tests

Keep `contentRoot` pointing to the existing `Content/Volumes` directory.

Acceptance criteria:

- Creator identity is not derived from slug or domain.
- The flagship Creator manifest loads and validates.
- Public Creator types have XML documentation.
- Invalid and duplicate domains are rejected by validation tests.
- Existing pages continue to work.

## Phase 2: Creator Retrieval and Host Resolution

Implement:

- `ICreatorService`
- JSON-backed Creator service
- `ICreatorResolver`
- Host normalization
- Explicit environment-specific development aliases
- Unknown-host behavior

Acceptance criteria:

- Approved domains resolve case-insensitively.
- Ports are handled correctly for development hosts.
- Unknown production hosts return no Creator.
- Inactive Creators do not resolve publicly.
- Ambiguous or duplicate domain registrations fail validation.
- Unit tests cover valid, invalid, unknown, and inactive resolution.

## Phase 3: Request-Scoped Creator Context

Implement:

- Scoped `ICreatorContextAccessor`
- Creator-resolution middleware
- A safe response for unknown hosts
- Dependency-injection registrations
- Trusted proxy/forwarded-header behavior only if Azure requires it

Place resolution early enough that routing consumers and page rendering can use
the established Creator Context.

Acceptance criteria:

- Creator resolution occurs once per request.
- Context is immutable and request-scoped.
- Concurrent requests cannot overwrite one another's context.
- Unknown production hosts never receive flagship content.
- Development continues to work through explicit local aliases.

## Phase 4: Address Engine Scoping

Change the Address Engine core contract to require `CreatorId`.

Implement creator-scoped:

- Slug resolution
- Route enumeration
- Alias uniqueness
- Publication checks
- QR validation

Update `/go/{slug}` and `/qr/{slug}.{format}` endpoints to use the resolved
Creator Context.

Acceptance criteria:

- Two test Creators can both own `acropolis`.
- Each host resolves its own `acropolis` target.
- Cross-Creator address lookup returns no result.
- QR URLs use the resolved Creator's primary public domain.
- Existing flagship QR routes remain valid.

## Phase 5: Content Service Scoping

Evolve core content operations to require `CreatorId`.

Recommended pattern:

```csharp
Task<Destination?> GetDestinationAsync(
    CreatorId creatorId,
    string volumeSlug,
    string countrySlug,
    string destinationSlug,
    CancellationToken cancellationToken = default);
```

Use an incremental adapter if needed while migrating components. Do not create
parallel JSON-loading implementations.

Acceptance criteria:

- Every core content lookup requires Creator identity.
- The JSON service resolves the Creator's configured content root.
- Cross-Creator content lookup returns no content.
- Existing manifest-order and publication tests remain green.
- A synthetic second Creator proves isolation.
- Missing or malformed content fails predictably and is observable.

## Phase 6: Creator-Driven Presentation

Move shared presentation assumptions into Creator configuration:

- Display name
- Logos and favicon
- Header and footer identity
- SEO defaults
- Copyright
- Brand tokens
- Locale and time zone
- Creator-specific feature availability

Remove hard-coded shared references to:

- The Simonton Adventures brand
- `italy-greece-croatia`
- A global current volume
- Creator-specific public base URLs

Creator-authored editorial content may remain in JSON content files. Do not move
story copy into C# configuration.

Acceptance criteria:

- Shared layout renders from Creator Context.
- Homepage navigation selects content through Creator-scoped services.
- The synthetic second Creator renders distinct brand values.
- No shared component assumes the flagship Creator or adventure.
- Feature configuration cannot bleed between Creator requests.

## Phase 7: Indexing, Caching, and Validation

After creator-scoped behavior is correct, introduce an immutable JSON index or
cache if profiling justifies it.

Requirements:

- Every key begins with Creator identity.
- Duplicate Creator domains fail startup validation.
- Duplicate slugs fail within a Creator.
- The same slug remains valid across different Creators.
- Referenced content and image validation remain Creator-scoped.
- Publication state is honored by all public queries.

Do not add caching before isolation tests exist.

## Phase 8: Optional Physical Content Migration

Only after the preceding phases are stable, consider moving content to:

```text
Content/Creators/{creator}/Adventures/{adventure}/...
```

Perform the move separately from contract refactoring so failures are easy to
diagnose. Preserve canonical public routes and permanent QR addresses.

## Verification Required for Every Phase

Run:

```text
dotnet restore TheSimontonAdventures.slnx
dotnet build TheSimontonAdventures.slnx --configuration Release --no-restore
dotnet test TheSimontonAdventures.slnx --configuration Release --no-build
```

Also verify:

- Existing homepage, volume, and destination routes
- Existing `/go/{slug}` redirects
- SVG and PNG QR generation
- Unknown-host behavior
- Unknown and unpublished content behavior
- The dev GitHub Actions deployment

## Pull Request Strategy

Prefer one reviewable pull request per phase. A phase may be split further when
needed, but do not combine physical content movement, API migration, and UI
rebranding into one change.

Each pull request should include:

- The architectural capability introduced
- Compatibility behavior retained
- Isolation tests added
- Known transitional code
- Follow-up phase
- Deployment verification

## Stop Conditions

Stop and document a decision before proceeding if implementation requires:

- Making Creator scope optional
- Trusting an arbitrary incoming or forwarded host
- Storing the active Creator in singleton mutable state
- Duplicating JSON content loading outside the content service
- Introducing authentication, billing, or a database to complete the phase
- Breaking existing printed QR addresses
- Creating a separate deployment per Creator without a demonstrated need

## Completion

The refactor is complete when Creator identity is mandatory at every ownership
boundary, The Simonton Adventures works through the same multi-tenant path as a
synthetic second Creator, and no shared platform component relies on implicit
flagship assumptions.
