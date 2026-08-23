# UI Flow

## Startup

The user selects the marquee/content type first.

Enabled now:

- Jukebox

Future types:

- Arcade
- System
- Collection
- Custom

## Jukebox Form

Required:

- Artist
- Title

Optional:

- Album / Release
- Featured Artist
- Release Year

Removed by design:

- Apple Music URL / ID
- MBID
- FanArt artist override
- Output filename base

## Canvas

The canvas is always `1920 x 360`.

Panel placement is fixed for the Jukebox marquee flow:

- Left panel
- Center panel
- Right panel

Default Jukebox panel layout:

```text
Left:   x=0,    y=0, w=360,  h=360
Center: x=360,  y=0, w=1200, h=360
Right:  x=1560, y=0, w=360,  h=360
```

## Search Results

Search displays resource candidates as thumbnail tiles.

Current providers:

- Apple Music
- MusicBrainz
- FanArt.tv

Each artwork tile can be assigned as:

- L: left 360 x 360 panel
- M: middle 1200 x 360 panel
- R: right 360 x 360 panel

If no middle artwork is selected, the app draws the title and artist using the built-in Jukebox text style over a generated background.

MusicBrainz metadata-only rows may appear without thumbnails and cannot be placed directly on the canvas.

## Generate

After previewing the canvas, click `Generate Marquee`.

The app prompts for the matching MP4 filename, then writes:

```text
<mp4 filename base> (JUKE).jpg
```

The generated file is saved under the app's local `output\marquees` folder and Explorer opens to the created file.
