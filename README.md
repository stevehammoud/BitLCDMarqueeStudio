# BitLCD Marquee Studio

Generator for BitLCD-ready marquees and related artwork across One saUCE / ALU content.

This project is intentionally separate from Jukebox Download Wizard. Its job is not downloading media. Its job is generating clean BitLCD marquee/artwork assets for existing content.

## Scope

- Jukebox music videos and audio
- Arcade titles
- Console games
- Collections
- Playlists, genres, decades, and curated groups
- Future BitLCD artwork types that need consistent sizing, naming, and rendering

## Initial Targets

- Static full-color artwork
- Animated MP4 artwork
- Matching JPG hold frames for BitLCD playback behavior
- Reusable render templates by content type
- Metadata-driven artwork lookup

## Build

```powershell
cd "C:\Users\steve\Documents\Codex\BitLCDMarqueeStudio"
scripts\build.ps1
dist\BitLCDMarqueeStudio.exe
```

## Public Build

The public build does not include Apple Music artwork lookup, bundled artwork, API credentials, generated cache, saved searches, downloads, or generated marquee output. Users are responsible for using artwork they own or are authorized to use.

## Naming Rule

Generated BitLCD artwork should match the target content filename base and append ` (JUKE)` only when the target BitLCD workflow requires that suffix.

## Status

Early scaffold with live Jukebox resource search.

Current search providers:

- Discogs search for release, master, artist, and label artwork using a local personal token
- MusicBrainz recording metadata
- FanArt.tv artist logo/image/background candidates
- ScreenScraper arcade artwork candidates

Optional provider credentials are read from local files under `resources`. These files are ignored by git.

Discogs lookup reads a personal access token from:

```text
resources\discogs_user_token.txt
```

ScreenScraper lookup reads local credential files from:

```text
resources\screenscraper_devid.txt
resources\screenscraper_devpassword.txt
resources\screenscraper_softname.txt
resources\screenscraper_ssid.txt
resources\screenscraper_sspassword.txt
```

Current Jukebox workflow:

- Enter at least one Jukebox search field.
- Optionally load a Jukebox theme text file using `artist - title - album` lines.
- Pick artwork candidates for fixed L / M / R placement.
- Clear any selected panel artwork when you want to revert that panel.
- Let the app draw the middle title/artist panel when no middle artwork is selected.
- Generate a BitLCD JPG or animated MP4 named from the matching media filename with ` (JUKE)` appended.
