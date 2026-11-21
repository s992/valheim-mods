using System.Collections.Generic;
using HarmonyLib;

namespace BuildRadius
{
    // Each placed station gets its radius fields rewritten from the vanilla values it was spawned with. The game then
    // does the rest: every 2 s it recomputes the radius (base + extensions x bonus) and sizes the hover circle and the
    // station's player-base area (no monster spawns) from it, and the build and remove checks read the same radius.
    // Restoring the vanilla values undoes it, so a setting changed back takes effect on placed stations too.
    internal static class Stations
    {
        private struct Vanilla
        {
            public float Base;
            public float Extra;
        }

        private static readonly Dictionary<CraftingStation, Vanilla> s_vanilla = new Dictionary<CraftingStation, Vanilla>();

        public static void Track(CraftingStation station)
        {
            if (!station || s_vanilla.ContainsKey(station)) return;
            var vanilla = new Vanilla { Base = station.m_rangeBuild, Extra = station.m_extraRangePerLevel };
            s_vanilla[station] = vanilla;
            Apply(station, vanilla);
        }

        public static void Forget(CraftingStation station) => s_vanilla.Remove(station);

        public static void ApplyAll()
        {
            foreach (KeyValuePair<CraftingStation, Vanilla> entry in s_vanilla)
            {
                if (entry.Key) Apply(entry.Key, entry.Value);
            }
        }

        private static void Apply(CraftingStation station, Vanilla vanilla)
        {
            float baseRange = vanilla.Base, extra = vanilla.Extra;
            if (ServerAuthority.Active)
            {
                float? overrideBase = Radius.Find(Settings.ParsedOverrides,
                    Utils.GetPrefabName(station.gameObject), station.m_name);
                (baseRange, extra) = Radius.Compute(vanilla.Base, vanilla.Extra, Settings.Multiplier.Value, overrideBase);
            }
            if (station.m_rangeBuild == baseRange && station.m_extraRangePerLevel == extra) return;
            station.m_rangeBuild = baseRange;
            station.m_extraRangePerLevel = extra;
            // The game recomputes the radius, circle and base area on its next check instead of up to 2 s later
            station.m_updateExtensionTimer = 2f;
        }
    }

    // Only stations the game itself tracks (it leaves out the placement ghost, which has no ZDO)
    [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.Start))]
    internal static class StationStart
    {
        private static void Postfix(CraftingStation __instance)
        {
            if (CraftingStation.m_allStations.Contains(__instance)) Stations.Track(__instance);
        }
    }

    [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.OnDestroy))]
    internal static class StationDestroy
    {
        private static void Prefix(CraftingStation __instance) => Stations.Forget(__instance);
    }
}
