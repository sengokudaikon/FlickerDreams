using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppEHG.Multiplayer;
using MelonLoader;

namespace FlickerDreams;

// The gameplay camera follows the player with CameraFollow: each frame it moves toward the player by
// distance * smoothingFactor * deltaTime, and snaps outright beyond scaledMoveTowardsSnapThreshold. The game's Camera Smoothing
// option sets these in UpdateCameraSmoothing (inlined into Start as well): Off turns the distance scaling off (the camera
// snaps), Low uses factor 35 and threshold 25, Full factor 10 and threshold 50. A lower factor follows more slowly, so the
// postfixes divide it by CameraSmoothing and the camera eases after a blink instead of jumping with it. Both set the factor
// from constants on every call, so the division never compounds. Not gated on Dreamshift: it is a camera option (1 turns it off).
[HarmonyPatch(typeof(CameraFollow), nameof(CameraFollow.UpdateCameraSmoothing))]
internal static class UpdateCameraSmoothingPatch {
    private static void Postfix(CameraFollow __instance) => CameraSmoothing.Apply(__instance);
}

[HarmonyPatch(typeof(CameraFollow), nameof(CameraFollow.Start))]
internal static class CameraFollowStartPatch {
    private static void Postfix(CameraFollow __instance) => CameraSmoothing.Apply(__instance);
}

internal static class CameraSmoothing {
    public static void Apply(CameraFollow follow) {
        float factor = Settings.CameraSmoothing;
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
