# BitLCD Marquee Studio

Private/personal-use generator for BitLCD-ready marquees and related artwork across One saUCE / ALU content.

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

## Private Apple Music Build

Apple Music artwork support is intended for personal/local use unless licensing is reviewed separately. Credentials, private keys, generated cache, and generated artwork are ignored by git.

## Naming Rule

Generated BitLCD artwork should match the target content filename base and append ` (JUKE)` only when the target BitLCD workflow requires that suffix.

## Status

Early scaffold with live Jukebox resource search.

Current search providers:

- Apple Music catalog search for song/album artwork, music-video stills, artist art, and featured-artist art
- MusicBrainz recording metadata
- FanArt.tv artist logo/image/background candidates

Current Jukebox workflow:

- Enter artist and title search criteria.
- Optionally load a Jukebox theme text file using `artist - title - album` lines.
- Pick artwork candidates for fixed L / M / R placement.
- Clear any selected panel artwork when you want to revert that panel.
- Let the app draw the middle title/artist panel when no middle artwork is selected.
- Generate a BitLCD JPG named from the matching MP4 filename with ` (JUKE)` appended.
