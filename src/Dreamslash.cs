using System;
using Il2Cpp;
using Il2CppEHG.Multiplayer;
using MelonLoader;

namespace FlickerDreams;

// The numbers Flicker Dreams builds on, and the gate every gameplay change checks.
internal static class Dreamslash {
    // DreamslashMutator.BASE_MAX_DISTANCE: the reach before range bonuses (8 for shadow casts).
    public const float BaseReach = 6f;

    // How far from where a strike lands its repeat and its sword waves look for enemies.
    public const float SearchRange = 6f;

    // A blink lands this far past its target, so it moves you even next to the enemy (the game stops 1.5 short).
    public const float PassThrough = 2f;

    // Most sword waves one strike sends (Inception's chain).
    public const int MostWaves = 4;

    // Figments of Annihilation counts up to this many repeats, as many as the shadows the game allows at once.
    public const int MostFigmentSteps = 3;

    // Dreamshift unlocks the mod: without it Dreamslash plays as the game ships it. The node's own effect,
    // consumeShadowsForDreamStacks, is set exactly while it is allocated.
    public static bool Flickering(DreamslashMutator mutator) => mutator != null && mutator.consumeShadowsForDreamStacks && !GameplayEnvironment.IsOnlinePlay;
}

// Each patch stops changing the game after its first exception, and says so once.
internal sealed class Fault(string what) {
    public bool Stopped { get; private set; }

    public void Report(Exception exception) {
        Stopped = true;
        MelonLogger.Error($"Flicker Dreams stopped {what}: {exception}");
    }
}
