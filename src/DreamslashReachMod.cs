using DreamslashReach;
using HarmonyLib;
using Il2Cpp;
using Il2CppEHG.Multiplayer;
using MelonLoader;

[assembly: MelonInfo(typeof(DreamslashReachMod), "Dreamslash Reach", "0.1.0", "sengokudaikon")]
[assembly: MelonGame("Eleventh Hour Games", "Last Epoch")]

namespace DreamslashReach;

public sealed class DreamslashReachMod : MelonMod {
    private static MelonPreferences_Entry<float> _reachMultiplier;

    public static float ReachMultiplier => _reachMultiplier?.Value ?? 1f;

    public override void OnInitializeMelon() {
        MelonPreferences_Category category = MelonPreferences.CreateCategory("DreamslashReach");
        _reachMultiplier = category.CreateEntry("ReachMultiplier", 1.5f, description: "Multiplies Dreamslash's base reach (6, or 8 for shadows). Offline only.");
        LoggerInstance.Msg($"Dreamslash reach x{ReachMultiplier}");
    }
}

// DreamslashMutator scales every range by (1 + GetTotalIncreasedRange()): the target search in mutateTargetLocation
// (base 6, 8 for shadow casts) and getStopRange. The shadow search adds it to 6 instead. The game's own value is 0.2
// with the Dream Aura node while the aura is down, else 0. Rewriting it as m * (1 + x) - 1 multiplies the base reach by m
// and keeps the node's 20 % on top. The aura call (excludeAura) sizes the aura, so it is left alone.
[HarmonyPatch(typeof(DreamslashMutator), nameof(DreamslashMutator.GetTotalIncreasedRange))]
internal static class GetTotalIncreasedRangePatch {
    private static void Postfix(bool excludeAura, ref float __result) {
        if (excludeAura || GameplayEnvironment.IsOnlinePlay) {
            return;
        }

        __result = DreamslashReachMod.ReachMultiplier * (1f + __result) - 1f;
    }
}
