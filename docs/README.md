# Documentation

This index collects the technical and operational documentation for `AF.Umbraco.S3.Media.Storage`.

## Recommended Path

1. [Main README](../README.md): overview, quick installation, and minimal configuration.
2. [Architecture](ARCHITECTURE.md): main components, S3 flow, middleware, and ImageSharp.
3. [Configuration](CONFIGURATION.md): `appsettings` options, AWS credentials, and cache retention.
4. [Development](DEVELOPMENT.md): local build, Umbraco hosts, and maintenance conventions.
5. [Testing](TESTING.md): unit tests, smoke endpoints, and SVG/PNG regressions.
6. [Maintenance](MAINTENANCE.md): operational and release checklists.
7. [API reference](API_REFERENCE.md): main package technical contracts.
8. [Project structure](PROJECT_STRUCTURE.md): folders, responsibilities, and solution projects.
9. [Changelog](CHANGELOG.md): relevant change history.

## Manuals

- [Technical manual](manuals/technical-manual.md): setup, build, test, release, and troubleshooting.
- [User manual](manuals/user-manual.md): package usage from the perspective of Umbraco administrators and editors.

## Consistency Notes

- Documentation must stay aligned with code, tests, and configuration.
- Changes to upload validation, cache behavior, middleware, options, or packaging must update README, technical guides, and changelog.
- Regression tests for SVG and invalid raster images are described in [Testing](TESTING.md).
