using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;

namespace LootFilter
{
    // Optional Epic Loot support, found by reflection so Epic Loot isn't needed to build or run.
    // Rarity comes from its public API (EpicLoot.API). Item tiers come from its gated item and
    // biome data, which isn't official API: if that changes, only the biome rule switches off.
    internal static class EpicLootCompat
    {
        public const string Guid = "randyknapp.mods.epicloot";

        private delegate bool TryGetRarityFn(ItemDrop.ItemData item, ref int rarity);

        private static Func<ItemDrop.ItemData, bool> s_isMagicItem;
        private static TryGetRarityFn s_tryGetRarity;

        // GatedItemTypeHelper.AllItemsWithDetails: item name -> GatedItemDetails { List<string> RequiredBosses }
        private static IDictionary s_itemsWithDetails;
        private static PropertyInfo s_requiredBosses;
        // BiomeDataManager.GetFirstBiomeForBossKey / GetOrder
        private static MethodInfo s_biomeForBossKey;
        private static MethodInfo s_biomeOrder;

        public static bool Available { get; private set; }
        public static bool BiomeTiersAvailable { get; private set; }

        public static void Init(ManualLogSource log)
        {
            if (!Chainloader.PluginInfos.ContainsKey(Guid)) return;

            try
            {
                Type api = AccessTools.TypeByName("EpicLoot.API");
                MethodInfo isMagic = AccessTools.Method(api, "IsMagicItem", new[] { typeof(ItemDrop.ItemData) });
                MethodInfo tryGetRarity = AccessTools.Method(api, "TryGetRarity",
                    new[] { typeof(ItemDrop.ItemData), typeof(int).MakeByRefType() });
                if (isMagic == null || tryGetRarity == null)
                {
                    log.LogWarning("Epic Loot is installed but its API wasn't found; rarity rules are disabled");
                    return;
                }

                s_isMagicItem = (Func<ItemDrop.ItemData, bool>)Delegate.CreateDelegate(typeof(Func<ItemDrop.ItemData, bool>), isMagic);
                s_tryGetRarity = (TryGetRarityFn)Delegate.CreateDelegate(typeof(TryGetRarityFn), tryGetRarity);
                Available = true;
                log.LogInfo("Epic Loot found; rarity rules enabled");
            }
            catch (Exception e)
            {
                log.LogWarning($"Couldn't hook up Epic Loot's API; rarity rules are disabled: {e.Message}");
                return;
            }

            try
            {
                Type gated = AccessTools.TypeByName("EpicLoot.GatedItemType.GatedItemTypeHelper");
                Type details = AccessTools.TypeByName("EpicLoot.GatedItemType.GatedItemDetails");
                Type biomes = AccessTools.TypeByName("EpicLoot.Biomes.BiomeDataManager");
                s_itemsWithDetails = AccessTools.Field(gated, "AllItemsWithDetails")?.GetValue(null) as IDictionary;
                s_requiredBosses = AccessTools.Property(details, "RequiredBosses");
                s_biomeForBossKey = AccessTools.Method(biomes, "GetFirstBiomeForBossKey", new[] { typeof(string) });
                s_biomeOrder = AccessTools.Method(biomes, "GetOrder", new[] { typeof(Heightmap.Biome) });

                BiomeTiersAvailable = s_itemsWithDetails != null && s_requiredBosses != null
                    && s_biomeForBossKey?.ReturnType == typeof(Heightmap.Biome) && s_biomeOrder != null;
                if (BiomeTiersAvailable)
                    log.LogInfo("Epic Loot item tiers found; outdated-rarity rules enabled");
                else
                    log.LogWarning("Epic Loot's item tier data wasn't found (different Epic Loot version?); outdated-rarity rules are disabled");
            }
            catch (Exception e)
            {
                log.LogWarning($"Couldn't read Epic Loot's item tiers; outdated-rarity rules are disabled: {e.Message}");
            }
        }

        // Rarity of magic gear, and of magic materials and runestones
        public static bool TryGetRarity(ItemDrop.ItemData item, out Rarities rarity, out bool isMagicItem)
        {
            rarity = Rarities.None;
            isMagicItem = false;
            if (!Available || item == null) return false;

            int value = 0;
            if (!s_tryGetRarity(item, ref value)) return false;
            isMagicItem = s_isMagicItem(item);
            rarity = Groups.FromEpicLoot(value);
            return true;
        }

        // The biome an item belongs to: the latest biome whose boss Epic Loot requires before it can drop.
        // False for items Epic Loot doesn't gate (materials, modded items it doesn't know).
        public static bool TryGetItemBiome(string itemName, out Heightmap.Biome biome, out int order)
        {
            biome = Heightmap.Biome.None;
            order = -1;
            if (!BiomeTiersAvailable || !s_itemsWithDetails.Contains(itemName)) return false;

            if (!(s_requiredBosses.GetValue(s_itemsWithDetails[itemName]) is IEnumerable<string> bosses)) return false;
            foreach (string boss in bosses)
            {
                var bossBiome = (Heightmap.Biome)s_biomeForBossKey.Invoke(null, new object[] { boss });
                int bossOrder = BiomeOrder(bossBiome);
                if (bossOrder > order)
                {
                    order = bossOrder;
                    biome = bossBiome;
                }
            }
            // Items with no boss requirement ("none") are Meadows tier
            if (biome == Heightmap.Biome.None)
            {
                biome = Heightmap.Biome.Meadows;
                order = BiomeOrder(biome);
            }
            return true;
        }

        // Position in Epic Loot's biome progression (Meadows first); -1 for none
        public static int BiomeOrder(Heightmap.Biome biome) =>
            BiomeTiersAvailable ? (int)s_biomeOrder.Invoke(null, new object[] { biome }) : -1;
    }
}
