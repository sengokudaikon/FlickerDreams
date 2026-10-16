using FlickerDreams;
using MelonLoader;

[assembly: MelonInfo(typeof(FlickerDreamsMod), "Flicker Dreams", "0.16.1", "sengokudaikon")]
[assembly: MelonGame("Eleventh Hour Games", "Last Epoch")]

namespace FlickerDreams;

// Dreamslash as Path of Exile's Flicker Strike, unlocked by the Dreamshift node and offline only. The patches, by file:
// Reach (how far Dreamslash looks), Strikes (repeats, blink, arming waves), Waves (the extra sword waves), UseGate (target
// and cooldown), CooldownDisplay (skill bar and ability tooltip), NodeTooltips (the passive tree) and CameraSmoothing.
public sealed class FlickerDreamsMod : MelonMod {
    public override void OnInitializeMelon() {
        Settings.Load();
        LoggerInstance.Msg(
            $"Reach x{Settings.ReachMultiplier:0.##}; cooldown {Settings.Cooldown:0.#}s; sword waves {Settings.WaveManaCost:P0} of maximum mana + {Settings.WaveManaFlat:0.#}. Offline, with Dreamshift.");
    }
}
