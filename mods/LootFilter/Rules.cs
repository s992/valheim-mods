using System;
using System.Collections.Generic;
using System.Linq;

namespace LootFilter
{
    internal enum FilterReason
    {
        NotFiltered,
        Item,
        Group,
    }

    internal static class Rules
    {
        private static HashSet<string> s_items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Bumped whenever any rule changes, so cached results can be refreshed
        public static int Version { get; private set; }

        public static void Init()
        {
            ParseItems();
            Settings.FilteredItems.SettingChanged += (_, __) => { ParseItems(); Version++; };
            Settings.FilteredGroups.SettingChanged += (_, __) => Version++;
        }

        private static void ParseItems()
        {
            s_items = new HashSet<string>(
                Settings.FilteredItems.Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }

        // Internal prefab name (e.g. "RoundLog"), which unlike display names doesn't depend on language
        public static string ItemName(ItemDrop.ItemData item) =>
            item.m_dropPrefab ? item.m_dropPrefab.name : item.m_shared.m_name;

        public static bool IsFiltered(ItemDrop.ItemData item) => Explain(item) != FilterReason.NotFiltered;

        public static FilterReason Explain(ItemDrop.ItemData item)
        {
            if (item == null) return FilterReason.NotFiltered;
            if (s_items.Contains(ItemName(item)))
                return FilterReason.Item;
            if ((Settings.FilteredGroups.Value & Groups.Of(item)) != 0)
                return FilterReason.Group;
            return FilterReason.NotFiltered;
        }

        // --- toggles; each returns the new state (true = filtered) ---

        public static bool ToggleItem(ItemDrop.ItemData item)
        {
            string name = ItemName(item);
            var items = new HashSet<string>(s_items, StringComparer.OrdinalIgnoreCase);
            bool filtered = !items.Remove(name);
            if (filtered) items.Add(name);
            Settings.FilteredItems.Value = string.Join(", ", items.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
            return filtered;
        }

        public static bool ToggleGroup(ItemGroups group)
        {
            Settings.FilteredGroups.Value ^= group;
            return (Settings.FilteredGroups.Value & group) != 0;
        }
    }
}
