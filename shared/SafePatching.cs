using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;

namespace ValheimMods.Shared
{
    // Applies a mod's Harmony patches all or nothing. Harmony's PatchAll stops at the first patch class that throws
    // (usually a game method that moved or changed in an update) and keeps the ones before it, so a mod could half
    // work: count materials in chests but not take them, say. Here every patch class is tried; if any fails, all of the
    // mod's patches are removed, and the log and the player are told the mod is off.
    internal static class SafePatching
    {
        /// <summary>
        /// Patches every [HarmonyPatch] class in this mod. Optional patches for other mods are applied separately,
        /// after this, so a missing optional mod can't turn the whole mod off.
        /// </summary>
        /// <returns>True when every patch applied; false when the mod is now unpatched and must stay inactive.</returns>
        public static bool PatchAll(Harmony harmony, ManualLogSource log)
        {
            var failed = new List<string>();
            foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(SafePatching).Assembly))
            {
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    failed.Add(type.Name);
                    log.LogError($"Patch {type.Name} failed: {e}");
                }
            }
            if (failed.Count == 0) return true;

            harmony.UnpatchSelf();
            string message = $"{ModInfo.Name} is turned off: this game version changed something it relies on " +
                             $"({string.Join(", ", failed)}). Check for an update to {ModInfo.Name}.";
            log.LogError(message);
            PlayerNotice.Show(message);
            return false;
        }

        /// <summary>
        /// For transpilers: throws when the number of instructions changed isn't what the game code had when the patch
        /// was written, which fails the patch (and with it the mod) instead of leaving a half-applied change.
        /// </summary>
        public static void ExpectReplaced(int replaced, int expected, string what)
        {
            if (replaced != expected)
                throw new InvalidOperationException($"{what}: expected {expected} change(s), made {replaced}");
        }
    }
}
