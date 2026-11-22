using System;
using System.Collections.Generic;
using System.Globalization;

namespace EverythingsANail
{
    // The best level (quality) of each tool the character has ever carried, kept in the player's custom data so
    // it's saved with the character. The hammer lends its own item data to the tool it becomes, quality included,
    // so mods that unlock abilities by tool level (Voxheim's hoe) would otherwise always see the hammer's level.
    // Integrations read the remembered level instead (VoxheimCompat).
    internal static class ToolLevels
    {
        public const string KeyPrefix = "s992.EverythingsANail.Level.";

        /// <summary>The remembered level of a tool, or 0 when none above level 1 has been carried.</summary>
        public static int Get(IDictionary<string, string> data, string prefab) =>
            data != null && prefab != null && data.TryGetValue(KeyPrefix + prefab, out string text) &&
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level) && level > 0
                ? level
                : 0;

        /// <summary>Remembers a level if it beats the one stored. Level 1 is every tool's default, so it isn't stored.</summary>
        /// <returns>True when the stored level changed.</returns>
        public static bool Remember(IDictionary<string, string> data, string prefab, int quality)
        {
            if (data == null || prefab == null || quality <= 1 || quality <= Get(data, prefab)) return false;
            data[KeyPrefix + prefab] = quality.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        /// <summary>The level the hammer works at as this tool: the best remembered, within what the tool allows now.</summary>
        public static int LevelFor(IDictionary<string, string> data, string prefab, int maxQuality) =>
            Math.Min(Math.Max(Get(data, prefab), 1), Math.Max(maxQuality, 1));

        public static int LevelFor(Player player, ToolInfo tool) =>
            player && tool != null ? LevelFor(player.m_customData, tool.Prefab, tool.Shared.m_maxQuality) : 1;

        // Every real tool in the inventory (the hammer isn't one, disguised or not: its prefab is the hammer)
        public static void RememberCarried(Player player)
        {
            if (!player || player.GetInventory() == null) return;
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (item?.m_dropPrefab == null) continue;
                ToolInfo tool = ToolCatalog.Get(item.m_dropPrefab.name);
                if (tool == null || tool.IsHammer) continue;
                Remember(player.m_customData, tool.Prefab, item.m_quality);
            }
        }
    }
}
