using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace EverythingsANail
{
    // The hammer becomes a tool by pointing its shared item data (name, attack, item type, build
    // pieces, skill...) at a copy of the tool's while it's equipped. The hammer's own ItemData stays,
    // so durability, quality, crafter and other mods' custom data (Epic Loot's magic) carry over.
    // Saves store the prefab name, so a saved or dropped hammer is always a plain hammer.
    internal static class Impersonation
    {
        // Which tool a hammer is set to, kept on the hammer itself so it survives saves
        public const string ToolKey = "s992.EverythingsANail.Tool";

        private static readonly MethodInfo s_memberwiseClone = AccessTools.Method(typeof(object), "MemberwiseClone");

        // Disguises by tool prefab, per ObjectDB (the game's prefabs are recreated per world)
        private static readonly Dictionary<string, ItemDrop.ItemData.SharedData> s_disguises = new Dictionary<string, ItemDrop.ItemData.SharedData>();
        private static ObjectDB s_disguisesDb;
        // Every disguise ever made, so items still wearing an old one are recognised
        private static readonly Dictionary<ItemDrop.ItemData.SharedData, ToolInfo> s_toolOf = new Dictionary<ItemDrop.ItemData.SharedData, ToolInfo>();

        public static bool IsHammer(ItemDrop.ItemData item) =>
            item?.m_dropPrefab != null && item.m_dropPrefab.name == ToolCatalog.HammerPrefab;

        public static bool IsDisguised(ItemDrop.ItemData item) => item?.m_shared != null && s_toolOf.ContainsKey(item.m_shared);

        // The tool the hammer is acting as right now (the hammer itself when not disguised)
        public static ToolInfo CurrentTool(ItemDrop.ItemData hammer) =>
            hammer?.m_shared != null && s_toolOf.TryGetValue(hammer.m_shared, out ToolInfo tool) ? tool : ToolCatalog.Hammer;

        public static string StoredTool(ItemDrop.ItemData hammer) =>
            hammer.m_customData.TryGetValue(ToolKey, out string prefab) ? prefab : null;

        public static void SetStoredTool(ItemDrop.ItemData hammer, ToolInfo tool)
        {
            if (tool == null || tool.IsHammer) hammer.m_customData.Remove(ToolKey);
            else hammer.m_customData[ToolKey] = tool.Prefab;
        }

        // Just before the hammer is equipped: become the stored tool if it's still available
        public static void Disguise(Player player, ItemDrop.ItemData hammer)
        {
            Restore(hammer);
            ToolInfo tool = ToolCatalog.Get(StoredTool(hammer));
            if (tool == null || tool.IsHammer || !ToolCatalog.IsAvailable(player, tool)) return;
            hammer.m_shared = GetDisguise(tool, hammer);
        }

        public static void Restore(ItemDrop.ItemData item)
        {
            if (!IsDisguised(item)) return;
            ItemDrop prefab = item.m_dropPrefab ? item.m_dropPrefab.GetComponent<ItemDrop>() : null;
            if (prefab) item.m_shared = prefab.m_itemData.m_shared;
        }

        private static ItemDrop.ItemData.SharedData GetDisguise(ToolInfo tool, ItemDrop.ItemData hammer)
        {
            if (s_disguisesDb != ObjectDB.instance)
            {
                s_disguises.Clear();
                s_disguisesDb = ObjectDB.instance;
            }
            if (s_disguises.TryGetValue(tool.Prefab, out ItemDrop.ItemData.SharedData disguise)) return disguise;

            ItemDrop.ItemData.SharedData real = hammer.m_dropPrefab.GetComponent<ItemDrop>().m_itemData.m_shared;
            // Shallow copy: attacks and other shared objects are the tool's own and must not be modified
            disguise = (ItemDrop.ItemData.SharedData)s_memberwiseClone.Invoke(tool.Shared, null);
            // The tool's name stays: other mods recognise tools by it (PlantEasily, OCDheim, PlanBuild).
            // What belongs to the item rather than the job comes from the hammer, so the durability
            // bar and weight don't jump when switching.
            disguise.m_maxDurability = real.m_maxDurability;
            disguise.m_durabilityPerLevel = real.m_durabilityPerLevel;
            disguise.m_useDurability = real.m_useDurability;
            disguise.m_canBeReparied = real.m_canBeReparied;
            disguise.m_destroyBroken = real.m_destroyBroken;
            disguise.m_maxQuality = real.m_maxQuality;
            disguise.m_weight = real.m_weight;
            disguise.m_scaleWeightByQuality = real.m_scaleWeightByQuality;
            disguise.m_teleportable = real.m_teleportable;
            disguise.m_value = real.m_value;
            disguise.m_variants = real.m_variants;
            Sprite hammerIcon = real.m_icons != null && real.m_icons.Length > 0 ? real.m_icons[0] : null;
            disguise.m_icons = new[] { IconBadge.Compose(tool.Icon, hammerIcon) ?? hammerIcon };

            s_disguises[tool.Prefab] = disguise;
            s_toolOf[disguise] = tool;
            return disguise;
        }
    }
}
