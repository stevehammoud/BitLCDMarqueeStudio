# Public GitHub Release Notes

The public repository should exclude credentials, generated output, downloaded artwork, saved searches, and private/local provider integrations.

Suggested repo name:

```text
BitLCDMarqueeStudio
```

Before pushing, verify:

```powershell
git status
git ls-files
```

Never commit:

- API credentials or token files
- generated artwork
- generated videos
- local input lists with personal curation data
- cache folders
- downloads

The `.gitignore` is set up to block these by default.
