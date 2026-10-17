# Using Flicker Dreams

Flicker Dreams turns Dreamslash into Path of Exile's Flicker Strike. It only changes offline play and only after you
allocate Dreamshift. Without Dreamshift, or online, Dreamslash plays exactly as the game ships it.

## Requirements

- Last Epoch, played offline.
- [MelonLoader](https://github.com/LavaGang/MelonLoader) 0.7.x installed into the game. Run the game once after
  installing it so MelonLoader generates its assemblies.

## Install

Eterra's Gift's Mods page can build and install the mod for you, and shows its settings.

To build it yourself you need the .NET 8 SDK or later:

```
dotnet build src/FlickerDreams.csproj -c Release
```

The build looks for Last Epoch in Steam's default folder, `C:\Program Files (x86)\Steam\steamapps\common\Last Epoch`.
If yours is elsewhere, set the `LAST_EPOCH_PATH` environment variable to it or add `-p:GamePath="<Last Epoch folder>"`;
the build says so when it cannot find the game. Copy `src/bin/Release/net6.0/FlickerDreams.dll` into the game's `Mods`
folder, or add `-p:DeployToGame=true` to the build to copy it there. Close the game first: a running game holds the DLL.

Start the game. `MelonLoader/Latest.log` lists Flicker Dreams with its version and a line of its current settings.

## How it plays

All changes are in the Dreamslash skill tree. Their tooltips describe them, and holding Alt shows the details.

- **Dreamshift** unlocks the mod. Dreamslash then needs an enemy within reach (8 meters by default), and you blink to just
  past your target. Each strike that consumes a Dream stack strikes again at another enemy within 6 meters, consuming
  another stack, until you run out. Each use starts a cooldown (2 seconds by default); while it runs, a use needs a Dream
  stack and is refused without one. A use that consumes a shadow for a stack, as Dreamshift does, ignores the cooldown and
  needs no enemy near you.
- **Shared Memories**: instead of refracting, each strike that consumes a Dream stack has the node's chance (15 % per point) to send a sword wave at
  another enemy within 6 meters. The wave hits only that enemy and costs mana; waves you cannot pay for are not sent.
- **Inception**: a strike can send up to 4 sword waves, each at a different enemy. Each wave after the first has half the
  chance of the one before.
- **Figments of Annihilation**: shadows no longer perform Dreamslash, so each repeat in a chain counts as one shadow, up to
  3. The first strike of a chain gets no bonus; the fourth and later get the full bonus.

Other nodes keep their game effects.

## Settings

Settings live in `UserData/MelonPreferences.cfg` in the game folder, under `[FlickerDreams]`. Edit them with the game
closed or while it runs: a saved edit applies on the next cast. Camera smoothing applies when the camera is set up: in a
new area, or after changing the game's Camera Smoothing option.

| Setting | Default | What it does |
| --- | --- | --- |
| `ReachMultiplier` | 1.3333 | Multiplies Dreamslash's base reach (6), its dash and how far sword waves fly. 1.3333 makes the reach 8. |
| `Cooldown` | 2 | Seconds after each use before Dreamslash is free again. 0 turns the cooldown off. |
| `WaveManaCost` | 0.05 | Mana per sword wave as a share of maximum mana (0.05 is 5 %). |
| `WaveManaFlat` | 10 | Flat mana per sword wave, on top of `WaveManaCost`. |
| `CameraSmoothing` | 3 | Makes the camera follow this many times more slowly than the game's Camera Smoothing option, so it eases after a blink. 1 is the game's. Needs the game option set to Low or Full, not Off; Full is smoothest. |
| `LogCasts` | false | Logs each strike, its Dream stacks, repeats, sword waves and refused uses. |

## Troubleshooting

- **Nothing changes**: check that Dreamshift is allocated and you are playing offline, and that `Latest.log` lists Flicker
  Dreams.
- **A feature stops working**: if a part of the mod hits an error, it stops changing the game and logs one line starting
  with `Flicker Dreams stopped`. The rest of the mod keeps working. Restart the game to try again, and include that line
  when you report the problem.
- **MelonLoader loads no mods after a game update**: on Last Epoch 1.5, MelonLoader can generate a broken
  `UnityEngine.CoreModule` ([MelonLoader #1218](https://github.com/LavaGang/MelonLoader/issues/1218)). Eterra's Gift
  repairs it automatically; otherwise use a CoreModule fix until MelonLoader ships one.

## License

GNU AGPL 3.0 or later, with a permission for combining the mod with the game. Eleventh Hour Games may use the code in
Last Epoch without the AGPL's conditions. See [NOTICE.md](NOTICE.md) and [LICENSE](LICENSE).
