using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace FlickerDreams;

// The skill bar shows the mod's cooldown like a game cooldown. The game fills AbilityBarIcon.cooldownBar only from the
// charge manager, for abilities with charges, so nothing else writes Dreamslash's bar. Each frame OnUpdateTick sets the
// bar's object active exactly while the icon's cooldownBarActive flag is on, which the charge manager turns on
// (activateCooldownBar) for abilities with charges. While the mod's cooldown runs the postfix turns the flag on the same way
// and shows the time left with the game's SetFillAmount0to1: with a Dream stack to spend the bar is dimmed, as for a skill
// with a charge left. When the cooldown ends, ResetFillAmount empties it and plays the game's off-cooldown shine, and the
// flag goes back to how it was.
[HarmonyPatch(typeof(AbilityBarIcon), nameof(AbilityBarIcon.OnUpdateTick))]
internal static class SkillBarCooldownPatch {
    private static readonly Fault _fault = new("showing the Dreamslash cooldown");
    private static IntPtr _shownOn;
    private static bool _wasActive;

    private static void Postfix(AbilityBarIcon __instance) {
        float left = DreamslashCooldown.Remaining;
        if (_fault.Stopped || (left <= 0f && _shownOn == IntPtr.Zero)) {
            return;
        }

        try {
            DreamslashMutator mutator = DreamslashCooldown.Mutator;
            Ability ability = __instance.ability;
            if (mutator?.ability == null || ability == null || ability.Pointer != mutator.ability.Pointer) {
                // Dreamslash left this slot: the bar is the next skill's now.
                if (_shownOn == __instance.Pointer) {
                    _shownOn = IntPtr.Zero;
                }

                return;
            }

            GameObject bar = __instance.cooldownBar?.gameObject;
            if (bar == null) {
                return;
            }

            if (left > 0f && Dreamslash.Flickering(mutator)) {
                if (_shownOn != __instance.Pointer) {
                    _shownOn = __instance.Pointer;
                    _wasActive = __instance.cooldownBarActive;
                }

                __instance.activateCooldownBar();
                if (!bar.activeSelf) {
                    bar.SetActive(true);
                }

                __instance.SetFillAmount0to1(left / Settings.Cooldown, mutator.currentDreamStacks > 0);
            } else if (_shownOn == __instance.Pointer) {
                __instance.ResetFillAmount();
                if (!_wasActive) {
                    __instance.deactivateCooldownBar();
                }

                _shownOn = IntPtr.Zero;
            }
        } catch (Exception exception) {
            _fault.Report(exception);
        }
    }
}

// The ability tooltip's cooldown line. Offline, UITooltipAbility.updateCooldown asks the ability's mutator for
// GetCooldownWithStats and shows nothing when it is 0, as for Dreamslash. While updateCooldown runs, the mutator answers with
// the mod's cooldown instead. Nothing else sees the changed value, so it never becomes a cooldown the game applies.
[HarmonyPatch(typeof(UITooltipAbility), nameof(UITooltipAbility.updateCooldown))]
internal static class AbilityTooltipCooldownPatch {
    public static bool Updating { get; private set; }

    private static void Prefix() => Updating = true;

    private static void Postfix() => Updating = false;
}

[HarmonyPatch(typeof(AbilityMutator), nameof(AbilityMutator.GetCooldownWithStats))]
internal static class TooltipCooldownValuePatch {
    private static readonly Fault _fault = new("showing the Dreamslash cooldown in its tooltip");

    private static void Postfix(AbilityMutator __instance, ref float __result) {
        if (!AbilityTooltipCooldownPatch.Updating || _fault.Stopped || __result > 0f || Settings.Cooldown <= 0f) {
            return;
        }

        try {
            if (Dreamslash.Flickering(__instance.TryCast<DreamslashMutator>())) {
                __result = Settings.Cooldown;
            }
        } catch (Exception exception) {
            _fault.Report(exception);
        }
    }
}
