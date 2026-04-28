# AF.Umbraco.S3.Media.Storage

AWS S3 media storage provider for Umbraco 15/16/17 on .NET 9/10.

This package replaces the default media file system with an S3-backed implementation and includes:

- S3-backed implementation of Umbraco `IFileSystem` for Media.
- Middleware for direct media delivery from S3 under `/media`.
- ImageSharp integration for dynamic thumbnails.
- S3 cache for all media files (`cache/` mirror) plus ImageSharp transformed images.
- Localized server-side validation for malformed image uploads.
- Startup S3 connectivity check that blocks app boot on connection failure.
- Optional package-hosted smoke endpoints (opt-in via `AF_SMOKE_TESTS=1`).
- Optional cache-retention cleanup with normal and test modes.
- Cache folder structure that mirrors the media folder structure.

## Credits

This project is a porting of `Our.Umbraco.StorageProviders.AWSS3`  
([adam-werner/Our.Umbraco.StorageProviders.AWSS3](https://github.com/adam-werner/Our.Umbraco.StorageProviders.AWSS3)),
which is not compatible with recent Umbraco versions.

`AF.Umbraco.S3.Media.Storage` was fully refactored to be compatible with modern Umbraco versions and then further optimized and extended.

Thanks to community contributors:

- [koty10](https://github.com/koty10) for the SVG upload-validation fix in [PR #3](https://github.com/afabri73/AF.Umbraco.S3.Media.Storage/pull/3).
- [proxicode](https://github.com/proxicode) for the configurable bucket-prefix contribution and related integration fixes in [PR #4](https://github.com/afabri73/AF.Umbraco.S3.Media.Storage/pull/4).

## Compatibility

- Current package version: `1.3.0`
- Umbraco CMS: `15.x`, `16.x`, `17.x`
- .NET: `9.0`, `10.0`
- AWS SDK for .NET: `AWSSDK.S3` + `AWSSDK.Extensions.NETCore.Setup`

## Current Release

`1.3.0` adds configurable S3 `MediaBucketPrefix` and `CacheBucketPrefix` support, keeps local public media URLs on Umbraco's media path unless `BucketHostName` is configured, normalizes configured prefixes, and adds regression tests/documentation for the new behavior.

Thanks to [proxicode](https://github.com/proxicode) for the configurable bucket-prefix contribution and related integration fixes in [PR #4](https://github.com/afabri73/AF.Umbraco.S3.Media.Storage/pull/4), and to [koty10](https://github.com/koty10) for the SVG upload-validation fix in [PR #3](https://github.com/afabri73/AF.Umbraco.S3.Media.Storage/pull/3).

## Test hosts and smoke CI

- Local compatibility hosts are included under `src/Umbraco.Cms.15.x`, `src/Umbraco.Cms.16.x`, and `src/Umbraco.Cms.17.x`.
- Each host supports local overrides through `appsettings.Local.json`.

## Build and test

Build the package:

```bash
dotnet build src/AF.Umbraco.S3.Media.Storage/AF.Umbraco.S3.Media.Storage.csproj --no-restore
```

Run unit tests:

```bash
dotnet test src/AF.Umbraco.S3.Media.Storage.Tests/AF.Umbraco.S3.Media.Storage.Tests.csproj
```

The unit test suite includes a regression for SVG uploads being accepted and invalid PNG content still being rejected by ImageSharp validation.

## Installation

Install from NuGet:

```bash
dotnet add package AF.Umbraco.S3.Media.Storage
```

## Basic setup

### 1) No `Program.cs` changes required

The package wires services and middleware automatically via a composer.
You only need to configure `appsettings*.json`.

### 2) Configure `appsettings*.json`

Minimal setup:

```json
{
  "Umbraco": {
    "Storage": {
      "AWSS3": {
        "Media": {
          "BucketName": "your-media-bucket",
          "Region": "eu-west-1"
        }
      }
    }
  }
}
```

Optional S3 object prefixes:

```json
{
  "Umbraco": {
    "Storage": {
      "AWSS3": {
        "Media": {
          "BucketName": "your-media-bucket",
          "Region": "eu-west-1",
          "MediaBucketPrefix": "tenant-a/media",
          "CacheBucketPrefix": "tenant-a/cache"
        }
      }
    }
  }
}
```

`MediaBucketPrefix` controls the internal S3 object key prefix for original media files. `CacheBucketPrefix` controls mirrored media cache and ImageSharp transformed cache keys. Public media URLs still use Umbraco's configured media path, normally `/media`, unless `BucketHostName` is configured for CDN/S3 public URLs.

For public/open-source repositories, keep placeholders in `appsettings.Development.json` and store real local values in `appsettings.Local.json` (git-ignored).

Optional AWS section (local/non-IAM environments):

```json
{
  "AWS": {
    "Profile": "YOUR_AWS_PROFILE",
    "Region": "eu-west-1",
    "ServiceURL": "http://localhost:9000",
    "ForcePathStyle": true
  }
}
```

Why two sections:

- `Umbraco:Storage:AWSS3:Media` is package/provider configuration (`BucketName`, retention, media behavior).
- `AWS` is AWS SDK client configuration (`Profile`, `ServiceURL`, `ForcePathStyle`, default `Region`).

In short: `Storage` defines what the provider does, `AWS` defines how the SDK connects.

### 3) AWS secrets for local development

Keep AWS secrets out of the repository.

Use local shared credentials on your machine (for example `~/.aws/credentials`):

```ini
[YOUR_AWS_PROFILE]
aws_access_key_id = YOUR_ACCESS_KEY_ID
aws_secret_access_key = YOUR_SECRET_ACCESS_KEY
```

Credential precedence used by this project:

1. `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` (for example set only in local `launchSettings.json`)
2. `~/.aws/credentials` using the profile configured in `AWS:Profile` (for example `YOUR_AWS_PROFILE`)
3. Other standard AWS SDK providers (for example IAM role on cloud hosts)

## Cache retention

Thumbnail cache retention is configurable via:

```json
{
  "Umbraco": {
    "Storage": {
      "AWSS3": {
        "Media": {
          "CacheRetention": {
            "Enabled": false,
            "NumberOfDays": 90,
            "TestModeEnable": false,
            "TestModeSweepSeconds": 30,
            "TestModeMaxAgeMinutes": 10
          }
        }
      }
    }
  }
}
```

Rules:

- `TestModeEnable = true` overrides `Enabled`.
- In normal mode, cleanup uses `NumberOfDays`.
- In test mode, cleanup sweep/max-age are controlled in seconds/minutes.

## Setup validation

- When Umbraco starts, the package checks AWS connectivity.
- If AWS connectivity is invalid, startup may be blocked by the package startup validation.

## Smoke endpoints (opt-in)

For local validation and CI checks you can enable built-in smoke endpoints by setting:

```bash
AF_SMOKE_TESTS=1
```

Endpoints:

- `GET /smoke/health`
- `POST /smoke/media-upload`

These endpoints are disabled by default.

## Logging and alerts

- Package logs are emitted in English and include the `[AFUS3MS]` prefix for easy filtering.
- Startup connectivity failures are logged as critical and block Umbraco startup.
- Upload/cache/delete storage failures are logged with the same prefix and returned to users with localized alert messages.

## S3 object layout

- Original media files: `media/...` by default, or `{MediaBucketPrefix}/...` when configured.
- Mirrored media cache (all media types): `cache/...` by default, or `{CacheBucketPrefix}/...` when configured.
- ImageSharp transformed cache: `cache/...` by default, or `{CacheBucketPrefix}/...` when configured.
- For ease of management, the `cache` folder replicates the `media` folder hierarchy, ensuring a one-to-one correspondence between each media folder and its cache folder.

## Localization for validation errors

The package includes localized messages for:

- `it-IT`
- `en-US` (default fallback)
- `fr-FR`
- `es-ES`
- `de-DE`
- `da-DK`

If a specific culture resource is missing, the package falls back to `en-US`.

## Project documentation

For full technical documentation:

- `docs/README.md`
- `docs/API_REFERENCE.md`
- `docs/ARCHITECTURE.md`
- `docs/CONFIGURATION.md`
- `docs/DEVELOPMENT.md`
- `docs/TESTING.md`
- `docs/MAINTENANCE.md`
- `docs/PROJECT_STRUCTURE.md`
- `docs/CHANGELOG.md`
- `docs/manuals/technical-manual.md`
- `docs/manuals/user-manual.md`

## Security checks

This repository runs automated secret scanning in GitHub Actions via Gitleaks (`.github/workflows/secret-scan.yml`).

## Security advisory notice

This package does not introduce the known Umbraco advisory `GHSA-69cg-w8vm-h229`, but it can be installed on Umbraco versions that may still include it.  
For production usage, always install the latest patched Umbraco release in your major/minor line.

## Attribution request (non-binding)

If you fork or modify this project, please consider adding credits to:

- Project: `AF.Umbraco.S3.Media.Storage`
- Author: `Adriano Fabri`
- Url: `https://github.com/afabri73/AF.Umbraco.S3.Media.Storage`

## License

This project is licensed under MIT. See `LICENSE`.
