using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace FlickerDreams;

// Sword waves are the game's refraction: a Special cast of Dreamslash, which slashes in place and sends a wave (Mutate turns
// the dash off for it). A strike that spent a Dream stack carries one wave caster; when the slash ends, WaveCasterPatch
// decides how many waves it sends and sends them itself, one per enemy, through the player's AbilityObjectConstructor (as
// CreateAbilityObjectOnDeath would). On the way to Mutate, mutateTargetLocation turns a Special cast's target into the point
// Dreamslash's reach away in that enemy's direction, so the enemy is known only from the send. Mutate then locks the wave
// onto it, so it hits only that enemy (WaveHitPatch), and lets it fly the whole way (WaveFlightPatch).
internal static class Waves {
    private static IntPtr _sendingAt;

    public static void Send(DreamslashMutator mutator, Actor player, Vector3 location, Actor target) {
        _sendingAt = target.Pointer;
        try {
            player.abilityObjectConstructor.constructAbilityObject(mutator.ability, location, target.position(), UseType.Special, null, true, false, mutator, false, null, 0f,
                AbilityID.none, false, Vector3.zero);
        } finally {
            _sendingAt = IntPtr.Zero;
        }
    }

    // Mutate of a wave, inside its Send.
    public static string Shape(DreamslashMutator mutator, GameObject wave) {
        IntPtr target = _sendingAt;
        string flight = WaveFlightPatch.Extend(wave);
        if (target == IntPtr.Zero) {
            return "; wave found no enemy at its target, so it hits everything" + flight;
        }

        return (WaveHitPatch.Lock(wave, target) ? "; wave locked on its target" : "; wave has no HitDetector, so it hits everything") + flight;
    }
}

// A wave caster fires when its slash ends (CreateAbilityObjectOnDeath.createAbilityObject). The prefix settles the count
// there, where the landing point, the enemies and the mana are known: Shared Memories' refraction chance rolls the first
// wave (Inception chains more at half the chance each), capped by MostWaves, by the mana and by the different enemies in
// reach. It pays for those waves only, sends one at each of those enemies, and skips the caster's own cast.
[HarmonyPatch(typeof(CreateAbilityObjectOnDeath), nameof(CreateAbilityObjectOnDeath.createAbilityObject))]
internal static class WaveCasterPatch {
    private static readonly Fault _fault = new("sending sword waves");

    private static bool Prefix(CreateAbilityObjectOnDeath __instance) {
        if (!Recasts.IsWaveCaster(__instance)) {
            return true;
        }

        if (_fault.Stopped || Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay) {
            return false;
        }

        try {
            DreamslashMutator mutator = __instance.mutatorOverride?.TryCast<DreamslashMutator>();
            BaseMana mana = mutator?.mana;
            Actor player = mutator?.actor;
            if (mana == null || player == null) {
                return false;
            }

            int rolled = Roll(mutator);
            if (rolled <= 0) {
                return false;
            }

            float step = Settings.WaveManaCost * mana.maxMana + Settings.WaveManaFlat;
            int affordable = step > 0f ? Math.Min(rolled, (int)(mana.currentMana / step)) : rolled;
            Vector3 location = __instance.transform.position;
            Il2CppSystem.Collections.Generic.List<Actor> targets = EnemiesNear(player, location, affordable);
            int waves = targets.Count;
            float cost = step * waves;
            if (Settings.LogCasts) {
                MelonLogger.Msg($"Dreamslash waves: rolled {rolled}, mana for {affordable} -> {waves} (enemies in {Dreamslash.SearchRange:0.#}), cost {cost:0.#} of {mana.currentMana:0} mana");
            }

            if (waves <= 0) {
                return false;
            }

            if (cost > 0f) {
                mana.reduceMana(cost);
            }

            for (int i = 0; i < targets.Count; i++) {
                Waves.Send(mutator, player, location, targets[i]);
            }

            return false;
        } catch (Exception exception) {
            _fault.Report(exception);
            return false;
        }
    }

    // Every full 100 % is a sure wave and the rest is a roll; with Inception (refractionsCanChain) each further wave rolls at
    // half the previous chance until one fails. Never more than MostWaves.
    private static int Roll(DreamslashMutator mutator) {
        float chance = mutator.refractionChance;
        if (chance <= 0f) {
            return 0;
        }

        int waves = (int)chance;
        if (UnityEngine.Random.value < chance - waves) {
            waves++;
        }

        if (waves > 0 && mutator.refractionsCanChain) {
            for (float next = chance * 0.5f; waves < Dreamslash.MostWaves && UnityEngine.Random.value < next; next *= 0.5f) {
                waves++;
            }
        }

        return Math.Min(waves, Dreamslash.MostWaves);
    }

    // Different enemies, up to limit, within SearchRange of the landing point.
    private static Il2CppSystem.Collections.Generic.List<Actor> EnemiesNear(Actor player, Vector3 location, int limit) {
        Alignment alignment = player.GetAlignment();
        var found = new Il2CppSystem.Collections.Generic.List<Actor>();
        Il2CppSystem.Collections.Generic.IEnumerable<Actor> exclude = found.Cast<Il2CppSystem.Collections.Generic.IEnumerable<Actor>>();
        float range = Dreamslash.SearchRange;
        while (found.Count < limit && alignment.TryGetRandomEnemyInRangeOfLocation(ref location, ref range, out Actor enemy, exclude) && enemy != null) {
            found.Add(enemy);
        }

        return found;
    }
}

// A wave flies to the point mutateTargetLocation picked, Dreamslash's reach (with ReachMultiplier) away. Its
// SetDurationBasedOnDistanceToTarget stops it at maxRange, which Mutate sets to the game's 6 (x1.2 with the Dream Aura node)
// with GetTotalIncreasedRange inlined, and gives it a lifetime for that distance. Start, which runs once the wave exists,
// reads both; for the mod's waves the prefix raises maxRange by ReachMultiplier for that call, so the wave flies the whole
// way at its usual speed. The postfix puts the value back, so nothing carries over to a pooled object's next use.
[HarmonyPatch(typeof(SetDurationBasedOnDistanceToTarget), nameof(SetDurationBasedOnDistanceToTarget.Start))]
internal static class WaveFlightPatch {
    private static readonly ExpiringMap<bool> _waves = new(3f);
    private static readonly Fault _fault = new("extending sword waves");

    public static string Extend(GameObject wave) {
        SetDurationBasedOnDistanceToTarget timing = wave.GetComponent<SetDurationBasedOnDistanceToTarget>();
        if (timing == null || !timing.hasMaxRange) {
            return "";
        }

        _waves.Set(timing.Pointer, true);
        return $"; flies {timing.maxRange * Settings.ReachMultiplier:0.#}";
    }

    private static void Prefix(SetDurationBasedOnDistanceToTarget __instance, out float __state) {
        __state = 0f;
        if (_fault.Stopped || _waves.Count == 0) {
            return;
        }

        try {
            if (_waves.TryGet(__instance.Pointer, out bool _)) {
                _waves.Remove(__instance.Pointer);
                __state = __instance.maxRange;
                __instance.maxRange *= Settings.ReachMultiplier;
            }
        } catch (Exception exception) {
            _fault.Report(exception);
        }
    }

    private static void Postfix(SetDurationBasedOnDistanceToTarget __instance, float __state) {
        if (__state > 0f) {
            __instance.maxRange = __state;
        }
    }
}

// A wave hits only the enemy it was sent at. HitDetector.hitActor is where every hit of an ability object starts (its
// trigger, its first tick, and abilities that hit for it), so the prefix refuses any other actor for a locked hit. A sword
// wave keeps flying to its target location and fades as usual. Locks are by HitDetector and cleared when a pooled sword
// wave is mutated again.
[HarmonyPatch(typeof(HitDetector), nameof(HitDetector.hitActor))]
internal static class WaveHitPatch {
    private static readonly ExpiringMap<IntPtr> _locks = new(3f);
    private static readonly Fault _fault = new("locking sword waves");

    public static bool Lock(GameObject wave, IntPtr target) {
        HitDetector detector = Detector(wave);
        if (detector == null) {
            return false;
        }

        _locks.Set(detector.Pointer, target);
        return true;
    }

    public static void Unlock(GameObject slash) {
        if (_locks.Count == 0) {
            return;
        }

        HitDetector detector = Detector(slash);
        if (detector != null) {
            _locks.Remove(detector.Pointer);
        }
    }

    private static HitDetector Detector(GameObject hit) => hit.GetComponent<HitDetector>() ?? hit.GetComponentInChildren<HitDetector>();

    private static bool Prefix(HitDetector __instance, Actor actor) {
        if (_fault.Stopped || _locks.Count == 0 || actor == null) {
            return true;
        }

        try {
            return !_locks.TryGet(__instance.Pointer, out IntPtr target) || actor.Pointer == target;
        } catch (Exception exception) {
            _fault.Report(exception);
            return true;
        }
    }
}
