using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;

namespace EverythingsANail
{
    // Voxheim gives the hoe extra levels that unlock terrain abilities (Carve at 4, Add at 5 by default). It reads
    // the level from the hand item's quality, which for a disguised hammer is the hammer's own, so the hammer
    // counts as the best hoe the character has carried instead (ToolLevels).
    //
    // Voxheim has no API for this; these are its internals (checked against 0.1.10), patched by hand from
    // Plugin.Awake only when it's installed. If they change, the level and cost patches switch off separately with
    // a warning, and the rest of the mod keeps working.
    internal static class VoxheimCompat
    {
        public const string Guid = "com.bogey.voxheim";
        private const string ProgressionType = "Voxheim.Unity.HoeProgression";

        private static FieldInfo s_hoeSharedField;

        // While Voxheim decides a Carve/Add's costs, the disguised hammer briefly wears the real hoe's data (see
        // BeginCostPrefix); these restore it
        private static ItemDrop.ItemData s_lentItem;
        private static ItemDrop.ItemData.SharedData s_lentDisguise;
        private static ToolInfo s_lentTool;
        // The disguise whose durability drain was set from Voxheim's per-level value, to reset afterwards
        private static ItemDrop.ItemData.SharedData s_costDisguise;

        // The level patch is in: the hammer works at its remembered hoe level
        public static bool LevelsAvailable { get; private set; }

        public static void Apply(Harmony harmony, ManualLogSource log)
        {
            if (!Chainloader.PluginInfos.ContainsKey(Guid)) return;
            Type progression = AccessTools.TypeByName(ProgressionType);
            if (progression == null)
            {
                log.LogWarning("Voxheim's hoe levels weren't found (different version?); the hammer works as a level 1 hoe");
                return;
            }

            MethodInfo getLevel = AccessTools.Method(progression, "GetLevel", new[] { typeof(Player) });
            try
            {
                if (getLevel == null || getLevel.ReturnType != typeof(int)) throw new MissingMethodException(ProgressionType, "GetLevel");
                harmony.Patch(getLevel, postfix: new HarmonyMethod(typeof(VoxheimCompat), nameof(GetLevelPostfix)));
                LevelsAvailable = true;
            }
            catch (Exception e)
            {
                log.LogWarning($"Voxheim's hoe levels changed (different version?); the hammer works as a level 1 hoe. {e.Message}");
                return;
            }

            // Voxheim sets each level's stamina and durability cost only when the hand item's data is the real
            // hoe's. Without this the hammer would pay the vanilla hoe's costs for Carve and Add.
            MethodInfo begin = AccessTools.Method(progression, "BeginPlacementCostOverride");
            MethodInfo end = AccessTools.Method(progression, "EndPlacementCostOverride");
            s_hoeSharedField = AccessTools.Field(progression, "s_hoeShared");
            try
            {
                if (begin == null || end == null || s_hoeSharedField == null ||
                    s_hoeSharedField.FieldType != typeof(ItemDrop.ItemData.SharedData))
                {
                    throw new MissingMemberException(ProgressionType, "BeginPlacementCostOverride/EndPlacementCostOverride/s_hoeShared");
                }
                harmony.Patch(begin,
                    prefix: new HarmonyMethod(typeof(VoxheimCompat), nameof(BeginCostPrefix)),
                    finalizer: new HarmonyMethod(typeof(VoxheimCompat), nameof(BeginCostFinalizer)));
                harmony.Patch(end, postfix: new HarmonyMethod(typeof(VoxheimCompat), nameof(EndCostPostfix)));
            }
            catch (Exception e)
            {
                log.LogWarning($"Voxheim's hoe costs changed (different version?); the hammer pays the vanilla hoe's costs. {e.Message}");
            }
        }

        // True when Voxheim reads this tool's level: tools sharing the real hoe's build menu
        public static bool UsesLevel(ToolInfo tool)
        {
            if (!LevelsAvailable || tool?.Shared.m_buildPieces == null) return false;
            ItemDrop.ItemData.SharedData hoe = RealHoe();
            return hoe != null && hoe.m_buildPieces == tool.Shared.m_buildPieces;
        }

        private static ItemDrop.ItemData.SharedData RealHoe() =>
            s_hoeSharedField?.GetValue(null) as ItemDrop.ItemData.SharedData
            ?? ToolCatalog.Get("Hoe")?.Shared;

        // The hand tool the hammer is acting as, or null for a real item
        private static ToolInfo DisguisedTool(ItemDrop.ItemData item)
        {
            if (item == null) return null;
            if (item == s_lentItem) return s_lentTool;
            return Impersonation.IsDisguised(item) ? Impersonation.CurrentTool(item) : null;
        }

        private static void GetLevelPostfix(Player player, ref int __result)
        {
            // 0: not holding a hoe (Voxheim checks the build menu, which the disguise keeps)
            if (__result == 0 || !player || player != Player.m_localPlayer) return;
            ToolInfo tool = DisguisedTool(player.GetRightItem());
            if (tool != null) __result = ToolLevels.LevelFor(player, tool);
        }

        private static void BeginCostPrefix(Player player)
        {
            ItemDrop.ItemData item = player ? player.GetRightItem() : null;
            if (!Impersonation.IsDisguised(item)) return;
            ItemDrop.ItemData.SharedData hoe = s_hoeSharedField.GetValue(null) as ItemDrop.ItemData.SharedData;
            if (hoe == null || item.m_shared.m_buildPieces != hoe.m_buildPieces) return;
            s_lentItem = item;
            s_lentDisguise = item.m_shared;
            s_lentTool = Impersonation.CurrentTool(item);
            item.m_shared = hoe;
        }

        private static Exception BeginCostFinalizer(Exception __exception)
        {
            if (s_lentItem != null)
            {
                s_lentItem.m_shared = s_lentDisguise;
                // The stamina cost is on the attack, which the disguise shares with the real hoe; the durability
                // drain is a field of the hoe's data, so copy it over
                if (s_hoeSharedField.GetValue(null) is ItemDrop.ItemData.SharedData hoe)
                {
                    s_lentDisguise.m_useDurabilityDrain = hoe.m_useDurabilityDrain;
                    s_costDisguise = s_lentDisguise;
                }
                s_lentItem = null;
                s_lentDisguise = null;
                s_lentTool = null;
            }
            return __exception;
        }

        // Voxheim has put the hoe's own drain back
        private static void EndCostPostfix()
        {
            if (s_costDisguise == null) return;
            if (s_hoeSharedField.GetValue(null) is ItemDrop.ItemData.SharedData hoe)
            {
                s_costDisguise.m_useDurabilityDrain = hoe.m_useDurabilityDrain;
            }
            s_costDisguise = null;
        }
    }
}
