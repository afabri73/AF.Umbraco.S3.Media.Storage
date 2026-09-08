# API Reference
_Last updated: 2026-09-08_

## Package surface
The package exposes Umbraco-integrated filesystem behavior through its S3 filesystem implementation and registers the required services and middleware automatically.

## Main runtime responsibilities
- Handle stream upload to S3 for media files.
- Resolve and read media streams from S3.
- Map missing S3 objects to filesystem not-found behavior while preserving authorization failures.
- Route original and transformed media requests between the S3 delivery middleware and ImageSharp.
- Create mirrored and transformed caches only for supported image formats.
- Validate supported raster uploads and return localized validation errors.

## Internal behavior notes
- Non-image cache generation is currently disabled.
- Image cache supports common formats except `bmp`, `tif`, and `tiff`.
- `IAWSS3FileSystem` remains synchronous because it implements Umbraco's filesystem contract; AWS task completion and exception translation are contained inside the shared operation boundary.
- `AWSS3FileSystemImageProvider` performs the source-object metadata request and passes the result to `AWSS3MediaImageResolver`, preventing duplicate metadata reads.
- `AWSS3FileSystemMiddleware` passes requests containing recognized ImageSharp commands to the next middleware. Query parameters unrelated to ImageSharp do not stop direct delivery of the original S3 object.
