using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppEHG.Multiplayer;
using MelonLoader;
using UnityEngine;

namespace FlickerDreams;

// Flicker Strike needs a target and has a cooldown that a Frenzy charge bypasses. UsingAbilityPlayer.InitialiseAbilityUse is
// where a player's press becomes an ability use (it takes the target by value; a prefix returning false refuses the use
// before mana, animation or the cast). For Dreamslash with Dreamshift, offline:
// - Target: refused unless the game's own enemy search finds an enemy within Dreamslash's reach of the player.
// - Cooldown: every use starts it; while it runs a use needs a Dream stack (which the press spends as it always does), and
//   without one it is refused with the game's on-cooldown response. Repeats are recasts, not presses, and pay their own stack.
// A use that will consume a shadow passes both.
[HarmonyPatch(typeof(UsingAbilityPlayer), nameof(UsingAbilityPlayer.InitialiseAbilityUse),
    [typeof(Ability), typeof(Vector3), typeof(bool), typeof(bool), typeof(bool), typeof(float)])]
internal static class InitialiseAbilityUsePatch {
    private static readonly Dictionary<string, float> _lastRefusalAt = [];
    private static readonly Fault _fault = new("gating Dreamslash");

    private static bool Prefix(UsingAbilityPlayer __instance, Ability _ability, Vector3 target, bool ignoreCanUseAbilityChecks, bool pressed, ref bool __result) {
        if (_fault.Stopped || ignoreCanUseAbilityChecks || _ability == null || GameplayEnvironment.IsOnlinePlay) {
            return true;
        }

        try {
            DreamslashMutator mutator = DreamslashCooldown.Find(__instance);
            if (mutator?.ability == null || mutator.ability.Pointer != _ability.Pointer || !Dreamslash.Flickering(mutator)) {
                return true;
            }

            Actor player = PlayerFinder.getPlayerActor();
            if (player == null) {
                return true;
            }

            // A use that will consume a shadow has a target (the enemy by the shadow) and brings its own Dream stack.
            bool shadow = ShadowToConsume(mutator, player);
            if (!shadow) {
                Vector3 position = player.position();
                float reach = Dreamslash.BaseReach * (1f + mutator.GetTotalIncreasedRange(false));
                if (!player.GetAlignment().TryGetRandomEnemyInRangeOfLocation(ref position, ref reach, out Actor _, null)) {
                    LogRefusal($"no enemy within {reach:0.##}");
                    __result = false;
                    return false;
                }
            }

            float left = DreamslashCooldown.Remaining;
            if (left > 0f && mutator.currentDreamStacks <= 0 && !shadow) {
                __instance.triedToUseAbilityOnCooldown(mutator.Cast<AbilityInfo>(), target, pressed);
                LogRefusal($"cooldown {left:0.0}s and no Dream stack");
                __result = false;
                return false;
            }

            return true;
        } catch (Exception exception) {
            _fault.Report(exception);
            return true;
        }
    }

    private static void Postfix(Ability _ability, bool __result) {
        if (__result && !_fault.Stopped && _ability != null) {
            DreamslashCooldown.Used(_ability);
        }
    }

    // DreamslashMutator.OnAbilityStartedDreamslash, with the node that consumes shadows (consumeShadowsForDreamStacks) and
    // fewer than the maximum Dream stacks: a use consumes a shadow within 6 + GetTotalIncreasedRange() of the player that has
    // an enemy within the same range, moves you to it, gains a Dream stack, and the press does not spend it. That runs after
    // InitialiseAbilityUse, so the gates ask the same question first.
    private static bool ShadowToConsume(DreamslashMutator mutator, Actor player) {
        if (!mutator.consumeShadowsForDreamStacks || mutator.currentDreamStacks >= mutator.maxDreamStacks) {
            return false;
        }

        Il2CppSystem.Collections.Generic.List<RogueShadow> shadows = mutator.shadowMut?.activeShadows;
        if (shadows == null) {
            return false;
        }

        float range = Dreamslash.BaseReach + mutator.GetTotalIncreasedRange(false);
        Vector3 playerPosition = player.position();
        Alignment alignment = player.GetAlignment();
        for (int i = 0; i < shadows.Count; i++) {
            RogueShadow shadow = shadows[i];
            if (shadow == null) {
                continue;
            }

            Vector3 position = shadow.transform.position;
            float reach = range;
            if (Vector3.Distance(position, playerPosition) <= range && alignment.TryGetRandomEnemyInRangeOfLocation(ref position, ref reach, out Actor _, null)) {
                return true;
            }
        }

        return false;
    }

    // A held button retries every frame: log each reason at most once a second.
    private static void LogRefusal(string reason) {
        if (!Settings.LogCasts) {
            return;
        }

        string kind = reason.Split(' ')[0];
        float now = Time.unscaledTime;
        if (_lastRefusalAt.TryGetValue(kind, out float last) && now - last < 1f) {
            return;
        }

        _lastRefusalAt[kind] = now;
        MelonLogger.Msg($"Dreamslash refused: {reason}");
    }
}

// The mod's own Dreamslash cooldown (the game gives Dreamslash none), shown by CooldownDisplay.
internal static class DreamslashCooldown {
    private static DreamslashMutator _mutator;
    private static float _readyAt;

    // The player's Dreamslash mutator, once a press has found it.
    public static DreamslashMutator Mutator => _mutator != null && !_mutator.WasCollected ? _mutator : null;

    // Seconds until Dreamslash is free again; 0 or less when it is, or when the cooldown is off.
    public static float Remaining => Settings.Cooldown > 0f ? Math.Min(_readyAt - Time.time, Settings.Cooldown) : 0f;

    // The Dreamslash mutator sits on the player with the ability state; cached per player object.
    public static DreamslashMutator Find(UsingAbilityPlayer state) {
        if (Mutator != null && _mutator.gameObject != null && _mutator.gameObject.Pointer == state.gameObject.Pointer) {
            return _mutator;
        }

        _mutator = state.GetComponent<DreamslashMutator>() ?? state.GetComponentInChildren<DreamslashMutator>();
        return _mutator;
    }

    public static void Used(Ability ability) {
        DreamslashMutator mutator = Mutator;
        if (Settings.Cooldown > 0f && mutator?.ability != null && mutator.ability.Pointer == ability.Pointer && Dreamslash.Flickering(mutator)) {
            _readyAt = Time.time + Settings.Cooldown;
        }
    }
}
