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
     main_menu/  garage/  cruise/  meetspot/  race/  all/
   ```
4. Drop `.ogg .wav .mp3 .flac` files into the scene folders. `all/` plays in every scene.
   To share songs between scenes, make a folder named after them separated by spaces, e.g. `garage cruise`.
5. Restart the game.

## How it plays
Whenever the game picks its next song (scene start, song ends, or you skip on the phone) there is a chance
(default 50%) one of **your** songs for that scene plays instead; otherwise the game plays its own. Songs shuffle
per scene without repeats. When yours ends, the game picks again.

## Hotkeys (configurable in `BepInEx/config/nrmmod.NRRadio.cfg`)
| Key | Action |
|-----|--------|
| P | play a custom song now (next in shuffle) |
| O | previous custom song |
| M | turn mixing custom songs into the playlist on/off |
| N | unload unused audio to free RAM |

Config also has `CustomSongChancePercent` (100 = only your songs when the scene has any).

## Build
.NET 6+ SDK and the game with BepInEx installed:
```
cp game.props.example game.props   # set GamePath
dotnet build NRRadio -c Release
```

## Status
Not compiled or run against the game yet. It relies on `GodConstant` / `RCC_Settings` members seen in other
NIGHT-RUNNERS mods; if the pre-alpha renames any, adjust `RadioPatches.cs` / `RadioRunner.cs`.
Not implemented from NRPFarMod: the INSERT GUI and per-song volume/pitch editing.
