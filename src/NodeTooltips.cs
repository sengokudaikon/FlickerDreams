using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppEHG.Multiplayer;
using Il2CppLE.UI.Tooltips;
using Il2CppLocalizationSystem;
using MelonLoader;

namespace FlickerDreams;

// Dreamslash tree (dr4sl) tooltips for the nodes the mod changes: Dreamshift (24) unlocks it, Shared Memories (19) and
// Inception (28) work differently with it. All node text goes through LocalizationSkills (both tooltip versions, every
// language, the tree search); in English it returns originalValue. The postfixes rewrite or extend it with the mod's text,
// built from the current settings. The decorated boxes are the node's stat lines (SkillTreeNode.stats), which NodeStatLines adds.
internal static class NodeTooltips {
    public const string TreeId = "dr4sl";
    public const byte SharedMemories = 19;
    public const byte Dreamshift = 24;
    public const byte Inception = 28;

    public static bool Changed(byte nodeID) => nodeID is SharedMemories or Dreamshift or Inception;

    public static bool Applies(string treeID, byte nodeID) => treeID == TreeId && Changed(nodeID) && !GameplayEnvironment.IsOnlinePlay;

    // Dreamshift's own text says a use that consumes a shadow starts from the shadow's location, which the blink makes untrue,
    // so the mod replaces it; the other two keep the game's text and gain a "With Dreamshift" paragraph.
    public static string Description(byte nodeID, string original) => nodeID switch {
        Dreamshift => string.Join("\n\n", DreamshiftDescription()),
        SharedMemories => Append(original,
            $"With Dreamshift: instead of refracting, each strike that consumes a {{Dream}} stack has this chance to send a sword wave at another enemy within {Meters(Dreamslash.SearchRange)}. "
            + $"The wave hits only that enemy and costs {WaveCost()}."),
        Inception => Append(original,
            $"With Dreamshift: a strike can send up to {Dreamslash.MostWaves} sword waves, each at a different enemy. Each wave after the first has half the chance of the one before."),
        _ => original,
    };

    public static string AltText(byte nodeID, string original) => nodeID switch {
        Dreamshift => Append(original, "A use that consumes a shadow ignores the cooldown and needs no enemy near you."),
        SharedMemories => Append(original,
            $"With Dreamshift: sword waves fly up to {Meters(Dreamslash.BaseReach * Settings.ReachMultiplier)}. Waves you cannot pay for are not sent."),
        _ => original,
    };

    private static IEnumerable<string> DreamshiftDescription() {
        yield return "Shadows no longer perform Dreamslash.";
        yield return $"Dreamslash becomes a flicker: it needs an enemy within {Meters(Dreamslash.BaseReach * Settings.ReachMultiplier)}, and you blink to just past your target.";
        yield return $"Each strike that consumes a {{Dream}} stack strikes again at another enemy within {Meters(Dreamslash.SearchRange)}, consuming another stack, until you run out.";
        if (Settings.Cooldown > 0f) {
            yield return $"Each use starts a {Seconds(Settings.Cooldown)} cooldown. While it runs, Dreamslash consumes a {{Dream}} stack, and you cannot use it without one.";
        }

        yield return "If you directly use Dreamslash while below your maximum {Dream} stacks, you consume a shadow near an enemy to gain a stack.";
    }

    // The decorated boxes: text, and whether it is a drawback (shown red).
    public static List<(string Text, bool Downside)> Boxes(byte nodeID) => nodeID switch {
        Dreamshift when Settings.Cooldown > 0f => [
            ("Strikes Again at another Enemy per Dream Stack", false),
            ($"{Settings.Cooldown:0.#} Second Cooldown, ignored with a Dream Stack", true),
        ],
        Dreamshift => [("Strikes Again at another Enemy per Dream Stack", false)],
        SharedMemories => [("With Dreamshift: Sword Wave at another Enemy for Mana", false)],
        Inception => [($"With Dreamshift: up to {Dreamslash.MostWaves} Sword Waves per Strike", false)],
        _ => [],
    };

    private static string Append(string original, string added) =>
        string.IsNullOrEmpty(added) ? original : string.IsNullOrWhiteSpace(original) ? added : $"{original}\n\n{added}";

    private static string WaveCost() {
        float percent = Settings.WaveManaCost * 100f;
        float flat = Settings.WaveManaFlat;
        return percent > 0f && flat > 0f ? $"{percent:0.#}% of your maximum mana plus {flat:0.#} mana"
            : percent > 0f ? $"{percent:0.#}% of your maximum mana"
            : flat > 0f ? $"{flat:0.#} mana"
            : "no mana";
    }

    private static string Meters(float meters) => $"{meters:0.#} meters";

    private static string Seconds(float seconds) => seconds == 1f ? "1 second" : $"{seconds:0.#} second";
}

[HarmonyPatch(typeof(LocalizationSkills), nameof(LocalizationSkills.GetNodeDescription))]
internal static class NodeDescriptionPatch {
    private static void Postfix(string treeID, byte nodeID, ref string __result) {
        if (NodeTooltips.Applies(treeID, nodeID)) {
            __result = NodeTooltips.Description(nodeID, __result);
        }
    }
}

[HarmonyPatch(typeof(LocalizationSkills), nameof(LocalizationSkills.GetNodeAltText))]
internal static class NodeAltTextPatch {
    private static void Postfix(string treeID, byte nodeID, ref string __result) {
        if (NodeTooltips.Applies(treeID, nodeID)) {
            __result = NodeTooltips.AltText(nodeID, __result);
        }
    }
}

// The node's stat lines are display only (the tree applies gameplay from its own data), so the boxes are added to the
// node's stats right before a tooltip reads them: in the current tooltip (NodeTooltipContentBuilder.GetNodeTooltipInfo,
// whose RefreshModifiers turns each line into a box) and the older one (NodeTooltipManager.setInfoForNode). Lines added
// before are recognised by their text and replaced, so settings changes show and nothing piles up; online they are only
// removed. A box copies its icon from the node's first line and has no value (no number to scale with points).
internal static class NodeStatLines {
    private static readonly HashSet<string> _added = [];
    private static readonly HashSet<byte> _logged = [];
    private static readonly Fault _fault = new("changing Dreamslash node tooltips");

    public static void Apply(SkillTreeNode node) {
        if (_fault.Stopped || node == null || node.tree == null || node.tree.treeID != NodeTooltips.TreeId) {
            return;
        }

        try {
            byte id = node.getID();
            Il2CppSystem.Collections.Generic.List<SkillTreeNode.NodeTooltipStat> stats = node.stats;
            if (!NodeTooltips.Changed(id) || stats == null) {
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
            foreach ((string text, bool downside) in NodeTooltips.Boxes(id)) {
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
            _fault.Report(exception);
        }
    }

    // What the game's own lines look like, once per node, for tuning the boxes.
    private static void Log(SkillTreeNode node, byte id, Il2CppSystem.Collections.Generic.List<SkillTreeNode.NodeTooltipStat> stats) {
        if (!Settings.LogCasts || !_logged.Add(id)) {
            return;
        }

        IEnumerable<string> lines = Enumerable.Range(0, stats.Count).Select(i => stats[i])
            .Select(stat => $"'{stat.statName}' = '{stat.value}' noScaling {stat.noScaling} downside {stat.downside} property {stat.property} tags {stat.tags}");
        MelonLogger.Msg($"Dreamslash node {id} ({node.maxPoints} max points) lines: {string.Join("; ", lines)}");
    }
}

[HarmonyPatch(typeof(NodeTooltipContentBuilder), nameof(NodeTooltipContentBuilder.GetNodeTooltipInfo))]
internal static class NodeTooltipInfoPatch {
    private static void Prefix(SkillTreeNode node) => NodeStatLines.Apply(node);
}

[HarmonyPatch(typeof(NodeTooltipManager), nameof(NodeTooltipManager.setInfoForNode))]
internal static class NodeTooltipManagerPatch {
    private static void Prefix(SkillTreeNode node) => NodeStatLines.Apply(node);
}
