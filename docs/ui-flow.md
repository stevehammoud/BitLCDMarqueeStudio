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

Only panel placement is editable:

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

Each artwork tile can currently be assigned as:

- Left panel
- Right panel
- Background

MusicBrainz metadata-only rows may appear without thumbnails.
