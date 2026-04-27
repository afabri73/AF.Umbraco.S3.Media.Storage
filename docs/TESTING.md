# Testing

## Goal

The testing strategy covers three levels:

- package unit tests;
- local Umbraco hosts for `15.x`, `16.x`, and `17.x` compatibility;
- optional smoke endpoints for startup, S3 configuration, and media upload validation.

## Unit Tests

The `src/AF.Umbraco.S3.Media.Storage.Tests` project contains focused xUnit tests for shared package rules.

Command:

```bash
dotnet test src/AF.Umbraco.S3.Media.Storage.Tests/AF.Umbraco.S3.Media.Storage.Tests.csproj
```

### Image Validation Regression

The `ImageSharpValidationFileTypesTests` class verifies that:

- a `.svg` file with `image/svg+xml` or `image/svg` content type is not passed to ImageSharp validation;
- a `.png` file still requires ImageSharp validation;
- invalid content declared as PNG is rejected by ImageSharp.

This coverage matters because the same rule is used in two paths:

- Management API upload validation middleware;
- internal filesystem validation before storage in S3.

## Package Build

Command:

```bash
dotnet build src/AF.Umbraco.S3.Media.Storage/AF.Umbraco.S3.Media.Storage.csproj --no-restore
```

The package targets both `net9.0` and `net10.0`; the build must remain free of errors on both targets.

## Smoke endpoint

For explicitly enabled local or CI checks:

```bash
AF_SMOKE_TESTS=1
```

Available endpoints:

- `GET /smoke/health`
- `POST /smoke/media-upload`

The endpoints are disabled by default and must not be exposed in production without an explicit operational decision.

## Compatibility Hosts

The solution includes these hosts:

- `src/Umbraco.Cms.15.x`
- `src/Umbraco.Cms.16.x`
- `src/Umbraco.Cms.17.x`

Use these hosts to validate Umbraco startup, media upload, `/media` reads, S3 cache behavior, and localized messages.
