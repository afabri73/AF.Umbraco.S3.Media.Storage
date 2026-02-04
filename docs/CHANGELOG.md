# Changelog

_Last updated: 2026-02-04_

## Unreleased

- Added startup S3 connectivity validation that blocks Umbraco boot on AWS connection failures.
- Added standardized package logging prefix `[AFUS3MS]` with English messages for filtering.
- Added localized user alerts for S3 upload, cache and delete failures.
- Added local configuration override support via optional `appsettings.Local.json` loading in `Program.cs`.
- Sanitized development configuration approach for public repositories (placeholders in `appsettings.Development.json`, real secrets locally only).
- Added AWS credentials precedence behavior:
  - environment variables (`AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY`) override
  - fallback to local shared credentials (`~/.aws/credentials`) and standard SDK chain.
- Kept documentation profile-agnostic using placeholders (for example `YOUR_AWS_PROFILE`).
- Updated release documentation to match manual package flow:
  - build on push
  - manual workflow run to generate `.nupkg`
  - manual upload to NuGet.
- Moved all `AWSS3FileSystem*.resx` localization resources to `Core/`.
- Removed explicit `.csproj` resource mappings now that resources follow the default SDK embedding convention.

## 2.1.0 - 2026-02-02

- Added comprehensive project documentation:
  - `README.md` (expanded usage and configuration guide)
  - `docs/API_REFERENCE.md` (JSDoc-style API reference)
  - `docs/ARCHITECTURE.md`
  - `docs/CONFIGURATION.md`
  - `docs/MAINTENANCE.md`
- Updated NuGet package metadata:
  - improved package title and description
  - added package README embedding
  - added package release notes
  - enabled XML documentation file generation
- Updated Umbraco Marketplace description and title to reflect current capabilities.
- Clarified in technical/project documentation that for ease of management, the S3 `cache` folder replicates the `media` folder hierarchy, ensuring a one-to-one correspondence between each media folder and its cache folder.

## 2.0.0

- This package is a full porting and refactor of `Our.Umbraco.StorageProviders.AWSS3` for Umbraco 17 and .NET 10.
- Project migration to Umbraco 17 / .NET 10.
- S3 media provider integration and middleware alignment.
- ImageSharp provider/cache integration updates.

## 1.x

- Legacy implementation lineage and earlier package versions.

---
Current documentation: see also `docs/DEVELOPMENT.md` and `docs/PROJECT_STRUCTURE.md`.
