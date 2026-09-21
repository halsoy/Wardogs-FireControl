# WARDOGS Fire Control

An external Windows fire-control companion for WARDOGS. It reads the coordinate text already displayed beside the mouse pointer on the tactical map, then calculates ground range, compass azimuth, and community firing-table elevation values for the L81 Mortar and SPH-2.

The application captures pixels from the desktop only. It does not inject into WARDOGS, inspect game memory, modify game files, or communicate with the game process.

## Features

- Configurable global keyboard hotkeys (`F1` target and `F5` gun by default), including Ctrl, Alt, Shift, and Win combinations.
- Six persistent, renameable gun positions per map.
- Twenty persistent, renameable targets per map.
- Large firing solution for the selected gun and compact solutions for every other gun.
- Static tower table with live range, azimuth, elevation, and reachability.
- Per-target removal and confirmed bulk removal.
- L81 Mortar and SPH-2 firing-table interpolation.
- Explicit minimum range, maximum range, and missing-table warnings.
- Bakurani, Ozeti, and Zestafona community map markers.
- Local OCR; screenshots and coordinates are not uploaded anywhere.
- Manual coordinate entry when OCR needs calibration.
- Dark, always-on-top Windows interface with a fixed, fully contained layout.
- Bullseye application icon sized for Windows title bars, taskbars, and shortcuts.

## Download

The Releases page provides two choices:

- `WardogsFireControl-Standalone.exe` — one downloadable executable. On first launch it extracts the signed-in user's private application payload under `%LOCALAPPDATA%\WardogsFireControl\App` and starts it.
- `WardogsFireControl-win-x64.zip` — the conventional portable folder build. Extract the complete archive and run `WardogsFireControl.exe`.

Keep the DLLs and the `x64`, `x86`, `Data`, and `tessdata` folders beside the executable. The release is self-contained and does not require a separate .NET installation.

Borderless fullscreen or windowed mode is recommended. Open the tactical map, place the pointer so its `x...` and `y...` readout is visible, then press the relevant capture hotkey.

## Building locally

Requirements:

- Windows 10 or Windows 11
- .NET 9 SDK

Build and check the project:

```powershell
dotnet build tests/WardogsFireControl.Tests/WardogsFireControl.Tests.csproj -c Release
dotnet run --project tests/WardogsFireControl.Tests/WardogsFireControl.Tests.csproj -c Release --no-build
```

Create the portable Windows release:

```powershell
dotnet publish src/WardogsFireControl/WardogsFireControl.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false `
  -o publish/WardogsFireControl
```

Tesseract loads native architecture-specific libraries at runtime, so the application itself uses a folder-based payload. The standalone download is a small native launcher with that portable payload embedded inside it.

## Publishing a GitHub release

Push a tag beginning with `v`, for example:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The included GitHub Actions workflow builds the program, runs its checks, creates `WardogsFireControl-win-x64.zip`, and attaches it to the GitHub release.

## Accuracy and data

This release uses community flat-ground firing tables. Terrain elevation is **not** applied, and every firing solution says so. Always verify with a ranging shot.

The currently reported level-ground envelopes are:

- L81 Mortar: 52–685 m
- SPH-2: 745–2,660 m

The bundled community MIL tables do not cover every metre of those reported envelopes. The application reports `NO MIL TABLE` instead of inventing an extrapolated setting.

Firing tables and map marker data are adapted from the MIT-licensed [apollyon-sys/wardogs-calculator](https://github.com/apollyon-sys/wardogs-calculator). Coordinate recognition uses Tesseract OCR and its English `tessdata_fast` model. Full notices are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

WARDOGS Fire Control is an unofficial community project. It is not affiliated with or endorsed by BULKHEAD, Team17, or the WARDOGS developers.

## Local files

Settings and saved positions are stored in:

```text
%LOCALAPPDATA%\WardogsFireControl\state.json
```

Failed OCR crops are written to `%TEMP%\WardogsFireControl`. Startup errors are recorded in `%LOCALAPPDATA%\WardogsFireControl\diagnostics.log`.

## License

The application source is available under the MIT License. Third-party datasets, libraries, and trained data retain their respective licences.
