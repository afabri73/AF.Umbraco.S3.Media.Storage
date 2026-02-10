# Configuration Reference

## Required settings
Configure the S3 provider settings used by the package in your Umbraco host configuration. No `Program.cs` changes are required.

Typical configuration areas:
- AWS access key / secret key
- AWS region
- Bucket name
- Optional prefix / path settings

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
