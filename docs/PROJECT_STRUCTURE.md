# Project Structure
_Last updated: 2026-02-05_

This document describes the package structure for `AF.Umbraco.S3.Media.Storage`.

## Rules
- Package source code lives under `src/AF.Umbraco.S3.Media.Storage`.
- `src/Umbraco.Cms.15.x`, `src/Umbraco.Cms.16.x`, and `src/Umbraco.Cms.17.x` are test hosts used for compatibility validation.
- Folders are organized by technical responsibility.
- XML documentation is expected across the package codebase (classes, methods, and properties).

## Main folders
- `Composers/`: Umbraco composition and dependency registration.
- `Core/`: Core S3 filesystem and stream handling logic.
- `Exceptions/`: Custom package exceptions.
- `Extensions/`: Extension methods and glue code.
- `Interfaces/`: Interfaces for abstractions and testing.
- `Middlewares/`: HTTP middleware integration points.
- `Options/`: Configuration option models.
- `Providers/`: External provider integrations.
- `Resources/`: Localized `.resx` resources for user-facing alert messages.
- `Resolvers/`: Runtime resolution and strategy helpers.
- `Services/`: Application services and orchestration logic.

## Key files
- `src/AF.Umbraco.S3.Media.Storage/Core/AWSS3FileSystem.cs`: main S3 filesystem implementation and cache flow.

## Localization resources
- Localized resource files for `AWSS3FileSystem` are centralized in `src/AF.Umbraco.S3.Media.Storage/Resources`.
- Resource embedding is configured in `src/AF.Umbraco.S3.Media.Storage/AF.Umbraco.S3.Media.Storage.csproj` with explicit `EmbeddedResource` mappings to preserve runtime manifest names.

<!-- DOCSYNC:START -->
## Implementation Notes (Code-Aligned)
- User-facing alert messages are localized through `.resx` resources and must remain concise and non-technical.
- Technical logs remain in English and include diagnostic context for troubleshooting (while avoiding noisy temporary culture probes).
- Package logs use the `[AFUS3MS]` prefix for quick filtering in Umbraco logs.
- XML documentation is expected on classes, interfaces, methods, properties, and relevant fields to support long-term maintainability.
- `/// <inheritdoc />` placeholders should be replaced with explicit summaries when maintainability documentation is required.
<!-- DOCSYNC:END -->
