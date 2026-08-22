# Private GitHub Repo Notes

Recommended repository visibility: private.

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

- Apple private key files
- Apple credential pointer files
- generated artwork
- generated videos
- local input lists with personal curation data
- cache folders

The `.gitignore` is set up to block these by default.
