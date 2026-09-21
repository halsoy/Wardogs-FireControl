# WARDOGS Fire Control

A small Windows companion that reads the coordinate text rendered beside the mouse pointer on the WARDOGS tactical map. It captures the desktop only; it does not attach to, inject into, or read memory from the game.

## First-pass features

- Global capture hotkeys: `F1` for a target and `F5` for a gun position.
- Hotkeys can be changed to any keyboard key or modifier combination from **Hotkeys…**.
- Up to six named gun positions and twenty named targets per map.
- A large solution for the selected gun plus compact live solutions for every other saved gun.
- Persistent positions stored in `%LOCALAPPDATA%\WardogsFireControl\state.json`.
- Permanent Bakurani, Ozeti, and Zestafona tower tables with live range, azimuth, MIL, and reachability; click any tower row to target it.
- L81 and SPH-2 flat-ground distance, compass azimuth, and interpolated elevation MIL values.
- Explicit too-close/out-of-range and incomplete-table warnings.
- Per-row target removal plus a separately confirmed bulk-removal action.
- Manual coordinate entry when OCR needs tuning.
- Dark title bar, custom dark dropdowns, and clearly separated table rows and columns.

## Running

Extract the complete folder, then run `WardogsFireControl.exe`. Keep the included DLL, `x64`, `x86`, `Data`, and `tessdata` folders beside the executable. Borderless fullscreen or windowed mode is recommended. Open the tactical map, place the pointer so its `x...` / `y...` readout is visible, then press the capture hotkey.

OCR failures save the inspected crop under `%TEMP%\WardogsFireControl`. Those crops are useful for tuning recognition across resolutions and graphics settings.

Startup details are written to `%LOCALAPPDATA%\WardogsFireControl\diagnostics.log` if OCR or global hotkey registration fails.

## Accuracy boundary

This first pass uses community flat-ground firing tables. It deliberately labels every result **TERRAIN NOT APPLIED**. The newer reported level-ground envelopes are 52–685 m for the L81 and 745–2,660 m for the SPH-2, but the bundled community MIL table does not cover every metre of those reported envelopes. In those small gaps, the app reports that the target may be reachable but refuses to invent a MIL value.

Terrain3D data exists for the maps, but the open project currently labels automatic terrain MIL correction experimental and its heightfield is about 129 MB per map. A later pass can download only the needed chunks and apply correction as an explicit opt-in.

Always verify with a ranging shot. Community measurements are not official BULKHEAD data and may change after game updates.
