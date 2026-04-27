# Technical Manual

## Purpose

This manual describes the operational activities required to configure, test, release, and maintain `AF.Umbraco.S3.Media.Storage`.

## Requirements

- .NET SDK compatible with the package targets: `net9.0` and `net10.0`.
- Umbraco CMS `15.x`, `16.x`, or `17.x`.
- An AWS S3 bucket or a compatible local service, for example MinIO.
- AWS credentials configured through environment variables, a local profile, or the standard AWS SDK provider chain.

## Local Setup

1. Clone the repository.
2. Configure one host under `src/Umbraco.Cms.*.x`.
3. Store real local values only in `appsettings.Local.json`, which must stay out of version control.
4. Verify `BucketName`, `Region`, `ServiceURL`, and `ForcePathStyle` when using MinIO.

## Build

```bash
dotnet build src/AF.Umbraco.S3.Media.Storage/AF.Umbraco.S3.Media.Storage.csproj --no-restore
```

## Test

```bash
dotnet test src/AF.Umbraco.S3.Media.Storage.Tests/AF.Umbraco.S3.Media.Storage.Tests.csproj
```

The unit tests include regressions for accepted SVG uploads and rejected invalid PNG content.

## Smoke Test

Enable smoke endpoints only for development or CI:

```bash
AF_SMOKE_TESTS=1
```

Verify:

- `GET /smoke/health`;
- `POST /smoke/media-upload`;
- no package-level errors in logs filtered by the `[AFUS3MS]` prefix.

## Release

1. Run build and tests.
2. Validate at least one Umbraco host with a real media upload.
3. Update version, release notes, and changelog.
4. Generate the NuGet package.
5. Publish the release and tag.

## Troubleshooting

- SVG upload rejected: verify the installed version includes the `ImageSharpValidationFileTypes` rule.
- Corrupted PNG/JPG files accepted: verify the write path goes through ImageSharp validation and unit tests are executed.
- S3 errors during startup: check credentials, bucket, region, and permissions.
- Media uploaded but cache missing: verify image format, MIME detection, and write permissions on the `cache/` prefix.
