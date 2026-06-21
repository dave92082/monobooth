# Developing MonoBooth

Technical notes for building, packaging, and contributing. End-user docs live in the
[README](../README.md).

## Requirements

- Windows 10 (build 19041 / version 2004) or newer.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- A webcam.
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) — only needed to build the installer.

## Build & run

```sh
cd src/MonoBooth
dotnet run -c Release
```

Build a self-contained copy (no .NET install required to run it):

```sh
dotnet publish src/MonoBooth -c Release -r win-x64 --self-contained
```

## Build the installer

After publishing (above), compile the [Inno Setup](https://jrsoftware.org/isinfo.php) script:

```sh
ISCC.exe installer/MonoBooth.iss
```

The `MonoBooth-Setup-x.y.z.exe` lands in `installer/Output/`. Pass `/DMyAppVersion=2.0.0` to stamp a
version.

## Releases (CI)

`.github/workflows/release.yml` runs when a GitHub Release is **published**: it publishes the app,
builds the installer, and attaches `MonoBooth-Setup-*.exe` to the release. You can also run it
manually from the **Actions** tab (**Run workflow**) for a test build.

To cut a release: tag a version (e.g. `v2.0.0`) and publish a Release for it.

## Runtime files

Settings and the camera diagnostics log are written to a per-user, writable location so the app works
when installed under Program Files:

```
%LOCALAPPDATA%\MonoBooth\settings.json
%LOCALAPPDATA%\MonoBooth\monobooth-camera.log
```

The full list of settings keys is in the [README](../README.md#settings); a few developer-oriented
ones (`ReviewMilliseconds`, `BorderWidth`, `FullScreen`, `PreviewArea`, `StripArea`) round out the
set in `Configuration/AppSettings.cs`.

## Project layout

```
src/MonoBooth/
  Program.cs                     app entry point
  MainForm.cs                    kiosk UI (preview, overlay, thumbnails, Start, drag-to-arrange)
  Configuration/                 JSON settings (AppSettings) + per-user paths (AppPaths)
  Camera/                        ICameraService + Windows MediaCapture implementation
  Imaging/FilmstripComposer.cs   stacks frames into the bordered strip
  Printing/FilmstripPrinter.cs   prints the 2×6 strip
  Session/                       the countdown → capture → save → print flow
installer/MonoBooth.iss          Inno Setup installer script
.github/workflows/release.yml    build + package on release
```

## Architecture notes

- **Camera:** `MediaCaptureCameraService` uses Windows `MediaCapture` + `MediaFrameReader`, requesting
  BGRA8 frames so any native camera format (NV12/YUY2/MJPG) yields a usable `SoftwareBitmap`. Frames
  are converted to GDI+ `Bitmap` via projected WinRT APIs (`CopyToBuffer` + `DataReader`) — the older
  `IMemoryBufferByteAccess` COM cast throws under modern C#/WinRT.
- **Preview:** a single ~30 fps render timer drives all repaints; the camera thread only keeps the
  latest frame. Pushing a repaint per frame floods the message queue and starves paints/timers.
- **Countdown & flash** are owner-drawn over the preview, so they can never bleed into a saved photo.

## What changed in the 2026 rebuild

- **.NET 3.5 → .NET 8** (`net8.0-windows`), SDK-style project, nullable reference types.
- **EmguCV/OpenCV native DLLs → Windows `MediaCapture`** — no native binaries to ship.
- **VAkos XML-config DLL → `System.Text.Json`**, stored in a per-user writable folder.
- Removed dead code: the gphoto2/DSLR path, the bit.ly/TinyURL URL shortener, and the unused QR-code
  dependency.
- **Bug fixes:** no cross-thread UI access; frames/bitmaps disposed (no leaks); countdown can't bleed
  into saved photos; survives having no printer; output goes to a tidy folder instead of GUID-named
  files in the working directory.
