# Architecture

## Overview

`AF.Umbraco.S3.Media.Storage` replaces Umbraco Media filesystem operations with an AWS S3-backed implementation and integrates ImageSharp with an S3 cache layer.

Core goals:

- Store media assets in S3.
- Serve `/media/*` directly from S3.
- Cache only supported image files in S3 and cache transformed images for ImageSharp requests.
- Keep upload validation and error reporting user-friendly and localized.

Compatibility validation hosts:

- `src/Umbraco.Cms.15.x` (`net9.0`, `net10.0`)
- `src/Umbraco.Cms.16.x` (`net9.0`, `net10.0`)
- `src/Umbraco.Cms.17.x` (`net10.0`)

## High-level flow

1. Umbraco bootstraps and `AWSS3Composer` registers services automatically.
2. `AddAWSS3MediaFileSystem()` is invoked by the composer (no `Program.cs` changes required).
3. `AWSS3StartupConnectivityHostedService` validates S3 connectivity and blocks boot on failure.
4. `UseAWSS3MediaFileSystem()` is applied by the composer via pipeline filters.
5. Media write/read operations flow through `AWSS3FileSystem`.
6. ImageSharp requests (`?width=...`) use:
   - `AWSS3FileSystemImageProvider` for source resolution.
   - `AWSS3FileSystemImageCache` for cache persistence in S3.
7. Cache key grouping is produced by `AWSS3ScopedCacheHash`.

## Components

### Composition and registration

- `AWSS3Composer`
- `AWSS3StartupConnectivityHostedService`
- `AWSS3FileSystemExtensions`
- `AWSS3MediaFileSystemExtensions`

Responsibilities:

- Bind options from configuration.
- Register AWS SDK clients.
- Apply credentials override from environment variables (`AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY`) when present.
- Validate S3 connectivity at startup and stop application boot on failure.
- Register filesystem, middleware, image provider, and image cache.
- Replace Umbraco media filesystem with S3-backed implementation.
- Keep hosts neutral (no manual `Program.cs` registration required).

### I/O layer

- `IAWSS3FileSystem`
- `IAWSS3FileSystemProvider`
- `AWSS3FileSystemProvider`
- `AWSS3FileSystem`

Responsibilities:

- Resolve virtual paths into S3 object keys.
- Upload/download/delete/list operations.
- Validate image payload before write.
- Cache maintenance on media delete (mirrored cache + transformed cache paths).

### HTTP middleware

- `AWSS3FileSystemMiddleware`
- `AWSS3UploadValidationExceptionMiddleware`

Responsibilities:

- Serve media responses from S3 with range and conditional support.
- Convert malformed-image upload failures into API-friendly localized `400` responses.

### Imaging layer

- `AWSS3FileSystemImageProvider`
- `AWSS3MediaImageResolver`
- `AWSS3FileSystemImageCache`
- `AWSS3ScopedCacheHash`
- `AWSS3ScopedCacheKey` (legacy helper)

Responsibilities:

- Resolve source media streams for ImageSharp.
- Persist transformed images in S3 cache.
- Apply retention cleanup policy.

## Storage layout in S3

- Original media: `media/{path...}`
- Mirrored media cache: `cache/{path...}` (supported images only)
- Image cache: `cache/{path...}`
- For ease of management, the cache folder replicates the media folder hierarchy, ensuring a one-to-one correspondence between each media folder and its cache folder.

## Retention strategy

Retention is opportunistic and runs during cache writes (`SetAsync`):

- If disabled: no automatic cleanup.
- If enabled: cleanup at configured interval.
- If test mode enabled: overrides normal mode and uses fast sweep/max-age values.

## Localization strategy

Upload validation messages use RESX resources with culture fallback:

1. Current UI culture.
2. Neutral resource (`AWSS3FileSystem.resx`, English).

Supported resource cultures:

- `it-IT`
- `en-US`
- `fr-FR`
- `es-ES`
- `de-DE`
- `da-DK`

---
Current documentation: see also `docs/DEVELOPMENT.md` and `docs/PROJECT_STRUCTURE.md`.
