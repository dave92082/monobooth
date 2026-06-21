# MonoBooth

A simple Windows photo booth: a live camera preview, a "3 · 2 · 1 · Smile!" countdown, four
snapshots, and a printed 2×6 photo strip. Originally built in 2011 for a wedding; rebuilt in 2026 on
modern .NET.

---

## For event hosts — install & run

1. **Download** the latest `MonoBooth-Setup-x.y.z.exe` from the
   [Releases page](https://github.com/dave92082/monobooth/releases).
2. **Run it.** It installs MonoBooth and adds a Start-menu (and optional desktop) shortcut. No .NET
   install is needed — everything is bundled.
3. **Plug in your webcam** (and your photo printer, if you have one).
4. **Allow camera access for desktop apps:** Windows **Settings ▸ Privacy & security ▸ Camera**, and
   turn **"Let desktop apps access your camera"** on. (If this is off, MonoBooth shows "No camera
   found".)
5. **Launch MonoBooth.** Press **START**, strike a pose for each of the four shots, and collect your
   strip. Press **Esc** to exit.

Finished strips and the individual photos are saved to **`Pictures\MonoBooth`**.

### Printing

MonoBooth sends each strip as a borderless **2×6** print and asks the printer for **2 copies** by
default. On a dye-sub photo printer loaded with 2×6 media — e.g. a **Kodak 6850** — the printer
prints and cuts the strips, so you get two ready-to-share 2×6 strips per session. No printer? It just
skips printing and keeps running.

### Branding the booth for your event

Use your own background art (event name, logos, a decorative frame):

1. Put your image somewhere handy and set `BackgroundImagePath` in the settings file (see below).
2. Launch MonoBooth. The picture fills the screen and frames the smaller live preview and photo
   strip.
3. **Right-drag** the preview or the strip to position them over your artwork — the placement is
   saved automatically when you exit.

The bundled background already places the strip in its white box and the preview under the
"monobooth" wordmark, so it works out of the box.

### Settings

Settings live in **`%LOCALAPPDATA%\MonoBooth\settings.json`** (created on first run). Edit it and
relaunch to apply changes.

| Key | Default | Meaning |
| --- | --- | --- |
| `FrameCount` | `4` | Photos per strip. |
| `CountdownSeconds` | `3` | Countdown before each shot. |
| `ReviewMilliseconds` | `1200` | Pause showing each shot. |
| `BorderWidth` | `12` | Border (px) around each photo. |
| `BorderColor` | `"Black"` | Named colour or `#RRGGBB`. |
| `FullScreen` | `true` | Borderless kiosk vs. a normal window. |
| `BackgroundImagePath` | `""` | Your background image; empty uses the bundled one. |
| `PreviewArea` | `{45,37,44,42}` | Live-preview box as `{X,Y,Width,Height}` percentages of the screen. |
| `StripArea` | `{12.5,10.5,19,82}` | Photo-strip box, same percentage format. |
| `OutputDirectory` | `"{Pictures}/MonoBooth"` | Where photos are saved (`{Pictures}` = your Pictures folder). |
| `PreferredCamera` | `""` | Camera name substring (e.g. `"Logitech"`); empty picks the first. |
| `PrintEnabled` | `true` | Send strips to the default printer. |
| `PrintCopies` | `2` | Number of 2×6 strips per session (the printer's copy count). |

If you ever want to start fresh, delete `settings.json` and relaunch.

---

## For developers

### Requirements

- Windows 10 (build 19041 / version 2004) or newer.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- A webcam.

### Build & run

```sh
cd src/MonoBooth
dotnet run -c Release
```

Build a self-contained copy (no .NET install required to run it):

```sh
dotnet publish src/MonoBooth -c Release -r win-x64 --self-contained
```

### Build the installer

The installer is [Inno Setup](https://jrsoftware.org/isinfo.php). After publishing (above), compile:

```sh
ISCC.exe installer/MonoBooth.iss
```

The `MonoBooth-Setup-x.y.z.exe` lands in `installer/Output/`.

### Releases (CI)

`.github/workflows/release.yml` runs when a GitHub Release is **published**: it publishes the app,
builds the installer, and attaches `MonoBooth-Setup-*.exe` to the release. You can also run it
manually from the Actions tab (**Run workflow**) for a test build. To cut a release: tag a version
(e.g. `v2.0.0`) and publish a Release for it.

### Project layout

```
src/MonoBooth/
  Program.cs                     app entry point
  MainForm.cs                    kiosk UI (preview, overlay, thumbnails, Start, drag-to-arrange)
  Configuration/                 JSON settings + per-user writable paths
  Camera/                        ICameraService + Windows MediaCapture implementation
  Imaging/FilmstripComposer.cs   stacks frames into the bordered strip
  Printing/FilmstripPrinter.cs   prints the 2×6 strip
  Session/                       the countdown → capture → save → print flow
installer/MonoBooth.iss          Inno Setup installer script
.github/workflows/release.yml    build + package on release
```

### What changed in the 2026 rebuild

- **.NET 3.5 → .NET 8** (`net8.0-windows`), SDK-style project, nullable reference types.
- **EmguCV/OpenCV native DLLs → Windows `MediaCapture`** — no native binaries to ship; the camera is
  reached through the built-in WinRT API.
- **VAkos XML-config DLL → `System.Text.Json`**; settings live in a per-user writable folder so the
  app works when installed under Program Files.
- Removed dead code: the gphoto2/DSLR path, the bit.ly/TinyURL URL shortener, and the unused QR-code
  dependency.
- **Bug fixes:** the preview no longer touches the UI from a background thread; a steady render timer
  drives repaints (no starved thumbnails/flash); frames and bitmaps are disposed (no leaks); the
  countdown is an overlay that can't bleed into a saved photo; the app survives having no printer;
  output goes to a tidy folder instead of GUID-named files in the working directory.
