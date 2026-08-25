# Changelog

## Unreleased
- Fixed direct S3 delivery for original media URLs containing unrelated query parameters, such as Umbraco's cache-busting `v` parameter, while continuing to route recognized processing commands to ImageSharp.

## 1.4.1 - 2026-08-23
- Updated README and Umbraco Marketplace contributor credits to acknowledge [suedeapple](https://github.com/suedeapple) for the Umbraco 18 compatibility, ImageSharp request-handling, and S3 cache integration fixes contributed in [PR #7](https://github.com/afabri73/AF.Umbraco.S3.Media.Storage/pull/7).

## 1.4.0 - 2026-08-23
- Added Umbraco 18.x compatibility by widening the `Umbraco.Cms.Web.Common` package reference range to `[15.0.0,19.0.0)`.
- Fixed a runtime `MissingMethodException` on `UmbracoPipelineFilter..ctor` under Umbraco 18: the composer previously called the 6-parameter constructor (`name` + 5 `Action<IApplicationBuilder>`), which Umbraco 18 replaced with an 8-parameter constructor that also adds `Action<IEndpointRouteBuilder>` stages. `AWSS3Composer` now uses the single-argument constructor plus object-initializer property assignment (`PrePipeline = ...`), which is stable across Umbraco 15-18.
- Added a dedicated `Umbraco.Cms.18.x` local test host (targets `net10.0` only, matching Umbraco 18's own framework support).
- Verified via `dotnet run` against all four hosts (15.x-18.x): each now boots through hosted-service startup and only fails at the (expected) placeholder S3 connectivity check, with no constructor-binding errors.
- Updated README, package metadata, and Marketplace description/tags to include Umbraco 18.
- Fixed `AWSS3FileSystemMiddleware` ignoring ImageSharp resize commands (`?width=`/`?height=`/crop): requests with processing commands now continue to ImageSharp instead of always returning the original S3 object.
- Fixed `IImageCache` registration so `AWSS3FileSystemImageCache` replaces Umbraco's local default cache and transformed images are stored in S3.
- Removed the placeholder `AWS:ServiceURL` override from development sample configurations. Standard AWS S3 now relies on the configured region; custom endpoint configuration remains documented for MinIO and other S3-compatible services.
- Updated `AWSSDK.S3` to `4.0.101.4`; `AWSSDK.Extensions.NETCore.Setup` remains at `4.0.3.22`.

## 1.3.0 - 2026-04-28
- Added configurable S3 `MediaBucketPrefix` and `CacheBucketPrefix` support while keeping public media URLs based on Umbraco's media path unless `BucketHostName` is configured.
- Normalized configured media/cache bucket prefixes to avoid malformed keys and cross-prefix collisions.
- Fixed public URL generation so custom media bucket prefixes do not leak into local Umbraco media URLs unless `BucketHostName` is configured.
- Added configurable cache-prefix handling for mirrored media cache and ImageSharp transformed cache files.
- Updated package metadata, README, Marketplace description, and release notes for the new prefix support.
- Thanks to [proxicode](https://github.com/proxicode) for the configurable bucket-prefix contribution and related integration fixes in [PR #4](https://github.com/afabri73/AF.Umbraco.S3.Media.Storage/pull/4).
- Thanks to [koty10](https://github.com/koty10) for the SVG upload-validation fix in [PR #3](https://github.com/afabri73/AF.Umbraco.S3.Media.Storage/pull/3).
- Added xUnit regression tests for ImageSharp upload validation rules:
  - SVG files are not passed to ImageSharp validation;
  - invalid PNG content still requires ImageSharp validation and is rejected.
- Added testing documentation and manuals for SVG/raster upload validation.

## 1.2.1 - 2026-02-10
- Rebuilt package to include composer auto-registration and smoke endpoints in the shipped DLLs.
- Documentation and marketplace metadata updated to reflect the changes.

## 1.2.0 - 2026-02-08
- Removed Program.cs requirements by moving registration to the package composer.
- Added package-hosted smoke endpoints (opt-in via AF_SMOKE_TESTS=1).
- Updated docs/metadata to reflect automatic composition.

## 1.1.0 - 2026-02-05

- Added multi-target support: `net9.0` and `net10.0`.
- Updated package version to align with cross-version Umbraco support (15/16/17).
- Added security advisory note in docs: users should run patched Umbraco versions for known platform advisories.
- Added dedicated Umbraco test hosts: `Umbraco.Cms.15.x` and `Umbraco.Cms.16.x`, aligned with local config overrides (`appsettings.Local.json`).
- Updated host package references to latest validated patch lines:
  - Umbraco 15 host: `Umbraco.Cms` `15.4.4`
  - Umbraco 16 host: `Umbraco.Cms` `16.4.1`
- Fixed smoke endpoint service resolution and validated smoke endpoints on `.NET 9` for Umbraco 15 and 16 (`/smoke/health`, `/smoke/media-upload` => `exists:true`).
- Completed log validation with `[AFUS3MS]` filter on test hosts without package-level `Error/Fatal` events during startup/upload/cache/delete checks.

## [1.0.0] - 2026-02-05
- Aligned release metadata and documentation with the current package naming.
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
- Moved all `AWSS3FileSystem*.resx` localization resources to `Resources/`.
- Added explicit `.csproj` resource mappings to preserve manifest names used by runtime localization lookup.
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
- Project migration to Umbraco 17 / .NET 10.
- S3 media provider integration and middleware alignment.
- ImageSharp provider/cache integration updates.
- Legacy implementation lineage and earlier package versions.
- This package is a full porting and refactor of `Our.Umbraco.StorageProviders.AWSS3`.

---
Current documentation: see also `docs/DEVELOPMENT.md` and `docs/PROJECT_STRUCTURE.md`.
