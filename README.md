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

## Compatibility

- Umbraco CMS: `15.x`, `16.x`, `17.x`
- .NET: `9.0`, `10.0`
- AWS SDK for .NET: `AWSSDK.S3` + `AWSSDK.Extensions.NETCore.Setup`

## Test hosts and smoke CI

- Local compatibility hosts are included under `src/Umbraco.Cms.15.x`, `src/Umbraco.Cms.16.x`, and `src/Umbraco.Cms.17.x`.
- Each host supports local overrides through `appsettings.Local.json`.

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

- When Umbraco start, the package check AWS connectivity
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

- Original media files: `media/...`
- Mirrored media cache (all media types): `cache/...`
- ImageSharp transformed cache: `cache/...` (under transformed keys)
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

- `docs/API_REFERENCE.md`
- `docs/ARCHITECTURE.md`
- `docs/CONFIGURATION.md`
- `docs/MAINTENANCE.md`
- `docs/CHANGELOG.md`

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

This project is licensed under MIT. See `License.md`.
