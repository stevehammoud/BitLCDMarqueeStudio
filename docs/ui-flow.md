# UI Flow

## Startup

The user selects the marquee/content type first.

Enabled now:

- Jukebox
- Arcade

Future types:

- System
- Collection
- Custom

## Jukebox Form

At least one Jukebox search field is required.

Search fields:

- Artist
- Title
- Album / Release
- Featured Artist
- Release Year

The app can also load a Jukebox theme text file where each non-empty line is one of these formats:

```text
artist - title
artist - title - album
```

Selecting an entry fills Artist, Title, and Album / Release when album is provided. The separator is ` - ` with spaces, so artist names like `A-Ha` are safe.

Removed by design:

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

Search keeps all returned resource candidates internally, then displays them as rows filtered by:

- Source
- Type

Current providers:

- Discogs
- MusicBrainz
- FanArt.tv
- ScreenScraper

Discogs returns release, master, artist, and label image candidates when local credentials are available.

Each artwork row can be assigned as:

- L: left 360 x 360 panel
- M: middle 1200 x 360 panel
- R: right 360 x 360 panel

Each selected panel can also be cleared. If no middle artwork is selected, the app draws the title and artist using the built-in Jukebox text style over the selected background.

Backgrounds are local only. The app can use built-in gallery backgrounds, images placed under `assets\backgrounds`, user-loaded image files, or a solid color chosen from the color picker.

Arcade mode includes a system dropdown using the BitLCD suffix list and mapped ScreenScraper numeric system IDs when available. The suffix is used for generated filenames, and the system ID is sent to ScreenScraper searches.

ScreenScraper arcade search uses local credential files when available:

```text
resources\screenscraper_devid.txt
resources\screenscraper_devpassword.txt
resources\screenscraper_softname.txt
resources\screenscraper_ssid.txt
resources\screenscraper_sspassword.txt
```

Blank canvas mode supports click/drag object placement, drag handles for resizing/skewing, arrow-key movement, plus/minus scaling, selected-layer fit/fill, horizontal flip, vertical flip, rotation by degrees, layer forward/backward controls, selected-layer delete, duplicate layer, and full art clear.

The Layers panel lists freeform objects so stacked layers can be selected directly. Animation controls support per-layer preset, start time, duration, visible-from time, and visible-to time.

Current animation presets:

- FadeIn
- FadeOut
- SlideInLeft
- SlideInRight
- SlideInUp
- SlideInDown
- SlideOutLeft
- SlideOutRight
- SlideOutUp
- SlideOutDown
- Pulse
- Bounce
- Spin
- ZoomIn
- ZoomOut

Undo / Redo tracks panel assignments, panel clears, background changes, search text application, selected theme-file entries, freeform layer edits, and animation edits.

MusicBrainz metadata-only rows may appear without thumbnails and cannot be placed directly on the canvas.

## Generate

After previewing the canvas, click `Generate Static JPG` or `Generate Animated MP4`.

The app prompts for the matching MP4 filename, then writes:

```text
<mp4 filename base> (JUKE).jpg
```

Animated output uses:

```text
<mp4 filename base> (JUKE).mp4
```

For Arcade mode, the selected system suffix is used instead of `(JUKE)`.

The generated file is saved under the app's local `output\marquees` folder and Explorer opens to the created file.
