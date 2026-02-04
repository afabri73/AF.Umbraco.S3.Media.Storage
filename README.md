# AF.Umbraco.S3.Media.Storage
_Last updated: 2026-02-04_

Amazon S3 media storage provider package for Umbraco.

## Purpose
- Store Umbraco media files in Amazon S3.
- Keep media upload and read workflows aligned with Umbraco expectations.
- Cache only supported image formats for now.

## Repository layout
- `src/AF.Umbraco.S3.Media.Storage`: package source code.
- `src/Umbraco.Cms.17.x`: local test host used to validate package behavior.
- `docs`: technical and operational documentation.

## Current caching behavior
- Image files: cache enabled.
- Non-image files: cache disabled in the current version.
- Image detection: MIME-first, extension fallback.
- Excluded from image cache due runtime issues: `bmp`, `tif`, `tiff`.

## Build
```bash
dotnet build src/AF.Umbraco.S3.Media.Storage/AF.Umbraco.S3.Media.Storage.csproj
```

## Documentation
- `docs/DEVELOPMENT.md`
- `docs/CONFIGURATION.md`
- `docs/API_REFERENCE.md`
- `docs/MAINTENANCE.md`
- `docs/PROJECT_STRUCTURE.md`
- Source code XML comments are maintained in English for long-term maintainability.

## License
See `LICENSE`.

## Localization
- Alert messages shown to users are localized by culture.
- Regional resource files (for example `it-IT`, `fr-FR`) are loaded when available.
- Default fallback language is English (`en-US`).

<!-- DOCSYNC:START -->
## Implementation Notes (Code-Aligned)
- User-facing alert messages are localized through `.resx` resources and must remain concise and non-technical.
- Technical logs remain in English and include diagnostic context for troubleshooting (while avoiding noisy temporary culture probes).
- Package logs use the `[AFUS3MS]` prefix for quick filtering in Umbraco logs.
- XML documentation is expected on classes, interfaces, methods, properties, and relevant fields to support long-term maintainability.
- `/// <inheritdoc />` placeholders should be replaced with explicit summaries when maintainability documentation is required.
<!-- DOCSYNC:END -->
