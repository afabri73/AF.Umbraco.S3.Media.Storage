# Testing

## Goal

The testing strategy covers three levels:

- package unit tests;
- local Umbraco hosts for `15.x`, `16.x`, `17.x`, and `18.x` compatibility;
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

### ImageSharp Delivery and Cache Regression

The middleware regression suite verifies that media requests with ImageSharp commands are passed to ImageSharp processing, while ordinary media requests remain eligible for direct S3 delivery. The S3 cache registration must replace Umbraco's local default cache so transformed images are written to the configured S3 cache.

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
- `src/Umbraco.Cms.18.x`

Use these hosts to validate Umbraco startup, media upload, `/media` reads, ImageSharp resize requests, S3 cache behavior, and localized messages. For standard AWS S3, also verify startup with `AWS:ServiceURL` unset.
