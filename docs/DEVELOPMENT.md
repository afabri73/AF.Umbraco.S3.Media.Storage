# Development Guide
_Last updated: 2026-02-05_

## Scope
This guide documents the package project `src/AF.Umbraco.S3.Media.Storage`.
Test hosts are available under:
- `src/Umbraco.Cms.15.x`
- `src/Umbraco.Cms.16.x`
- `src/Umbraco.Cms.17.x`

## Local build
```bash
dotnet build src/AF.Umbraco.S3.Media.Storage/AF.Umbraco.S3.Media.Storage.csproj
```

## Host validation quick checks
- Run host smoke endpoints with `AF_SMOKE_TESTS=1` to validate boot and media upload path.
- Smoke endpoints:
  - `GET /smoke/health`
  - `POST /smoke/media-upload`
- Verified combinations:
  - Umbraco 15 on `.NET 9`
  - Umbraco 16 on `.NET 9`

## Caching behavior
- Images are mirrored into cache using the original image stream.
- Non-image files are not cached in this version.
- Image eligibility uses MIME detection first, then extension fallback.
- `bmp`, `tif`, and `tiff` are excluded from image cache.

## Debugging notes
- If an upload fails during cache write, verify S3 credentials and bucket permissions.
- If a file is uploaded but has no cache asset, ensure it is a supported image format.

## Localization behavior
- User-facing alert messages are localized via `.resx` resources.
- Resource files are stored under `src/AF.Umbraco.S3.Media.Storage/Resources`.
- Neutral/default fallback is `AWSS3FileSystem.resx` (English content).
- If a user culture-specific resource is available (for example `it-IT`), it is used automatically; otherwise the default English resource is used.
- `AF.Umbraco.S3.Media.Storage.csproj` keeps explicit `EmbeddedResource` mappings so manifest names remain `AF.Umbraco.S3.Media.Storage.Core.AWSS3FileSystem*` for runtime compatibility.

## Documentation standards
- XML comments are required for classes, interfaces, methods, functions, and properties across the package codebase.
- XML comments and technical documentation must be written in English.
- Prefer explicit XML comments over `<inheritdoc />` to keep implementation details self-contained for long-term maintenance.

<!-- DOCSYNC:START -->
## Implementation Notes (Code-Aligned)
- User-facing alert messages are localized through `.resx` resources and must remain concise and non-technical.
- Technical logs remain in English and include diagnostic context for troubleshooting (while avoiding noisy temporary culture probes).
- Package logs use the `[AFUS3MS]` prefix for quick filtering in Umbraco logs.
- XML documentation is expected on classes, interfaces, methods, properties, and relevant fields to support long-term maintainability.
- `/// <inheritdoc />` placeholders should be replaced with explicit summaries when maintainability documentation is required.
<!-- DOCSYNC:END -->
