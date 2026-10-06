# NR Radio

A custom radio / music mod for **NIGHT-RUNNERS** (Touge pre-alpha), built as a BepInEx 6 IL2CPP plugin.
Each folder of music becomes a radio station you can tune with hotkeys; the game's own music effects
(menus, pause, etc.) still apply because tracks play through the game's music `AudioSource`.

## Install
1. Install **BepInEx 6 Unity IL2CPP (build 647 or newer)** into the game folder and launch the game once so
   BepInEx generates its `interop` assemblies.
2. Copy `NRRadio.dll` into `BepInEx/plugins/NRRadio/`.
3. Launch the game once. A `Radio` folder appears next to the game exe.
4. Put music in sub-folders of `Radio` — **folder name = station name**:
   ```
   Radio/
     Midnight FM/   song1.ogg  song2.mp3
     Touge Tapes/   a.wav  b.flac
   ```
5. Relaunch. Supported: `.ogg .wav .mp3 .flac`.

## Controls (changeable in `BepInEx/config/nrmmod.NRRadio.cfg`)
| Key | Action |
|-----|--------|
| F3  | next station (after the last one: radio off → game music) |
| F4  | previous station |
| F2  | skip track |

Tracks shuffle per station (everything plays once before repeats) and continue across scene changes.

## Build
Needs the .NET 6+ SDK and the game with BepInEx already installed.
```
cp game.props.example game.props   # edit GamePath
dotnet build NRRadio -c Release
```
The DLL is copied into `BepInEx/plugins/NRRadio/` automatically if that folder's parent exists.

## Status
Untested against the game: written from the public game API names (`GodConstant`, `RCC_Settings`) used by
other NIGHT-RUNNERS mods. If the pre-alpha renames them, adjust `RadioPatches.cs`.
