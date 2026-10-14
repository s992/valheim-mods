using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace LootFilter
{
    internal enum FilterReason
    {
        NotFiltered,
        Item,
        Group,
        Rarity,
        BiomeRarity,
        OutdatedRarity,
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
            Settings.FilteredRarities.SettingChanged += (_, __) => Version++;
            Settings.OutdatedRarities.SettingChanged += (_, __) => Version++;
            Settings.OutdatedBiomeMargin.SettingChanged += (_, __) => Version++;
            Settings.OutdatedComparedTo.SettingChanged += (_, __) => Version++;
            Settings.IncludeMagicMaterials.SettingChanged += (_, __) => Version++;
            foreach (var entry in Settings.RaritiesByItemBiome.Values)
                entry.SettingChanged += (_, __) => Version++;
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

            // Magic gear is judged by rarity alone, so a legendary bronze sword isn't hidden by a "Bronze sword" rule
            if (EpicLootCompat.TryGetRarity(item, out Rarities rarity, out bool isMagicItem))
            {
                bool rarityApplies = isMagicItem || Settings.IncludeMagicMaterials.Value;
                if (rarityApplies && (Settings.FilteredRarities.Value & rarity) != 0)
                    return FilterReason.Rarity;
                if (rarityApplies && (BiomeRarities(item, out _) & rarity) != 0)
                    return FilterReason.BiomeRarity;
                if (rarityApplies && (Settings.OutdatedRarities.Value & rarity) != 0 && IsOutdated(item, out _, out _))
                    return FilterReason.OutdatedRarity;
                if (isMagicItem)
                    return FilterReason.NotFiltered;
            }

            if (s_items.Contains(ItemName(item)))
                return FilterReason.Item;
            if ((Settings.FilteredGroups.Value & Groups.Of(item)) != 0)
                return FilterReason.Group;
            return FilterReason.NotFiltered;
        }

        // Rarities filtered for the item's tier biome ("Rarities by item biome")
        public static Rarities BiomeRarities(ItemDrop.ItemData item, out Heightmap.Biome itemBiome)
        {
            if (!EpicLootCompat.TryGetItemBiome(ItemName(item), out itemBiome, out _))
                return Rarities.None;
            return Settings.RaritiesByItemBiome.TryGetValue(itemBiome, out var entry) ? entry.Value : Rarities.None;
        }

        // An item is outdated when Epic Loot places it in an earlier biome than the player's current one
        // (progression or location, minus the configured margin). Items without a known tier are never outdated.
        public static bool IsOutdated(ItemDrop.ItemData item, out Heightmap.Biome itemBiome, out Heightmap.Biome currentBiome)
        {
            itemBiome = Heightmap.Biome.None;
            currentBiome = CurrentBiome();
            if (currentBiome == Heightmap.Biome.None || !EpicLootCompat.TryGetItemBiome(ItemName(item), out itemBiome, out int itemOrder))
                return false;

            int currentOrder = EpicLootCompat.BiomeOrder(currentBiome);
            return currentOrder >= 0 && itemOrder < currentOrder - Settings.OutdatedBiomeMargin.Value;
        }

        public static Heightmap.Biome CurrentBiome()
        {
            if (Settings.OutdatedComparedTo.Value == BiomeReference.Progression)
                return EpicLootCompat.ProgressionBiome();
            Player player = Player.m_localPlayer;
            return player ? player.GetCurrentBiome() : Heightmap.Biome.None;
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

        public static bool ToggleRarity(Rarities rarity)
        {
            Settings.FilteredRarities.Value ^= rarity;
            return (Settings.FilteredRarities.Value & rarity) != 0;
        }

        public static bool ToggleBiomeRarity(Heightmap.Biome biome, Rarities rarity)
        {
            ConfigEntry<Rarities> entry = Settings.RaritiesByItemBiome[biome];
            entry.Value ^= rarity;
            return (entry.Value & rarity) != 0;
        }

        public static bool ToggleOutdatedRarity(Rarities rarity)
        {
            Settings.OutdatedRarities.Value ^= rarity;
            return (Settings.OutdatedRarities.Value & rarity) != 0;
        }
    }
}
