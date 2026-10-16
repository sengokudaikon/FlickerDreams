using MelonLoader;

namespace FlickerDreams;

// UserData/MelonPreferences.cfg, category FlickerDreams. Values are read on every use, so a saved edit applies on the next
// cast (CameraSmoothing at the next camera setup). ModSettings.json describes the same keys for Eterra's Gift.
internal static class Settings {
    private static MelonPreferences_Entry<float> _reachMultiplier;
    private static MelonPreferences_Entry<float> _cooldown;
    private static MelonPreferences_Entry<float> _waveManaCost;
    private static MelonPreferences_Entry<float> _waveManaFlat;
    private static MelonPreferences_Entry<float> _cameraSmoothing;
    private static MelonPreferences_Entry<bool> _logCasts;

    public static float ReachMultiplier => _reachMultiplier?.Value ?? 1f;

    public static float Cooldown => _cooldown?.Value ?? 0f;

    public static float WaveManaCost => _waveManaCost?.Value ?? 0f;

    public static float WaveManaFlat => _waveManaFlat?.Value ?? 0f;

    public static float CameraSmoothing => _cameraSmoothing?.Value ?? 1f;

    public static bool LogCasts => _logCasts?.Value ?? false;

    public static void Load() {
        MelonPreferences_Category category = MelonPreferences.CreateCategory("FlickerDreams");
        _reachMultiplier = category.CreateEntry("ReachMultiplier", 4f / 3f,
            description: "Multiplies Dreamslash's base reach (6, or 8 for shadows), its dash and how far its sword waves fly. 1.3333 makes the first strike reach 8.");
        _cooldown = category.CreateEntry("Cooldown", 2f,
            description: "Seconds after each use before Dreamslash is free again; meanwhile a use spends a Dream stack, and without one it is refused. 0 turns it off.");
        _waveManaCost = category.CreateEntry("WaveManaCost", 0.05f,
            description: "Mana per sword wave, as a fraction of maximum mana (0.05: one wave 5 %, four waves 20 %); waves you cannot pay for are not sent.");
        _waveManaFlat = category.CreateEntry("WaveManaFlat", 10f, description: "Flat mana per sword wave, on top of WaveManaCost.");
        _cameraSmoothing = category.CreateEntry("CameraSmoothing", 3f,
            description: "Makes the camera follow this many times more slowly than the game's Camera Smoothing option (which must not be Off; Full is smoothest). 1 is the game's. Applies when the camera is set up (a new area, or changing the option).");
        _logCasts = category.CreateEntry("LogCasts", false, description: "Log each Dreamslash strike, its Dream stacks, repeats and waves, and refused uses.");
    }
}
