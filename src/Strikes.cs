using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace FlickerDreams;

// Repeats and sword waves are recasts through the game's own CreateAbilityObjectOnDeath on the slash object: when the slash
// ends it rolls chanceToCast (0 or less always casts) and casts Dreamslash again at a random enemy near where it landed.
//
// Refraction in the game: a direct cast that consumed a Dream stack arms one such roll at refractionChance; the recast is a
// Special cast, which Mutate treats like noMove: the in-place sword wave. With refractionsCanChain (Inception) a Special
// cast arms the next roll at half the chance. Mutate dashes for every use type except Shadow and Special.
//
// The mod takes that over. Mutate's refraction block only runs while refractionChance > 0, so the prefix sets it to 0 for
// the call and the postfix restores it. Instead, a strike that spent a Dream stack (a press that consumed one, or a repeat):
// - repeats: a Special3 recast, which dashes; each repeat spends a Dream stack, so the chain ends with them;
// - arms a wave caster, a Special recast that WaveCasterPatch turns into one sword wave per extra enemy.
// Every strike's dash is extended into a blink; Mutate writes MoveCreatorToPositionOnDeath.maxMoveRange = 6 * (1 + node
// bonus) with GetTotalIncreasedRange inlined, so the reach patch never reaches it.
[HarmonyPatch(typeof(DreamslashMutator), nameof(DreamslashMutator.Mutate))]
internal static class MutatePatch {
    private static readonly Fault _fault = new("changing Dreamslash");

    // Whether the mod handles this cast, the refraction chance the prefix took away, and whether this cast consumed a
    // Dream stack (Mutate clears that flag).
    internal readonly record struct CastState(bool Active, float RefractionChance, bool ConsumedStack);

    private static void Prefix(DreamslashMutator __instance, UseType useType, out CastState __state) {
        __state = default;
        if (_fault.Stopped || !Dreamslash.Flickering(__instance)) {
            return;
        }

        try {
            __state = new CastState(true, __instance.refractionChance, __instance.consumedDreamStackThisCast);
            __instance.refractionChance = 0f;
            if (useType == Recasts.RepeatUseType) {
                int amount = -1;
                __instance.ModifyDreamStacks(ref amount);
            }
        } catch (Exception exception) {
            _fault.Report(exception);
        }
    }

    private static void Postfix(DreamslashMutator __instance, GameObject abilityObject, UseType useType, GameObject __result, CastState __state) {
        if (__state.Active) {
            __instance.refractionChance = __state.RefractionChance;
        }

        if (_fault.Stopped) {
            return;
        }

        try {
            GameObject slash = __result ?? abilityObject;
            if (slash == null) {
                return;
            }

            Recasts.RemoveAdded(slash);
            if (!__state.Active) {
                return;
            }

            string log = useType == UseType.Special ? Waves.Shape(__instance, slash) : Strike(__instance, slash, useType, __state);
            if (Settings.LogCasts) {
                MelonLogger.Msg($"Dreamslash {useType}: {__instance.currentDreamStacks} Dream stack(s){log}");
            }
        } catch (Exception exception) {
            _fault.Report(exception);
        }
    }

    private static string Strike(DreamslashMutator mutator, GameObject slash, UseType useType, CastState state) {
        string dash = ExtendDash(slash);
        bool direct = useType is UseType.Direct or UseType.InstantDirect or UseType.SummonerCastDirect;
        if (!(direct && state.ConsumedStack) && useType != Recasts.RepeatUseType) {
            return dash;
        }

        string repeat = "; no stack to repeat";
        if (mutator.currentDreamStacks > 0) {
            Recasts.Add(mutator, slash, Recasts.RepeatUseType);
            repeat = "; repeats";
        }

        Recasts.Add(mutator, slash, UseType.Special);
        return $"{dash}{repeat}; waves armed";
    }

    // MoveCreatorToPositionOnDeath moves you to the target shifted by offsetDistanceFromTarget: a positive offset stops that
    // far short (Dreamslash uses 1.5), a negative one lands past the target by its radius plus the offset. The changes:
    // maxMoveRange scaled like the reach (Mutate sets it on every cast, so this never compounds on a pooled object); landing
    // past the target; no onlyMoveWithinRange gate (it cancels the dash for targets outside the dash's min/max range); and a
    // blink: moveOnStartInstead puts you at the landing point as the slash starts instead of sliding along it.
    private static string ExtendDash(GameObject slash) {
        MoveCreatorToPositionOnDeath move = slash.GetComponent<MoveCreatorToPositionOnDeath>();
        if (move == null) {
            return "";
        }

        move.maxMoveRange *= Settings.ReachMultiplier;
        move.offsetDistanceFromTarget = -Dreamslash.PassThrough;
        move.onlyMoveWithinRange = false;
        move.moveOnStartInstead = true;
        move.moveOverDurationInstead = false;
        return $"; dash {move.maxMoveRange:0.#}";
    }
}

// The casters the mod adds to a slash. Each carries a mark in manaRequirementOverrideValue, which the game reads only with
// manaRequirementOverride on (never for these). Unlike a remembered pointer, a mark cannot go stale when an object is
// destroyed instead of pooled and its memory reused. Ability objects are pooled, so every caster this mod added is removed
// from a Dreamslash object before anything new is added; otherwise an old recast could fire again.
internal static class Recasts {
    public const UseType RepeatUseType = UseType.Special3;

    private const float _repeatMark = -7101f;
    private const float _waveMark = -7102f;

    public static void Add(DreamslashMutator mutator, GameObject slash, UseType useType) {
        CreateAbilityObjectOnDeath caster = slash.AddComponent<CreateAbilityObjectOnDeath>();
        caster.abilityToInstantiate = mutator.ability;
        caster.mutatorOverride = mutator;
        caster.useType = useType;
        caster.aimingMethod = CreateAbilityObjectOnDeath.AimingMethod.Random;
        caster.aimingDistance = Dreamslash.SearchRange;
        caster.fixedYForwards = true;
        caster.targetEnemiesInsteadOfLocation = true;
        caster.targetLocationIfNoEnemy = false;
        caster.mustTargetDifferentEnemies = true;
        caster.chanceToCast = 0f;
        caster.manaRequirementOverrideValue = useType == RepeatUseType ? _repeatMark : _waveMark;
    }

    public static bool IsWaveCaster(CreateAbilityObjectOnDeath caster) => caster.manaRequirementOverrideValue == _waveMark;

    public static void RemoveAdded(GameObject slash) {
        foreach (CreateAbilityObjectOnDeath caster in slash.GetComponents<CreateAbilityObjectOnDeath>()) {
            if (caster.manaRequirementOverrideValue is _repeatMark or _waveMark) {
                UnityEngine.Object.DestroyImmediate(caster);
            }
        }

        WaveHitPatch.Unlock(slash);
    }
}
