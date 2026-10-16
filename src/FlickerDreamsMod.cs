using System;
using System.Collections.Generic;
using System.Linq;
using FlickerDreams;
using HarmonyLib;
using Il2Cpp;
using Il2CppEHG.Multiplayer;
using Il2CppLE.UI.Tooltips;
using Il2CppLocalizationSystem;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(FlickerDreamsMod), "Flicker Dreams", "0.15.0", "sengokudaikon")]
[assembly: MelonGame("Eleventh Hour Games", "Last Epoch")]

namespace FlickerDreams;

public sealed class FlickerDreamsMod : MelonMod {
    private static MelonPreferences_Entry<float> _reachMultiplier;
    private static MelonPreferences_Entry<float> _repeatChance;
    private static MelonPreferences_Entry<int> _repeatStackCost;
    private static MelonPreferences_Entry<float> _repeatRange;
    private static MelonPreferences_Entry<float> _passThrough;
    private static MelonPreferences_Entry<bool> _requireTarget;
    private static MelonPreferences_Entry<float> _cooldown;
    private static MelonPreferences_Entry<bool> _waves;
    private static MelonPreferences_Entry<string> _waveStyle;
    private static MelonPreferences_Entry<float> _waveChance;
    private static MelonPreferences_Entry<float> _waveManaCost;
    private static MelonPreferences_Entry<float> _waveManaFlat;
    private static MelonPreferences_Entry<bool> _waveCostRises;
    private static MelonPreferences_Entry<float> _waveRange;
    private static MelonPreferences_Entry<int> _waveMax;
    private static MelonPreferences_Entry<float> _cameraSmoothing;
    private static MelonPreferences_Entry<bool> _logCasts;

    public static float ReachMultiplier => _reachMultiplier?.Value ?? 1f;

    public static float RepeatChance => _repeatChance?.Value ?? 0f;

    public static int RepeatStackCost => _repeatStackCost?.Value ?? 1;

    public static float RepeatRange => _repeatRange?.Value ?? 6f;

    public static float PassThrough => _passThrough?.Value ?? 0f;

    public static bool RequireTarget => _requireTarget?.Value ?? false;

    public static float Cooldown => _cooldown?.Value ?? 0f;

    public static bool Waves => _waves?.Value ?? false;

    public static bool ShadowRendWaves => _waveStyle?.Value == "shadowrend";

    public static float WaveChance => _waveChance?.Value ?? 0f;

    public static float WaveManaCost => _waveManaCost?.Value ?? 0f;

    public static float WaveManaFlat => _waveManaFlat?.Value ?? 0f;

    public static bool WaveCostRises => _waveCostRises?.Value ?? false;

    public static float WaveRange => _waveRange?.Value ?? 6f;

    public static int WaveMax => _waveMax?.Value ?? 0;

    public static float CameraSmoothing => _cameraSmoothing?.Value ?? 1f;

    public static bool LogCasts => _logCasts?.Value ?? false;

    // Dreamshift unlocks the mod: without it Dreamslash plays as the game ships it. The node's own effect,
    // consumeShadowsForDreamStacks, is set exactly while it is allocated.
    public static bool Flickering(DreamslashMutator mutator) => mutator != null && mutator.consumeShadowsForDreamStacks && !GameplayEnvironment.IsOnlinePlay;

    public override void OnInitializeMelon() {
        MelonPreferences_Category category = MelonPreferences.CreateCategory("FlickerDreams");
        _reachMultiplier = category.CreateEntry("ReachMultiplier", 4f / 3f, description: "Multiplies Dreamslash's base reach (6, or 8 for shadows) and its dash. 1.3333 makes the first strike reach 8.");
        _repeatChance = category.CreateEntry("RepeatChance", 1f, description: "Chance (1 = 100 %) for each strike to repeat while Dream stacks last. 0 uses Dreamslash's refraction chance instead, halved per repeat with Inception.");
        _repeatStackCost = category.CreateEntry("RepeatStackCost", 1, description: "Dream stacks each repeat spends; without them the chain ends.");
        _repeatRange = category.CreateEntry("RepeatRange", 6f, description: "How far from where a strike lands a repeat looks for its next enemy.");
        _passThrough = category.CreateEntry("PassThrough", 2f, description: "Land this far past the target, so a dash moves you even next to the enemy (Dreamslash stops 1.5 short). 0 keeps the game's offset.");
        _requireTarget = category.CreateEntry("RequireTarget", true, description: "Dreamslash can only be used with an enemy within its reach (or a shadow to consume).");
        _cooldown = category.CreateEntry("Cooldown", 2f, description: "Seconds after each use before Dreamslash is free again; meanwhile a use spends a Dream stack, and without one it is refused. 0 turns it off.");
        _waves = category.CreateEntry("Waves", true, description: "A strike that spent a Dream stack (a press that consumed one, or a repeat) sends sword waves at other enemies near where it lands. Each wave hits only the enemy it was sent at.");
        _waveStyle = category.CreateEntry("WaveStyle", "sword", description: "What a wave is: sword (Dreamslash's own sword wave) or shadowrend (a Shadow Rend shadow appears by the enemy and strikes it 0.8 s later, for Shadow Rend's mana cost each, instead of WaveManaCost and WaveManaFlat).");
        _waveChance = category.CreateEntry("WaveChance", 0f, description: "Chance for a wave per strike (1 = 100 %; every full 100 % is a sure wave). 0 uses Dreamslash's refraction chance. With Inception, each further wave rolls at half the previous chance.");
        _waveManaCost = category.CreateEntry("WaveManaCost", 0.05f, description: "Mana per sword wave, as a fraction of maximum mana (0.05: one wave 5 %, four waves 20 %); waves you cannot pay for do not fire.");
        _waveManaFlat = category.CreateEntry("WaveManaFlat", 10f, description: "Flat mana per sword wave, on top of WaveManaCost.");
        _waveCostRises = category.CreateEntry("WaveCostRises", false, description: "Each further wave of a strike costs one more step: the second twice the first, the third three times (four waves cost 10 steps instead of 4).");
        _waveRange = category.CreateEntry("WaveRange", 6f, description: "How far from where a strike lands a wave looks for an enemy.");
        _waveMax = category.CreateEntry("WaveMax", 4, description: "Most waves one strike can send.");
        _cameraSmoothing = category.CreateEntry("CameraSmoothing", 3f, description: "Makes the camera follow this many times more slowly than the game's Camera Smoothing option (which must not be Off; Full is smoothest). 1 is the game's. Applies when the camera is set up (a new area, or changing the option).");
        _logCasts = category.CreateEntry("LogCasts", false, description: "Log each Dreamslash strike, its Dream stacks, repeats and waves, and refused uses.");
        string repeats = RepeatChance > 0f ? $"{RepeatChance:P0}" : "refraction chance";
        string waves = Waves ? ShadowRendWaves ? "Shadow Rend" : "sword" : "off";
        LoggerInstance.Msg($"Reach x{ReachMultiplier:0.##}; repeats {repeats} for {RepeatStackCost} stack each; waves {waves}; cooldown {Cooldown:0.#}s. Offline only.");
    }
}

// DreamslashMutator scales every range by (1 + GetTotalIncreasedRange()): the target search in mutateTargetLocation
// (base 6, 8 for shadow casts) and getStopRange. The shadow search adds it to 6 instead. The game's own value is 0.2
// with the Dream Aura node while the aura is down, else 0. Rewriting it as m * (1 + x) - 1 multiplies the base reach by m
// and keeps the node's 20 % on top. The aura call (excludeAura) sizes the aura, so it is left alone.
[HarmonyPatch(typeof(DreamslashMutator), nameof(DreamslashMutator.GetTotalIncreasedRange))]
internal static class GetTotalIncreasedRangePatch {
    private static void Postfix(DreamslashMutator __instance, bool excludeAura, ref float __result) {
        if (excludeAura || !FlickerDreamsMod.Flickering(__instance)) {
            return;
        }

        __result = FlickerDreamsMod.ReachMultiplier * (1f + __result) - 1f;
    }
}

// Repeats and waves are recasts through the game's own CreateAbilityObjectOnDeath on the slash object: when the slash ends
// it rolls chanceToCast (0 or less always casts) and casts Dreamslash again at a random enemy near where it landed.
//
// Refraction in the game: a direct cast that consumed a Dream stack arms one such roll at refractionChance; the recast is a
// Special cast, which Mutate treats like noMove: the in-place sword wave. With refractionsCanChain (Inception) a Special
// cast arms the next roll at half the chance. Mutate dashes for every use type except Shadow and Special.
//
// The mod takes that over. Mutate's refraction block only runs while refractionChance > 0, so the prefix sets it to 0 for
// the call and the postfix restores it. Instead:
// - Repeat: a strike that spent a Dream stack arms a Special3 recast at RepeatChance (or the refraction chance, halving
//   with Inception), which dashes; each repeat spends RepeatStackCost Dream stacks, so the chain ends with them.
// - Waves: the same strikes arm a wave caster, a Special recast that WaveCasterPatch turns into one wave per extra enemy.
// - Dash: Mutate writes MoveCreatorToPositionOnDeath.maxMoveRange = 6 * (1 + node bonus) with GetTotalIncreasedRange
//   inlined, so the reach patch never reaches it; ExtendDash scales it and makes the dash a blink.
//
// Ability objects are pooled, so every caster this mod added is removed from a Dreamslash object before anything new is
// added; otherwise an old recast could fire again.
[HarmonyPatch(typeof(DreamslashMutator), nameof(DreamslashMutator.Mutate))]
internal static class MutatePatch {
    private const UseType _repeatUseType = UseType.Special3;

    // The mod's casters carry a mark in manaRequirementOverrideValue, which the game reads only with
    // manaRequirementOverride on (never for these). Unlike a remembered pointer, a mark cannot go stale when an object is
    // destroyed instead of pooled and its memory reused.
    private const float _repeatMark = -7101f;
    private const float _waveMark = -7102f;

    private static float _nextRepeatChance;
    private static bool _faulted;

    // Whether the mod handles this cast, the refraction chance the prefix took away, and whether this cast consumed a
    // Dream stack (Mutate clears that flag).
    internal readonly record struct CastState(bool Active, float RefractionChance, bool ConsumedStack);

    private static void Prefix(DreamslashMutator __instance, UseType useType, out CastState __state) {
        __state = default;
        if (_faulted || !FlickerDreamsMod.Flickering(__instance)) {
            return;
        }

        try {
            __state = new CastState(true, __instance.refractionChance, __instance.consumedDreamStackThisCast);
            __instance.refractionChance = 0f;
            int cost = FlickerDreamsMod.RepeatStackCost;
            if (useType == _repeatUseType && cost > 0) {
                int amount = -cost;
                __instance.ModifyDreamStacks(ref amount);
            }
        } catch (Exception exception) {
            Fault(exception);
        }
    }

    private static void Postfix(DreamslashMutator __instance, GameObject abilityObject, Vector3 targetLocation, UseType useType, GameObject __result, CastState __state) {
        if (__state.Active) {
            __instance.refractionChance = __state.RefractionChance;
        }

        if (_faulted) {
            return;
        }

        try {
            GameObject slash = __result ?? abilityObject;
            if (slash == null) {
                return;
            }

            RemoveAdded(slash);
            if (!__state.Active) {
                return;
            }

            bool direct = useType is UseType.Direct or UseType.InstantDirect or UseType.SummonerCastDirect;
            bool spentStack = (direct && __state.ConsumedStack) || useType == _repeatUseType;
            string dash = ExtendDash(slash);
            string repeat = spentStack ? Repeat(__instance, slash, direct, __state) : "";
            string waves = Waves(__instance, slash, targetLocation, useType, spentStack);
            if (FlickerDreamsMod.LogCasts) {
                MelonLogger.Msg($"Dreamslash {useType}: {__instance.currentDreamStacks} Dream stack(s){dash}{repeat}{waves}");
            }
        } catch (Exception exception) {
            Fault(exception);
        }
    }

    private static string Repeat(DreamslashMutator mutator, GameObject slash, bool direct, CastState state) {
        float chance = FlickerDreamsMod.RepeatChance;
        if (chance <= 0f) {
            chance = direct ? state.RefractionChance : _nextRepeatChance;
            if (!direct && !mutator.refractionsCanChain) {
                return "; chain ends";
            }
        }

        if (chance < 0.005f) {
            return "; chain ends";
        }

        if (mutator.currentDreamStacks < FlickerDreamsMod.RepeatStackCost) {
            return "; no stack to repeat";
        }

        AddCaster(mutator, slash, _repeatUseType, FlickerDreamsMod.RepeatRange, chance, _repeatMark);
        _nextRepeatChance = chance * 0.5f;
        return $"; repeat {chance:P0}";
    }

    // MoveCreatorToPositionOnDeath moves you to the target shifted by offsetDistanceFromTarget: a positive offset stops that
    // far short (Dreamslash uses 1.5), a negative one lands past the target by its radius plus the offset. The changes:
    // maxMoveRange scaled like the reach (Mutate sets it on every cast, so this never compounds on a pooled object);
    // PassThrough; no onlyMoveWithinRange gate (it cancels the dash for targets outside the dash's min/max range); and a
    // blink: moveOnStartInstead puts you at the landing point as the slash starts instead of sliding along it.
    private static string ExtendDash(GameObject slash) {
        MoveCreatorToPositionOnDeath move = slash.GetComponent<MoveCreatorToPositionOnDeath>();
        if (move == null) {
            return "";
        }

        move.maxMoveRange *= FlickerDreamsMod.ReachMultiplier;
        if (FlickerDreamsMod.PassThrough > 0f) {
            move.offsetDistanceFromTarget = -FlickerDreamsMod.PassThrough;
        }

        move.onlyMoveWithinRange = false;
        move.moveOnStartInstead = true;
        move.moveOverDurationInstead = false;
        return $"; dash {move.maxMoveRange:0.#}";
    }

    // Sword waves are the game's refraction: a Special cast of Dreamslash, which slashes in place and sends a wave (Mutate
    // turns the dash off for it). A strike that spent a Dream stack carries one wave caster; when the slash ends,
    // WaveCasterPatch decides how many waves it sends. Each wave is aimed at an enemy (targetLocation is that enemy's
    // position) and WaveHitPatch lets it hit only that enemy, so it no longer cuts through the pack.
    private static string Waves(DreamslashMutator mutator, GameObject slash, Vector3 targetLocation, UseType useType, bool spentStack) {
        if (!FlickerDreamsMod.Waves) {
            return "";
        }

        if (useType == UseType.Special) {
            return WaveHitPatch.LockWave(mutator, slash, targetLocation);
        }

        if (!spentStack) {
            return "";
        }

        AddCaster(mutator, slash, UseType.Special, FlickerDreamsMod.WaveRange, 0f, _waveMark);
        return "; waves armed";
    }

    private static void AddCaster(DreamslashMutator mutator, GameObject slash, UseType useType, float range, float chance, float mark) {
        CreateAbilityObjectOnDeath caster = slash.AddComponent<CreateAbilityObjectOnDeath>();
        caster.abilityToInstantiate = mutator.ability;
        caster.mutatorOverride = mutator;
        caster.useType = useType;
        caster.aimingMethod = CreateAbilityObjectOnDeath.AimingMethod.Random;
        caster.aimingDistance = range;
        caster.fixedYForwards = true;
        caster.targetEnemiesInsteadOfLocation = true;
        caster.targetLocationIfNoEnemy = false;
        caster.mustTargetDifferentEnemies = true;
        caster.chanceToCast = chance;
        caster.manaRequirementOverrideValue = mark;
    }

    internal static bool IsWaveCaster(CreateAbilityObjectOnDeath caster) => caster.manaRequirementOverrideValue == _waveMark;

    private static void RemoveAdded(GameObject slash) {
        foreach (CreateAbilityObjectOnDeath caster in slash.GetComponents<CreateAbilityObjectOnDeath>()) {
            if (caster.manaRequirementOverrideValue is _repeatMark or _waveMark) {
                UnityEngine.Object.DestroyImmediate(caster);
            }
        }

        WaveHitPatch.Unlock(slash);
    }

    private static void Fault(Exception exception) {
        _faulted = true;
        MelonLogger.Error($"Flicker Dreams stopped changing Dreamslash: {exception}");
    }
}

// A wave caster fires when its slash ends: CreateAbilityObjectOnDeath.createAbilityObject casts 1 + additionalCasts times,
// each at a random enemy within aimingDistance of the slash (mustTargetDifferentEnemies keeps them apart). The prefix
// settles the count there, where the landing point, the enemies and the mana are known: the rolls (WaveChance, or the
// refraction chance; Inception chains at half the chance), capped by WaveMax, by the mana and by the enemies in reach.
// It pays for those waves only, and skips the cast when there are none. Shadow Rend waves are spawned here, one per
// enemy found, and the caster's own sword-wave cast is skipped.
[HarmonyPatch(typeof(CreateAbilityObjectOnDeath), nameof(CreateAbilityObjectOnDeath.createAbilityObject))]
internal static class WaveCasterPatch {
    private static bool _faulted;

    private static bool Prefix(CreateAbilityObjectOnDeath __instance) {
        if (!MutatePatch.IsWaveCaster(__instance)) {
            return true;
        }

        if (_faulted || GameplayEnvironment.IsOnlinePlay) {
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

            Ability rend = FlickerDreamsMod.ShadowRendWaves ? ShadowRendWave.Find(player) : null;
            float step = rend != null
                ? mana.getManaCost(rend, 0f, false, null, false)
                : FlickerDreamsMod.WaveManaCost * mana.maxMana + FlickerDreamsMod.WaveManaFlat;
            int affordable = 0;
            while (affordable < rolled && Cost(step, affordable + 1) <= mana.currentMana) {
                affordable++;
            }

            Il2CppSystem.Collections.Generic.List<Actor> targets = EnemiesNear(player, __instance.transform.position, affordable);
            int waves = targets.Count;
            float cost = Cost(step, waves);
            if (FlickerDreamsMod.LogCasts) {
                MelonLogger.Msg($"Dreamslash {(rend != null ? "Shadow Rend " : "")}waves: rolled {rolled}, mana for {affordable} -> {waves} (enemies in {FlickerDreamsMod.WaveRange:0.#}), cost {cost:0.#} of {mana.currentMana:0} mana");
            }

            if (waves <= 0) {
                return false;
            }

            if (cost > 0f) {
                mana.reduceMana(cost);
            }

            if (rend != null) {
                for (int i = 0; i < targets.Count; i++) {
                    ShadowRendWave.Spawn(player, rend, targets[i]);
                }

                return false;
            }

            __instance.additionalCasts = waves - 1;
            __instance.randomExtraCasts = 0;
            return true;
        } catch (Exception exception) {
            _faulted = true;
            MelonLogger.Error($"Flicker Dreams stopped sending waves: {exception}");
            return false;
        }
    }

    // n waves cost n steps, or 1 + 2 + ... + n steps when each further wave costs more.
    private static float Cost(float step, int waves) => step * (FlickerDreamsMod.WaveCostRises ? waves * (waves + 1) / 2 : waves);

    // Every full 100 % is a sure wave and the rest is a roll; with Inception (refractionsCanChain) each further wave rolls at
    // half the previous chance until one fails. Never more than WaveMax.
    private static int Roll(DreamslashMutator mutator) {
        float chance = FlickerDreamsMod.WaveChance > 0f ? FlickerDreamsMod.WaveChance : mutator.refractionChance;
        if (chance <= 0f) {
            return 0;
        }

        int waves = (int)chance;
        if (UnityEngine.Random.value < chance - waves) {
            waves++;
        }

        if (waves > 0 && mutator.refractionsCanChain) {
            for (float next = chance * 0.5f; waves < FlickerDreamsMod.WaveMax && UnityEngine.Random.value < next; next *= 0.5f) {
                waves++;
            }
        }

        return Math.Min(waves, FlickerDreamsMod.WaveMax);
    }

    private static Il2CppSystem.Collections.Generic.List<Actor> EnemiesNear(Actor player, Vector3 location, int limit) {
        Alignment alignment = player.GetAlignment();
        var found = new Il2CppSystem.Collections.Generic.List<Actor>();
        Il2CppSystem.Collections.Generic.IEnumerable<Actor> exclude = found.Cast<Il2CppSystem.Collections.Generic.IEnumerable<Actor>>();
        float range = FlickerDreamsMod.WaveRange;
        while (found.Count < limit && alignment.TryGetRandomEnemyInRangeOfLocation(ref location, ref range, out Actor enemy, exclude) && enemy != null) {
            found.Add(enemy);
        }

        return found;
    }
}

// Shadow Rend's shadow attack is not an ability of its own: the Shadow Rend object (881) waits 0.8 s, then casts its 500 %
// sweep (ShadowRend Damage, 883) and the shadow creator. The game spawns just that from code (CharacterMutator.OnHit
// builds 881 with the player's AbilityObjectConstructor 3 from an enemy, with a "no small hit" use type), and so does
// this. With Special3, ShadowRendMutator.Mutate drops the initial small hit and has the created shadow consumed at once,
// so none lingers. Every Rogue carries ShadowRendMeleeMutator (the tree only writes its fields), so this works whether
// Shadow Rend is specced or not, with its nodes. Building the object charges no mana; WaveCasterPatch pays Shadow Rend's
// cost (BaseMana.getManaCost) per shadow. While a wave is being built the shadow's own dash nodes are switched off, and
// its sweep is locked onto the wave's enemy.
internal static class ShadowRendWave {
    private const float _distance = 3f;

    private static readonly ExpiringMap _targets = new(3f);
    private static Ability _ability;
    private static IntPtr _pending;

    public static bool Building => _pending != IntPtr.Zero;

    public static Ability Find(Actor player) {
        if (player.abilityObjectConstructor == null || (player.GetComponent<ShadowRendMeleeMutator>() ?? player.GetComponentInChildren<ShadowRendMeleeMutator>()) == null) {
            return null;
        }

        _ability ??= Ability.getAbility(AbilityID.shadowRend);
        return _ability;
    }

    public static void Spawn(Actor player, Ability rend, Actor target) {
        Vector3 enemy = target.position();
        Vector3 away = player.position() - enemy;
        away.y = 0f;
        Vector3 toward = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
        Vector3 spot = NavMeshHelper.getFurthestPointTowardsPoint(enemy, enemy + toward * _distance);
        _pending = target.Pointer;
        try {
            player.abilityObjectConstructor.constructAbilityObject(rend, spot, enemy, UseType.Special3, null, true, false, null, false, null, 0f, AbilityID.none, false, Vector3.zero);
        } finally {
            _pending = IntPtr.Zero;
        }
    }

    public static void Built(GameObject shadow) {
        if (shadow != null) {
            _targets.Set(shadow.Pointer, _pending);
        }
    }

    public static void Forget(GameObject shadow) {
        if (shadow != null) {
            _targets.Remove(shadow.Pointer);
        }
    }

    public static bool TargetOf(GameObject shadow, out IntPtr target) {
        target = IntPtr.Zero;
        return shadow != null && _targets.TryGet(shadow.Pointer, out target);
    }
}

// Mutate of a Shadow Rend object: for the mod's wave, the dash nodes (shadowForwardDash, shadowRandomDash: the shadow
// dashes up to 5 to a new target) are off for the call, and the object is remembered with its enemy. Any other use of a
// pooled object forgets it.
[HarmonyPatch(typeof(ShadowRendMeleeMutator), nameof(ShadowRendMeleeMutator.Mutate))]
internal static class ShadowRendMutatePatch {
    internal readonly record struct Dashes(bool Active, bool Forward, bool Random);

    private static void Prefix(ShadowRendMeleeMutator __instance, GameObject abilityObject, out Dashes __state) {
        __state = default;
        if (!ShadowRendWave.Building) {
            ShadowRendWave.Forget(abilityObject);
            return;
        }

        __state = new Dashes(true, __instance.shadowForwardDash, __instance.shadowRandomDash);
        __instance.shadowForwardDash = false;
        __instance.shadowRandomDash = false;
    }

    private static void Postfix(ShadowRendMeleeMutator __instance, GameObject abilityObject, GameObject __result, Dashes __state) {
        if (!__state.Active) {
            return;
        }

        __instance.shadowForwardDash = __state.Forward;
        __instance.shadowRandomDash = __state.Random;
        ShadowRendWave.Built(__result ?? abilityObject);
    }
}

// The sweep (883) is built by the Shadow Rend object's own constructor, so its ShadowRendDamageMutator sits on that object:
// a wave's sweep hits only the wave's enemy.
[HarmonyPatch(typeof(ShadowRendDamageMutator), nameof(ShadowRendDamageMutator.Mutate))]
internal static class ShadowRendDamagePatch {
    private static bool _faulted;

    private static void Postfix(ShadowRendDamageMutator __instance, GameObject abilityObject, GameObject __result) {
        if (_faulted) {
            return;
        }

        try {
            if (ShadowRendWave.TargetOf(__instance.gameObject, out IntPtr target)) {
                WaveHitPatch.Lock(__result ?? abilityObject, target);
            }
        } catch (Exception exception) {
            _faulted = true;
            MelonLogger.Error($"Flicker Dreams stopped locking Shadow Rend waves: {exception}");
        }
    }
}

// A wave hits only the enemy it was sent at. HitDetector.hitActor is where every hit of an ability object starts (its
// trigger, its first tick, and abilities that hit for it), so the prefix refuses any other actor for a locked hit. A sword
// wave keeps flying to its target location and fades as usual. Locks are by HitDetector and cleared when a pooled sword
// wave is mutated again.
[HarmonyPatch(typeof(HitDetector), nameof(HitDetector.hitActor))]
internal static class WaveHitPatch {
    private const float _targetSnap = 2f;

    private static readonly ExpiringMap _locks = new(3f);
    private static bool _faulted;

    // A sword wave is aimed at an enemy: targetLocation is that enemy's position.
    public static string LockWave(DreamslashMutator mutator, GameObject wave, Vector3 targetLocation) {
        Actor player = mutator.actor;
        if (player == null || !CharacterMutator.TryGetNearestEnemy(targetLocation, player.GetAlignment(), _targetSnap, 0f, out Actor target) || target == null) {
            return "; wave found no enemy at its target, so it hits everything";
        }

        return Lock(wave, target.Pointer) ? "; wave locked on its target" : "; wave has no HitDetector, so it hits everything";
    }

    public static bool Lock(GameObject hit, IntPtr target) {
        HitDetector detector = Detector(hit);
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
        if (_faulted || _locks.Count == 0 || actor == null) {
            return true;
        }

        try {
            return !_locks.TryGet(__instance.Pointer, out IntPtr target) || actor.Pointer == target;
        } catch (Exception exception) {
            _faulted = true;
            MelonLogger.Error($"Flicker Dreams stopped locking waves: {exception}");
            return true;
        }
    }
}

// Game object pointer -> target pointer for a few seconds (far longer than a wave lives). The expiry keeps an entry from
// outliving an object that is destroyed instead of pooled, whose memory something else may reuse.
internal sealed class ExpiringMap(float seconds) {
    private readonly Dictionary<IntPtr, (IntPtr Value, float Until)> _entries = [];

    public int Count => _entries.Count;

    public void Set(IntPtr key, IntPtr value) {
        float now = Time.time;
        if (_entries.Count > 32) {
            var expired = new List<IntPtr>();
            foreach (KeyValuePair<IntPtr, (IntPtr Value, float Until)> entry in _entries) {
                if (entry.Value.Until < now) {
                    expired.Add(entry.Key);
                }
            }

            expired.ForEach(expiredKey => _entries.Remove(expiredKey));
        }

        _entries[key] = (value, now + seconds);
    }

    public bool TryGet(IntPtr key, out IntPtr value) {
        value = IntPtr.Zero;
        if (!_entries.TryGetValue(key, out (IntPtr Value, float Until) entry)) {
            return false;
        }

        if (entry.Until < Time.time) {
            _entries.Remove(key);
            return false;
        }

        value = entry.Value;
        return true;
    }

    public void Remove(IntPtr key) => _entries.Remove(key);
}

// Flicker Strike needs a target and has a cooldown that a Frenzy charge bypasses. UsingAbilityPlayer.InitialiseAbilityUse is
// where a player's press becomes an ability use (it takes the target by value; a prefix returning false refuses the use
// before mana, animation or the cast). For Dreamslash, offline:
// - RequireTarget: refused unless the game's own enemy search finds an enemy within Dreamslash's reach of the player.
// - Cooldown: every use starts it; while it runs a use needs a Dream stack (which the press spends as it always does), and
//   without one it is refused with the game's on-cooldown response. Repeats are recasts, not presses, and pay their own stack.
// A use that will consume a shadow passes both.
[HarmonyPatch(typeof(UsingAbilityPlayer), nameof(UsingAbilityPlayer.InitialiseAbilityUse),
    [typeof(Ability), typeof(Vector3), typeof(bool), typeof(bool), typeof(bool), typeof(float)])]
internal static class InitialiseAbilityUsePatch {
    private const float _baseReach = 6f;

    private static DreamslashMutator _mutator;
    private static float _readyAt;
    private static readonly Dictionary<string, float> _lastRefusalAt = [];
    private static bool _faulted;

    private static bool Prefix(UsingAbilityPlayer __instance, Ability _ability, Vector3 target, bool ignoreCanUseAbilityChecks, bool pressed, ref bool __result) {
        if (_faulted || ignoreCanUseAbilityChecks || _ability == null || GameplayEnvironment.IsOnlinePlay) {
            return true;
        }

        try {
            DreamslashMutator mutator = Mutator(__instance);
            if (mutator == null || mutator.ability == null || mutator.ability.Pointer != _ability.Pointer || !FlickerDreamsMod.Flickering(mutator)) {
                return true;
            }

            Actor player = PlayerFinder.getPlayerActor();
            if (player == null) {
                return true;
            }

            // A use that will consume a shadow has a target (the enemy by the shadow) and brings its own Dream stack.
            bool shadow = ShadowToConsume(mutator, player);
            if (FlickerDreamsMod.RequireTarget && !shadow) {
                Vector3 position = player.position();
                float reach = _baseReach * (1f + mutator.GetTotalIncreasedRange(false));
                if (!player.GetAlignment().TryGetRandomEnemyInRangeOfLocation(ref position, ref reach, out Actor _, null)) {
                    LogRefusal($"no enemy within {reach:0.##}");
                    __result = false;
                    return false;
                }
            }

            float left = _readyAt - Time.time;
            if (FlickerDreamsMod.Cooldown > 0f && left > 0f && mutator.currentDreamStacks <= 0 && !shadow) {
                __instance.triedToUseAbilityOnCooldown(mutator.Cast<AbilityInfo>(), target, pressed);
                LogRefusal($"cooldown {left:0.0}s and no Dream stack");
                __result = false;
                return false;
            }

            return true;
        } catch (Exception exception) {
            _faulted = true;
            MelonLogger.Error($"Flicker Dreams stopped gating Dreamslash: {exception}");
            return true;
        }
    }

    private static void Postfix(Ability _ability, bool __result) {
        if (!__result || _faulted || _mutator == null || _ability == null || FlickerDreamsMod.Cooldown <= 0f) {
            return;
        }

        if (_mutator.ability != null && _mutator.ability.Pointer == _ability.Pointer) {
            _readyAt = Time.time + FlickerDreamsMod.Cooldown;
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

        float range = _baseReach + mutator.GetTotalIncreasedRange(false);
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
        if (!FlickerDreamsMod.LogCasts) {
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

    // The Dreamslash mutator sits on the player with the ability state; cached per player object.
    private static DreamslashMutator Mutator(UsingAbilityPlayer state) {
        if (_mutator != null && !_mutator.WasCollected && _mutator.gameObject != null && _mutator.gameObject.Pointer == state.gameObject.Pointer) {
            return _mutator;
        }

        _mutator = state.GetComponent<DreamslashMutator>() ?? state.GetComponentInChildren<DreamslashMutator>();
        return _mutator;
    }
}

// The gameplay camera follows the player with CameraFollow: each frame it moves toward the player by
// distance * smoothingFactor * deltaTime, and snaps outright beyond scaledMoveTowardsSnapThreshold. The game's Camera Smoothing
// option sets these in UpdateCameraSmoothing (inlined into Start as well): Off turns the distance scaling off (the camera
// snaps), Low uses factor 35 and threshold 25, Full factor 10 and threshold 50. A lower factor follows more slowly, so the
// postfixes divide it by CameraSmoothing and the camera eases after a blink instead of jumping with it. Both set the factor
// from constants on every call, so the division never compounds.
[HarmonyPatch(typeof(CameraFollow), nameof(CameraFollow.UpdateCameraSmoothing))]
internal static class UpdateCameraSmoothingPatch {
    private static void Postfix(CameraFollow __instance) {
        CameraSmoothingTweak.Apply(__instance);
    }
}

[HarmonyPatch(typeof(CameraFollow), nameof(CameraFollow.Start))]
internal static class CameraFollowStartPatch {
    private static void Postfix(CameraFollow __instance) {
        CameraSmoothingTweak.Apply(__instance);
    }
}

internal static class CameraSmoothingTweak {
    public static void Apply(CameraFollow follow) {
        float factor = FlickerDreamsMod.CameraSmoothing;
        if (follow == null || factor <= 0f || factor == 1f || GameplayEnvironment.IsOnlinePlay) {
            return;
        }

        try {
            if (!follow.distanceScalingMoveTowards) {
                MelonLogger.Msg("Camera follow: the game's Camera Smoothing option is Off (the camera snaps); set it to Full for CameraSmoothing to work");
                return;
            }

            follow.smoothingFactor /= factor;
        } catch (Exception exception) {
            MelonLogger.Error($"Flicker Dreams could not change camera smoothing: {exception}");
        }
    }
}

// Dreamslash tree (dr4sl) tooltips for the nodes the mod changes: Dreamshift (24) unlocks it, Shared Memories (19) and
// Inception (28) work differently with it. All node text goes through LocalizationSkills (both tooltip versions, every
// language, the tree search); in English it returns originalValue. The postfixes append the mod's text, built from the
// current settings. The decorated boxes are the node's stat lines (SkillTreeNode.stats), which DreamslashStatLines adds.
internal static class DreamslashTooltips {
    public const string TreeId = "dr4sl";
    public const byte SharedMemories = 19;
    public const byte Dreamshift = 24;
    public const byte Inception = 28;
    private const float _baseReach = 6f;

    public static bool Changed(byte nodeID) => nodeID is SharedMemories or Dreamshift or Inception;

    public static bool Applies(string treeID, byte nodeID) => treeID == TreeId && Changed(nodeID) && !GameplayEnvironment.IsOnlinePlay;

    public static string Description(byte nodeID) => nodeID switch {
        Dreamshift => $"Dreamslash also becomes a flicker: it can only be used with an enemy within {Meters(_baseReach * FlickerDreamsMod.ReachMultiplier)}, and a strike that consumes a {{Dream}} stack {Repeats()} at another enemy within {Meters(FlickerDreamsMod.RepeatRange)}, consuming {Stacks(FlickerDreamsMod.RepeatStackCost)}, until your stacks run out."
            + (FlickerDreamsMod.Cooldown > 0f ? $" Each use starts a {Seconds(FlickerDreamsMod.Cooldown)} cooldown; while it runs, using Dreamslash consumes a {{Dream}} stack, and without one you cannot use it." : ""),
        SharedMemories => FlickerDreamsMod.Waves
            ? $"With Dreamshift, Dreamslash no longer refracts. Instead, a strike that consumes a {{Dream}} stack has {WaveChance()} to send a {Wave()} at another enemy within {Meters(FlickerDreamsMod.WaveRange)}, for {WaveCost()}. It hits only that enemy."
            : "With Dreamshift, Dreamslash no longer refracts.",
        Inception => FlickerDreamsMod.Waves
            ? $"With Dreamshift, a strike can send up to {FlickerDreamsMod.WaveMax} {Wave()}s, each at a different enemy. Each one after the first rolls at half the previous chance."
            : "",
        _ => "",
    };

    public static string AltText(byte nodeID) => nodeID switch {
        Dreamshift => $"With Dreamshift, you blink to {Meters(FlickerDreamsMod.PassThrough)} past each target instead of sliding to it. A use that consumes a shadow ignores the cooldown and needs no enemy near you.",
        SharedMemories => FlickerDreamsMod.Waves ? $"With Dreamshift, {Wave()}s you cannot pay for are not sent, and a strike that did not consume a Dream stack sends none." : "",
        _ => "",
    };

    // The decorated boxes: text, and whether it is a drawback (shown red).
    public static List<(string Text, bool Downside)> Boxes(byte nodeID) {
        var boxes = new List<(string Text, bool Downside)>();
        switch (nodeID) {
            case Dreamshift:
                boxes.Add(("Strikes Again at another Enemy per Dream Stack", false));
                if (FlickerDreamsMod.Cooldown > 0f) {
                    boxes.Add(($"{FlickerDreamsMod.Cooldown:0.#} Second Cooldown, ignored with a Dream Stack", true));
                }

                break;
            case SharedMemories:
                boxes.Add(FlickerDreamsMod.Waves ? ($"With Dreamshift: {Title(Wave())} at another Enemy for Mana", false) : ("With Dreamshift: No Refraction", true));
                break;
            case Inception when FlickerDreamsMod.Waves:
                boxes.Add(($"With Dreamshift: up to {FlickerDreamsMod.WaveMax} {Title(Wave())}s per Strike", false));
                break;
            default:
                break;
        }

        return boxes;
    }

    public static string Append(string original, string added) =>
        string.IsNullOrEmpty(added) ? original : string.IsNullOrWhiteSpace(original) ? added : $"{original}\n\n{added}";

    private static string Repeats() {
        float chance = FlickerDreamsMod.RepeatChance;
        return chance >= 1f ? "instantly strikes again"
            : chance > 0f ? $"has a {chance:P0} chance to strike again"
            : "may strike again (at the refraction chance, halved per repeat with Inception)";
    }

    private static string WaveChance() => FlickerDreamsMod.WaveChance > 0f ? $"a {FlickerDreamsMod.WaveChance:P0} chance (not this node's)" : "this chance";

    private static string Wave() => FlickerDreamsMod.ShadowRendWaves ? "Shadow Rend strike" : "sword wave";

    private static string WaveCost() {
        if (FlickerDreamsMod.ShadowRendWaves) {
            return "Shadow Rend's mana cost" + Rises();
        }

        float percent = FlickerDreamsMod.WaveManaCost * 100f;
        float flat = FlickerDreamsMod.WaveManaFlat;
        string cost = percent > 0f && flat > 0f ? $"{percent:0.#}% of your maximum mana plus {flat:0.#} mana"
            : percent > 0f ? $"{percent:0.#}% of your maximum mana"
            : flat > 0f ? $"{flat:0.#} mana"
            : "no mana";
        return cost + Rises();
    }

    private static string Rises() => FlickerDreamsMod.WaveCostRises ? " (each further one in a strike costs one step more: the second twice, the third three times)" : "";

    private static string Meters(float meters) => $"{meters:0.#} meters";

    private static string Seconds(float seconds) => seconds == 1f ? "1 second" : $"{seconds:0.#} second";

    private static string Stacks(int stacks) => stacks == 1 ? "another stack" : $"{stacks} more stacks";

    private static string Title(string text) => string.Join(' ', text.Split(' ').Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
}

[HarmonyPatch(typeof(LocalizationSkills), nameof(LocalizationSkills.GetNodeDescription))]
internal static class NodeDescriptionPatch {
    private static void Postfix(string treeID, byte nodeID, ref string __result) {
        if (DreamslashTooltips.Applies(treeID, nodeID)) {
            __result = DreamslashTooltips.Append(__result, DreamslashTooltips.Description(nodeID));
        }
    }
}

[HarmonyPatch(typeof(LocalizationSkills), nameof(LocalizationSkills.GetNodeAltText))]
internal static class NodeAltTextPatch {
    private static void Postfix(string treeID, byte nodeID, ref string __result) {
        if (DreamslashTooltips.Applies(treeID, nodeID)) {
            __result = DreamslashTooltips.Append(__result, DreamslashTooltips.AltText(nodeID));
        }
    }
}

// The node's stat lines are display only (the tree applies gameplay from its own data), so the boxes are added to the
// node's stats right before a tooltip reads them: in the current tooltip (NodeTooltipContentBuilder.GetNodeTooltipInfo)
// and the older one (NodeTooltipManager.setInfoForNode). Lines added before are recognised by their text and replaced,
// so settings changes show and nothing piles up; online they are only removed. A box copies its icon from the node's
// first line and has no value (no number to scale with points).
internal static class DreamslashStatLines {
    private static readonly HashSet<string> _added = [];
    private static readonly HashSet<byte> _logged = [];
    private static bool _faulted;

    public static void Apply(SkillTreeNode node) {
        if (_faulted || node == null || node.tree == null || node.tree.treeID != DreamslashTooltips.TreeId) {
            return;
        }

        try {
            byte id = node.getID();
            Il2CppSystem.Collections.Generic.List<SkillTreeNode.NodeTooltipStat> stats = node.stats;
            if (!DreamslashTooltips.Changed(id) || stats == null) {
                return;
            }

            for (int i = stats.Count - 1; i >= 0; i--) {
                if (stats[i] != null && _added.Contains(stats[i].statName)) {
                    stats.RemoveAt(i);
                }
            }

            Log(node, id, stats);
            if (GameplayEnvironment.IsOnlinePlay) {
                return;
            }

            SkillTreeNode.NodeTooltipStat template = stats.Count > 0 ? stats[0] : null;
            foreach ((string text, bool downside) in DreamslashTooltips.Boxes(id)) {
                var box = new SkillTreeNode.NodeTooltipStat {
                    statName = text,
                    value = "",
                    noScaling = false,
                    downside = downside,
                };
                if (template != null) {
                    box.property = template.property;
                    box.tags = template.tags;
                    box.spriteSoftRef = template.spriteSoftRef;
                    box.overrideSprite = template.overrideSprite;
                }

                stats.Add(box);
                _added.Add(text);
            }
        } catch (Exception exception) {
            _faulted = true;
            MelonLogger.Error($"Flicker Dreams stopped changing Dreamslash node tooltips: {exception}");
        }
    }

    // What the game's own lines look like, once per node, for tuning the boxes.
    private static void Log(SkillTreeNode node, byte id, Il2CppSystem.Collections.Generic.List<SkillTreeNode.NodeTooltipStat> stats) {
        if (!FlickerDreamsMod.LogCasts || !_logged.Add(id)) {
            return;
        }

        var lines = new List<string>();
        for (int i = 0; i < stats.Count; i++) {
            SkillTreeNode.NodeTooltipStat stat = stats[i];
            lines.Add($"'{stat.statName}' = '{stat.value}' noScaling {stat.noScaling} downside {stat.downside} property {stat.property} tags {stat.tags}");
        }

        MelonLogger.Msg($"Dreamslash node {id} ({node.maxPoints} max points) lines: {string.Join("; ", lines)}");
    }
}

[HarmonyPatch(typeof(NodeTooltipContentBuilder), nameof(NodeTooltipContentBuilder.GetNodeTooltipInfo))]
internal static class NodeTooltipInfoPatch {
    private static void Prefix(SkillTreeNode node) => DreamslashStatLines.Apply(node);
}

[HarmonyPatch(typeof(NodeTooltipManager), nameof(NodeTooltipManager.setInfoForNode))]
internal static class NodeTooltipManagerPatch {
    private static void Prefix(SkillTreeNode node) => DreamslashStatLines.Apply(node);
}
