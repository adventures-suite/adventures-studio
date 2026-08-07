# AdventuresSuite Resource Engine

## Foundation scope

The Resource Engine establishes `CreatorId` as the ownership boundary for reusable media. A stable `ResourceId` identifies each resource independently of filenames, URLs, storage vendors, or later processing decisions. Resource lookups always require both identities, so an identifier reused by two Creators remains isolated.

The first provider intentionally wraps files already in `wwwroot`. It validates that a registered path is safe, exists, and is published, then returns the current root-relative URL. Creator homepage and About heroes, favicons, optional logos, Adventure covers, destination hero/card images, galleries, and story-section images now use resource references.

## Resource records

A record contains identity, Creator ownership, media type, descriptive metadata, alternative text, attribution, copyright, usage rights, publication state, and a provider-specific storage location. Startup validation rejects duplicate identities, mismatched ownership, missing accessibility or rights metadata, unknown providers, unsafe paths, missing files, and draft resources placed in shared public storage.

Migrated presentation components resolve the public URL and resource record together. Image alternative text and media type are therefore authoritative Resource Engine metadata, while captions remain editorial content owned by the surrounding story. Startup validation also requires the declared media type to match the storage file extension.

Creator and travel-content manifests store resource identities rather than public URLs or duplicated alternative text. Public presentation URLs can only be obtained through the Creator-scoped Resource Engine.

## Public storage limitation

`wwwroot` remains shared and inherently public. It cannot enforce Creator authorization and must never contain private, protected, embargoed, or draft media. Resource ownership in this phase is a validated logical boundary; it does not turn static files into access-controlled objects.

Future providers may use Azure Blob Storage and CDN delivery without changing `ResourceId` or content ownership. Private delivery, image processing, dimensions, content-reference migration, and storage migration should be added incrementally behind the provider and service abstractions.
