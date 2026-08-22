# Architecture Notes

BitLCD Marquee Studio should be content-type agnostic.

## Content Model

Each job should describe:

- `contentType`: jukebox, arcade, console, collection, playlist, custom
- `sourcePath`: optional local media or ROM path
- `displayName`: visible title/name
- `primaryName`: artist, developer, system, collection, or main credited name
- `secondaryName`: featured artist, publisher, genre, playlist, or subtitle
- `release`: album, game release, collection name, or other disambiguator
- `outputBaseName`: exact filename base to use for generated artwork

## Renderer Model

Renderers should not assume jukebox. They should accept normalized artwork slots:

- left panel
- right panel
- background
- center title/logo
- metadata/story panel
- optional animation timeline

## Jukebox-Specific Rules

Jukebox is one content profile, not the whole app.

- Left panel: album/single artwork
- Right panel preference: featured artist, video still, primary artist, fallback logo
- Center: title and artist word art or artist logo treatment
- Static and animated outputs should share a consistent rest frame

## Global Rules

- Keep BitLCD dimensions explicit per template.
- Preserve Unicode metadata through lookup and rendering.
- Sanitize only filesystem-invalid characters when writing files.
- Keep generated output, cache, and credentials out of git.
