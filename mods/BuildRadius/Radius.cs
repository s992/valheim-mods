using System.Collections.Generic;
using System.Globalization;

namespace BuildRadius
{
    // The radius rules, kept free of game objects so they can be tested.
    //
    // The game gives each station a base build radius (m_rangeBuild) plus a bonus per attached extension
    // (m_extraRangePerLevel). The multiplier scales both; an override replaces the base radius of one kind of station,
    // and its extension bonus still follows the multiplier.
    internal static class Radius
    {
        public const float MinOverride = 1f;
        public const float MaxOverride = 500f;

        public static (float baseRange, float extraPerLevel) Compute(
            float vanillaBase, float vanillaExtra, float multiplier, float? overrideBase) =>
            (overrideBase ?? vanillaBase * multiplier, vanillaExtra * multiplier);

        /// <summary>
        /// Stations are named by prefab (piece_workbench, forge) or by the game's station name without the $
        /// (piece_workbench, piece_forge), in any case.
        /// </summary>
        public static string Key(string name) => (name ?? "").Trim().TrimStart('$').ToLowerInvariant();

        // "piece_workbench:40, forge:30". Entries that don't parse end up in `problems` rather than silently changing a
        // radius; a station named twice keeps the last one.
        public static Dictionary<string, float> ParseOverrides(string text, List<string> problems)
        {
            var overrides = new Dictionary<string, float>();
            foreach (string raw in (text ?? "").Split(','))
            {
                string entry = raw.Trim();
                if (entry.Length == 0) continue;
                string[] parts = entry.Split(':');
                if (parts.Length != 2 || Key(parts[0]).Length == 0
                    || !float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float radius)
                    || float.IsNaN(radius) || radius < MinOverride || radius > MaxOverride)
                {
                    problems.Add(entry);
                    continue;
                }
                overrides[Key(parts[0])] = radius;
            }
            return overrides;
        }

        /// <summary>The override for a station, by prefab name first, then by station name.</summary>
        public static float? Find(Dictionary<string, float> overrides, string prefabName, string stationName)
        {
            if (overrides.TryGetValue(Key(prefabName), out float byPrefab)) return byPrefab;
            if (overrides.TryGetValue(Key(stationName), out float byName)) return byName;
            return null;
        }
    }
}
