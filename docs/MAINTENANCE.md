# Maintenance Guide
_Last updated: 2026-02-05_

## Routine checks
- Build the package and verify there are no warnings/errors.
- Validate media upload/read in all test hosts (`Umbraco.Cms.15.x`, `Umbraco.Cms.16.x`, `Umbraco.Cms.17.x`).
- Confirm S3 bucket access and object lifecycle rules.
- Confirm XML documentation is present for classes, methods, and properties.
- Confirm all XML comments and technical documentation remain in English.

## Release checklist
1. Build package.
2. Validate image upload and cache behavior.
3. Verify non-image upload works without cache errors.
4. Run smoke endpoint checks on compatibility hosts:
   - `GET /smoke/health`
   - `POST /smoke/media-upload`
5. Verify `[AFUS3MS]` logs have no package-level `Error/Fatal` events during startup/upload/cache/delete checks.
6. Update documentation date and release notes.

<!-- DOCSYNC:START -->
## Implementation Notes (Code-Aligned)
- User-facing alert messages are localized through `.resx` resources and must remain concise and non-technical.
- Technical logs remain in English and include diagnostic context for troubleshooting (while avoiding noisy temporary culture probes).
- Package logs use the `[AFUS3MS]` prefix for quick filtering in Umbraco logs.
- XML documentation is expected on classes, interfaces, methods, properties, and relevant fields to support long-term maintainability.
- `/// <inheritdoc />` placeholders should be replaced with explicit summaries when maintainability documentation is required.
<!-- DOCSYNC:END -->
