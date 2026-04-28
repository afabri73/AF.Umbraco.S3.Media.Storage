# Configuration Reference

## Required settings
Configure the S3 provider settings used by the package in your Umbraco host configuration. No `Program.cs` changes are required.

Typical configuration areas:
- AWS access key / secret key
- AWS region
- Bucket name
- Optional prefix / path settings

### Optional S3 object prefixes
`Umbraco:Storage:AWSS3:Media:MediaBucketPrefix` controls the internal S3 object key prefix used for original media files. It defaults to `media`.

`Umbraco:Storage:AWSS3:Media:CacheBucketPrefix` controls the internal S3 object key prefix used for mirrored media cache and ImageSharp transformed cache files. It defaults to `cache`.

Both values are normalized by trimming leading/trailing slashes, converting backslashes to forward slashes, removing empty path segments, and lowercasing the prefix. Empty values fall back to their defaults.

These settings do not change the public Umbraco media URL path. Public URLs continue to use `Umbraco:CMS:Global:UmbracoMediaPath` (normally `/media`) unless `BucketHostName` is configured for CDN/S3-hosted public URLs.

## Operational recommendations
- Use least-privilege IAM policies for the target bucket.
- Keep production credentials out of source control.
- Validate read/write/list permissions for media and cache keys.

## Smoke endpoints (opt-in)
Enable smoke endpoints for local validation and CI by setting:

```bash
AF_SMOKE_TESTS=1
```

Endpoints:
- `GET /smoke/health`
- `POST /smoke/media-upload`

These endpoints are disabled by default.
