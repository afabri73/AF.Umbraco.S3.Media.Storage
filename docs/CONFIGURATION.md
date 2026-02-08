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
