# NR Radio

A custom music mod for **NIGHT-RUNNERS** (Touge pre-alpha), a BepInEx 6 IL2CPP plugin modelled on
[NRPFarMod](https://github.com/iLollek/NRPFarMod): your songs are mixed into the game's own playlist and play
through the game's music channel, so they behave like the original soundtrack (they duck/change while driving
fast, follow menu and pause handling, etc.).

## Install
1. Install **BepInEx 6 Unity IL2CPP (build 647 or newer)** and launch the game once so BepInEx generates `interop`.
2. Copy `NRRadio.dll` to `BepInEx/plugins/NRRadio/`.
3. Launch the game once. A `Music` folder appears next to the game exe with one folder per scene:
   ```
   Music/
     garage/  meetspot/  cruise/  race/  race_win/  race_loss/  all/
   ```
4. Drop `.ogg .wav .mp3 .flac` files into the scene folders. `all/` plays in garage, meetspot, cruise and race. The main menu music is not covered.
   To share songs between scenes, make a folder named after them separated by spaces, e.g. `garage cruise`.
5. Restart the game.

## How it plays
Whenever the game picks its next song (scene start, song ends, or you skip on the phone) there is a chance
(default: 100% in the garage/menu, 75% elsewhere) one of **your** songs for that scene plays instead; otherwise the game plays its own. Songs shuffle
per scene without repeats. When yours ends, the game picks again.

## Hotkeys (configurable in `BepInEx/config/nrmmod.NRRadio.cfg`)
| Key | Action |
|-----|--------|
| P | play a custom song now (next in shuffle) |
| O | previous custom song |
| M | turn mixing custom songs into the playlist on/off |
| N | unload unused audio to free RAM |

Config also has `GarageMenuChancePercent` (default 100) and `OtherScenesChancePercent` (default 75).

## Build
.NET 6+ SDK and the game with BepInEx installed:
```
cp game.props.example game.props   # set GamePath
dotnet build NRRadio -c Release
```

## Status
v0.3.0: rewritten on the pre-alpha's real music classes (`PlanetJem.Audio.AudioManager` / `MusicPlayer`).
Custom clips are swapped onto the game's own music AudioSource, so its fades, ducking and filters apply, and
the game's now-playing overlay shows the song name. Not compiled or run against the game yet; expect possible
API mismatches. Not implemented from NRPFarMod: the INSERT GUI and per-song volume/pitch editing.
