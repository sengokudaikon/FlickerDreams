using HarmonyLib;
using Il2Cpp;

namespace FlickerDreams;

// DreamslashMutator scales every range by (1 + GetTotalIncreasedRange()): the target search in mutateTargetLocation
// (base 6, 8 for shadow casts; for a sword wave, the point it flies toward) and getStopRange. The shadow search adds it to
// 6 instead. The game's own value is 0.2 with the Dream Aura node while the aura is down, else 0. Rewriting it as
// m * (1 + x) - 1 multiplies the base reach by m and keeps the node's 20 % on top. The aura call (excludeAura) sizes the
// aura, so it is left alone. Mutate inlines the value, so the dash (Strikes) and the wave's flight (Waves) are scaled there.
[HarmonyPatch(typeof(DreamslashMutator), nameof(DreamslashMutator.GetTotalIncreasedRange))]
internal static class GetTotalIncreasedRangePatch {
    private static void Postfix(DreamslashMutator __instance, bool excludeAura, ref float __result) {
        if (excludeAura || !Dreamslash.Flickering(__instance)) {
            return;
        }

        __result = Settings.ReachMultiplier * (1f + __result) - 1f;
    }
}
