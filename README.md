# Dreamslash Reach

A MelonLoader mod for Last Epoch 1.5 that multiplies Dreamslash's base reach: how far it looks for an enemy to dash at.
Offline only; it does nothing while the game is in online play.

| | Game | With the mod (x1.5) |
|---|---|---|
| Reach | 6 (8 for shadow casts) | 9 (12) |
| With the Dream Aura node, aura down (+20 %) | 7.2 | 10.8 |
| Stop range | game value | x1.5 |
| Shadow search | 6 (+0.2) | 6.5 (6.8) |

The game multiplies every Dreamslash range by `1 + DreamslashMutator.GetTotalIncreasedRange()`. A Harmony postfix rewrites
that value as `m * (1 + x) - 1`. The shadow search adds the value to 6 instead of multiplying, so it grows less. The aura's own
size call is left alone.

## Settings

`UserData/MelonPreferences.cfg`, category `DreamslashReach`: `ReachMultiplier` (default 1.5).

## Build

Needs the .NET SDK (6 or later) and a Last Epoch install where MelonLoader 0.7.x has generated its assemblies (launch the game
once). On 1.5 the generated `UnityEngine.CoreModule.dll` must be repaired first (MelonLoader #1159).

```
dotnet build src/DreamslashReach.csproj -c Release
```

The game folder defaults to `D:\SteamLibrary\steamapps\common\Last Epoch`; override it with `-p:GamePath=...` or the
`LAST_EPOCH_PATH` variable. The build copies the mod into the game's `Mods` folder; `-p:DeployToGame=false` turns that off.
Launch with `Last Epoch.exe --offline`.
