# API Reference
_Last updated: 2026-02-04_

## Package surface
The package exposes Umbraco-integrated filesystem behavior via its S3 filesystem implementation.

## Main runtime responsibilities
- Handle stream upload to S3 for media files.
- Resolve and read media streams from S3.
- Create cache only for supported image formats.

## Internal behavior notes
- Non-image cache generation is currently disabled.
- Image cache supports common formats except `bmp`, `tif`, and `tiff`.
