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

The middleware regression suite runs on .NET 9 and .NET 10. It verifies routing for recognized ImageSharp commands, including requests that also contain unrelated parameters, and direct S3 delivery when only parameters such as Umbraco's `v` value remain. Coverage includes dependency-injection activation, the legacy public and protected constructors, complete S3 responses, and commands declared by custom processors. The S3 middleware does not invoke `OnParseCommandsAsync` early; the callback remains exclusively managed by ImageSharp to avoid duplicate invocations. S3 cache registration must also replace Umbraco's default local cache so transformed images remain in S3.

### S3 Failure and Missing Media Regression

The `AWSS3FileSystemS3OperationTests` class verifies that asynchronous AWS failures are observed inside the synchronous filesystem exception boundary. Coverage includes metadata, object reads, list operations, existence checks, awaited deletes, `FileNotFoundException` translation for HTTP 404, and propagation of HTTP 403 authorization failures.

The `AWSS3FileSystemImageProviderTests` class verifies that the package composer runs after Umbraco's ImageSharp composer, the S3 provider remains ahead of the catch-all web-root provider, and the S3 cache/hash replace ImageSharp defaults. It also verifies that the provider performs one metadata request for the resolved S3 key, reuses successful metadata in the image resolver, propagates request cancellation, and does not hide HTTP 401 or 403 responses. Its pipeline regression invokes the real ImageSharp middleware and confirms that a missing S3 source reaches the next middleware as HTTP 404 instead of throwing HTTP 500.

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

The workspace Run and Debug configuration includes Kestrel and IIS Express entries, with matching pre-launch build tasks, for every compatibility host from Umbraco 15.x through 18.x.

| Host | HTTP | HTTPS |
|---|---|---|
| Umbraco 15 | `http://localhost:5015` | `https://localhost:44375` |
| Umbraco 16 | `http://localhost:5016` | `https://localhost:44376` |
| Umbraco 17 | `http://localhost:5017` | `https://localhost:44377` |
| Umbraco 18 | `http://localhost:5018` | `https://localhost:44378` |

For Umbraco 18, set a local `Umbraco:CMS:Imaging:HMACSecretKey` in git-ignored `appsettings.Local.json` before exercising ImageSharp URLs. Release validation must cover an original URL with only `?v=...`, a transformed URL such as `?width=200&v=...`, a missing object, and confirmation that transformed output is written to the configured S3 cache.
