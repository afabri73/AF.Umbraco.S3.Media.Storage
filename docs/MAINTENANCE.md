# Maintenance Guide
_Last updated: 2026-09-08_

## Routine checks

- Build the package and verify there are no warnings/errors.
- Run the unit test project before validating host behavior.
- Validate media upload/read in all test hosts (`Umbraco.Cms.15.x`, `Umbraco.Cms.16.x`, `Umbraco.Cms.17.x`, `Umbraco.Cms.18.x`).
- Confirm S3 bucket access and object lifecycle rules.
- Confirm XML documentation is present for classes, methods, and properties.
- Confirm XML comments and updated technical documentation remain in English.

## Release checklist

1. Build package.
2. Run unit tests:
   - `dotnet test src/AF.Umbraco.S3.Media.Storage.Tests/AF.Umbraco.S3.Media.Storage.Tests.csproj`
3. Validate image upload and cache behavior, including SVG upload and invalid raster rejection.
4. Validate an ImageSharp resize request and confirm the transformed asset is stored under the configured S3 cache prefix rather than `umbraco/Data/TEMP/MediaCache`.
5. Verify non-image upload works without cache errors.
6. Run smoke endpoint checks on compatibility hosts:
   - `GET /smoke/health`
   - `POST /smoke/media-upload`
7. Verify `[AFUS3MS]` logs have no package-level `Error/Fatal` events during startup/upload/cache/delete checks.
8. Confirm standard AWS configurations do not set `AWS:ServiceURL`; retain it only for explicit S3-compatible endpoints.
9. Verify direct delivery with an unrelated query parameter such as `?v=...`, and transformed delivery with a registered command such as `?width=200&v=...`.
10. Verify missing media returns HTTP 404 and authorization failures remain observable.
11. Confirm the Umbraco 18 HMAC secret exists only in git-ignored local configuration.
12. Update the version in the changelog and package project, then align README, NuGet release notes, and Marketplace metadata.
13. Generate the `.nupkg` and inspect its version, dependency ranges, README, assemblies, XML documentation, and release notes.

<!-- DOCSYNC:START -->
## Implementation Notes (Code-Aligned)

- User-facing alert messages are localized through `.resx` resources and must remain concise and non-technical.
- Technical logs remain in English and include diagnostic context for troubleshooting (while avoiding noisy temporary culture probes).
- Package logs use the `[AFUS3MS]` prefix for quick filtering in Umbraco logs.
- XML documentation is expected on classes, interfaces, methods, properties, and relevant fields to support long-term maintainability.
- `/// <inheritdoc />` placeholders should be replaced with explicit summaries when maintainability documentation is required.
- SVG upload acceptance and invalid raster rejection are release-blocking regression checks for upload validation changes.
<!-- DOCSYNC:END -->
