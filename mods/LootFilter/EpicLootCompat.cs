using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;

namespace LootFilter
{
    // Optional Epic Loot support through its public API (EpicLoot.API), found by reflection
    // so Epic Loot isn't needed to build or run
    internal static class EpicLootCompat
    {
        public const string Guid = "randyknapp.mods.epicloot";

        private delegate bool TryGetRarityFn(ItemDrop.ItemData item, ref int rarity);

        private static Func<ItemDrop.ItemData, bool> s_isMagicItem;
        private static TryGetRarityFn s_tryGetRarity;

        public static bool Available { get; private set; }

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
            }
        }

        // Rarity of magic gear, and (if wanted) of magic materials and runestones
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
    }
}
