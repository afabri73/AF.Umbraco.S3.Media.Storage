# User Manual

## Purpose

The package allows an Umbraco site to store and serve media through AWS S3 instead of the local filesystem.

## Audience

- Umbraco administrators.
- Editors who upload media in the backoffice.
- Technical teams supporting end users.

## Backoffice Usage

Editors continue to use the Umbraco Media section in the standard way:

1. open the backoffice;
2. go to the Media section;
3. upload or replace a file;
4. save the media item;
5. use the media item in content.

S3 storage is transparent to the user.

## Image Formats

- Valid SVG files are accepted as media.
- Raster formats supported by ImageSharp, for example PNG, JPG, GIF, WebP, BMP, TIFF, and QOI, are validated before storage.
- A corrupted or unrecognized raster image is rejected with a localized message.

## Common Errors

| Message or Symptom | Recommended Action |
|---|---|
| Image upload rejected | Verify the file is not corrupted and try again with a valid file. |
| Upload unavailable | Contact the technical team: there may be S3 connection or permission issues. |
| Media not visible after upload | Refresh the page; if the issue persists, contact the technical team. |

## Support

For recurring issues, provide the technical team with:

- file name;
- file format;
- upload attempt time;
- any message shown by Umbraco.
