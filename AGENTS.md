# Project-specific instructions

These instructions apply only to the `AF.Umbraco.S3.Media.Storage` repository and all files below this repository root.

## Language policy

This is a public community project. All source-code comments and all project documentation must be written exclusively in English.

This requirement includes:

- inline comments and XML documentation;
- README files and Markdown documentation under `docs/`;
- technical and user manuals;
- changelog entries and release notes;
- NuGet, package, and marketplace descriptive metadata.

When global quality or documentation directives require Italian, this project-specific policy overrides only that language requirement. All other global quality, testing, documentation, versioning, and commit requirements remain applicable.

Localization resources and translated user-facing content are outside this policy and may use their intended target language.

Before completing a change, verify that no new non-English comments or documentation have been introduced.

## Release versioning policy

During issue and pull-request development, keep the current published version unchanged in the changelog, README, package manifests, and marketplace metadata. Record pending public changes under the `Unreleased` changelog section.

Only bump the version when all planned changes are complete and the user explicitly requests preparation of a new release or NuGet package. At that point, move the pending changelog entries into the new version section and align every version-bearing file.
