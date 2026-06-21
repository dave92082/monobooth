# MonoBooth

A simple Windows photo-booth: live camera preview, a countdown, four snapshots, and a printed
filmstrip. Originally built in 2011 for a wedding on .NET 3.5 + WinForms + EmguCV; rebuilt in 2026
on modern .NET.

## What it does

1. Shows a full-screen live camera preview (kiosk mode).
2. On **Start**, counts down "3 · 2 · 1 · Smile!" and captures a frame — repeated four times.
3. Stitches the frames into a bordered vertical filmstrip.
4. Saves the strip (and the individual frames) to your Pictures folder.
5. Prints the strip to the default printer — two copies side-by-side by default.

Press **Esc** (or **X**) to exit.

### Branding the booth

Drop a decorative image at `BackgroundImagePath` (event name, logos, a frame). It's stretched to
fill the screen, and the smaller live preview and photo strip sit on top of it. **Right-drag** the
preview or the strip to position them over your artwork — the placement is saved to `settings.json`
on exit. You can also set `PreviewArea` / `StripArea` directly for pixel-free, resolution-independent
placement.

## Requirements

- Windows 10 (build 19041 / version 2004) or newer.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build; the .NET 8 Desktop
  Runtime to run a published copy.
- A webcam (USB or built-in).
- **Camera permission for desktop apps:** Settings ▸ Privacy & security ▸ Camera ▸
  *Let desktop apps access your camera* must be **On**. Without it the app shows
  "No camera found".

## Build & run

```sh
cd src/MonoBooth
dotnet run -c Release
```

Or build a self-contained kiosk you can copy to the booth PC:

```sh
dotnet publish src/MonoBooth -c Release -r win-x64 --self-contained
```

## Configuration

Settings live in `settings.json` next to the executable (created on first run). The file is
re-read at startup, so tweak and relaunch.

| Key | Default | Meaning |
| --- | --- | --- |
| `FrameCount` | `4` | Photos per filmstrip. |
| `CountdownSeconds` | `3` | Count down before each shot. |
| `ReviewMilliseconds` | `1200` | Pause after each shot. |
| `BorderWidth` | `12` | Border (px) around each photo. |
| `BorderColor` | `"Black"` | Named colour or `#RRGGBB`. |
| `FullScreen` | `true` | Borderless kiosk vs. a normal window. |
| `BackgroundImagePath` | `""` | Custom backdrop; empty uses the bundled image. Stretched to fill the window and framed around the preview + strip. |
| `PreviewArea` | `{6,9,50,60}` | Live-preview rectangle as `{X,Y,Width,Height}` percentages of the window. |
| `StripArea` | `{62,9,20,72}` | Photo-strip rectangle, same percentage format. |
| `OutputDirectory` | `"{Pictures}/MonoBooth"` | Where strips are saved. `{Pictures}` expands to your Pictures folder. |
| `PreferredCamera` | `""` | Camera name substring (e.g. `"Logitech"`); empty picks the first. |
| `PrintEnabled` | `true` | Send the strip to the default printer. |
| `PrintCopies` | `2` | Number of 2×6 strips the printer runs off (sent as the printer's copy count). |

### Printing

The strip is sent as a single borderless **2×6** print, and the printer's copy count is set to
`PrintCopies`. On a dye-sub photo printer loaded with 2×6 media — e.g. a **Kodak 6850** — the printer
prints and cuts each strip, so the default of **2 copies** hands you two ready 2×6 strips. The page
targets the printer's 2×6 paper if it has one, otherwise a custom 2×6 size. Set `PrintEnabled` to
`false` to skip printing.

## Project layout

```
src/MonoBooth/
  Program.cs                     app entry point
  MainForm.cs                    kiosk UI (preview, overlay, thumbnails, Start)
  Configuration/AppSettings.cs   JSON settings load/save
  Camera/                        ICameraService + Windows MediaCapture implementation
  Imaging/FilmstripComposer.cs   stacks frames into the bordered strip
  Printing/FilmstripPrinter.cs   lays out and prints the strip
  Session/                       the countdown → capture → save → print flow
```

## What changed in the 2026 rebuild

- **.NET 3.5 → .NET 8** (`net8.0-windows`), SDK-style project, nullable reference types.
- **EmguCV/OpenCV native DLLs → Windows `MediaCapture`** — no native binaries to ship; the camera
  is reached through the built-in WinRT API.
- **VAkos XML-config DLL → `System.Text.Json`** (`settings.json`).
- Removed dead code: the gphoto2/DSLR path, the bit.ly/TinyURL URL shortener, and the unused
  QR-code dependency.
- **Bug fixes:** the live preview no longer touches the UI from a background thread without
  marshalling; captured frames and bitmaps are now disposed (no leaks); countdown text is painted
  as an overlay and can never bleed into the saved photo; the app no longer crashes when no printer
  is attached; output files go to a tidy folder instead of GUID-named files in the working
  directory.
