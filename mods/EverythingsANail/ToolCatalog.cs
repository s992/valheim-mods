using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EverythingsANail
{
    internal sealed class ToolInfo
    {
        public string Prefab;
        public ItemDrop.ItemData.SharedData Shared;
        public ToolCategories Category;
        // Tools in one group are variants of each other (axes, pickaxes); each build tool is its own group
        public string Group;
        // Higher is better within a group
        public float Rank;

        public bool IsHammer => Prefab == ToolCatalog.HammerPrefab;
        public string DisplayName => Localization.instance.Localize(Shared.m_name);
        public Sprite Icon => Shared.m_icons != null && Shared.m_icons.Length > 0 ? Shared.m_icons[0] : null;
    }

    // Every tool-like item in the game (including other mods'), detected by what it does
    internal static class ToolCatalog
    {
        public const string HammerPrefab = "Hammer";

        private static ObjectDB s_db;
        private static int s_itemCount;
        private static List<ToolInfo> s_tools = new List<ToolInfo>();
        private static Dictionary<string, ToolInfo> s_byPrefab = new Dictionary<string, ToolInfo>();

        public static IReadOnlyList<ToolInfo> All
        {
            get
            {
                Refresh();
                return s_tools;
            }
        }

        public static ToolInfo Get(string prefab)
        {
            Refresh();
            return prefab != null && s_byPrefab.TryGetValue(prefab, out ToolInfo tool) ? tool : null;
        }

        public static ToolInfo Hammer => Get(HammerPrefab);

        public static bool IsAllowed(ToolInfo tool) => IsAllowed(tool, Settings.Active);

        public static bool IsAllowed(ToolInfo tool, Rules rules)
        {
            if (tool.IsHammer) return true;
            if (rules.Exclude.Contains(tool.Prefab)) return false;
            if (rules.OnlyAllow.Count > 0) return rules.OnlyAllow.Contains(tool.Prefab);
            return (rules.Categories & tool.Category) != 0;
        }

        // "Has ever been in your inventory": the game's known-items list, kept per character
        public static bool IsUnlocked(Player player, ToolInfo tool) =>
            tool.IsHammer || (player != null && player.IsKnownMaterial(tool.Shared.m_name));

        public static bool IsAvailable(Player player, ToolInfo tool) => IsAllowed(tool) && IsUnlocked(player, tool);

        // Available tools in menu order: hammer, other build tools, then the other categories,
        // each group best first
        public static List<List<ToolInfo>> AvailableGroups(Player player) =>
            All.Where(tool => IsAvailable(player, tool))
                .GroupBy(tool => tool.Group)
                .Select(group => group.OrderByDescending(tool => tool.Rank).ThenBy(tool => tool.DisplayName).ToList())
                .OrderBy(group => group[0].IsHammer ? 0 : 1)
                .ThenBy(group => group[0].Category)
                .ThenBy(group => group[0].DisplayName)
                .ToList();

        // ObjectDB is recreated per world and other mods add items to it after it wakes, so rebuild
        // whenever it changes rather than once at startup
        private static void Refresh()
        {
            ObjectDB db = ObjectDB.instance;
            if (!db)
            {
                s_db = null;
                s_tools = new List<ToolInfo>();
                s_byPrefab = new Dictionary<string, ToolInfo>();
                return;
            }
            if (db == s_db && db.m_items.Count == s_itemCount) return;
            s_db = db;
            s_itemCount = db.m_items.Count;

            var tools = new List<ToolInfo>();
            var byPrefab = new Dictionary<string, ToolInfo>();
            foreach (GameObject prefab in db.m_items)
            {
                ToolInfo tool = Classify(prefab);
                if (tool == null || byPrefab.ContainsKey(tool.Prefab)) continue;
                tools.Add(tool);
                byPrefab[tool.Prefab] = tool;
            }
            s_tools = tools;
            s_byPrefab = byPrefab;
        }

        private static ToolInfo Classify(GameObject prefab)
        {
            if (!prefab) return null;
            ItemDrop drop = prefab.GetComponent<ItemDrop>();
            ItemDrop.ItemData.SharedData shared = drop ? drop.m_itemData?.m_shared : null;
            return shared == null ? null : Classify(prefab.name, shared);
        }

        /// <summary>What kind of tool an item is, from what it does (null for anything that isn't a tool).</summary>
        public static ToolInfo Classify(string prefabName, ItemDrop.ItemData.SharedData shared)
        {
            // The serving tray has its own animation and isn't a tool
            if (shared.m_animationState == ItemDrop.ItemData.AnimationState.Feaster) return null;

            var tool = new ToolInfo { Prefab = prefabName, Shared = shared };
            HitData.DamageTypes damage = shared.m_damages;
            if (shared.m_buildPieces)
            {
                tool.Category = ToolCategories.Building;
                tool.Group = prefabName;
            }
            else if (shared.m_tamedOnly || prefabName == "KnifeButcher")
            {
                tool.Category = ToolCategories.Butchering;
                tool.Rank = damage.GetTotalDamage();
            }
            else if (shared.m_animationState == ItemDrop.ItemData.AnimationState.FishingRod)
            {
                tool.Category = ToolCategories.Fishing;
            }
            else if (IsHarvest(shared.m_attack) || IsHarvest(shared.m_secondaryAttack))
            {
                tool.Category = ToolCategories.Harvesting;
                tool.Rank = shared.m_toolTier * 1000 + damage.GetTotalDamage();
            }
            else if (shared.m_skillType == Skills.SkillType.Axes && shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon && damage.m_chop > 0f)
            {
                tool.Category = ToolCategories.Logging;
                tool.Rank = shared.m_toolTier * 1000 + damage.m_chop;
            }
            else if (shared.m_skillType == Skills.SkillType.Pickaxes && damage.m_pickaxe > 0f)
            {
                tool.Category = ToolCategories.Mining;
                tool.Rank = shared.m_toolTier * 1000 + damage.m_pickaxe;
            }
            else
            {
                return null;
            }
            if (tool.Group == null) tool.Group = tool.Category.ToString();
            return tool;
        }

        private static bool IsHarvest(Attack attack) => attack != null && attack.m_harvest;
    }
}
