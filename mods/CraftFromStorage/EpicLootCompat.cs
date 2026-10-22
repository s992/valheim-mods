using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;

namespace Storage
{
    // Optional Epic Loot integration through its official API (EpicLoot.API), found at runtime
    internal static class EpicLootCompat
    {
        public const string Guid = "randyknapp.mods.epicloot";
        public const string ProviderId = "s992.storage";
        // Digi's storage-materials module; running both would count every chest twice
        private const string DigiProviderId = "pieter.nearbychests";

        private static Func<ItemDrop.ItemData, bool> s_isMagicItem;
        private static Func<ItemDrop.ItemData, bool> s_isMagicCraftingMaterial;

        public static bool Available { get; private set; }

        public static void Init(ManualLogSource log)
        {
            if (!Chainloader.PluginInfos.ContainsKey(Guid)) return;
            try
            {
                Type api = AccessTools.TypeByName("EpicLoot.API");
                s_isMagicItem = Make(api, "IsMagicItem");
                s_isMagicCraftingMaterial = Make(api, "IsMagicCraftingMaterial");
                Available = s_isMagicItem != null && s_isMagicCraftingMaterial != null;
                if (!Available) log.LogWarning("Epic Loot API not found; magic items in chests can't be told apart and Epic Loot support is off");
            }
            catch (Exception e)
            {
                log.LogWarning($"Epic Loot hookup failed, Epic Loot support is off: {e.Message}");
            }
        }

        private static Func<ItemDrop.ItemData, bool> Make(Type api, string name)
        {
            MethodInfo method = api == null ? null : AccessTools.Method(api, name, new[] { typeof(ItemDrop.ItemData) });
            return method == null ? null : (Func<ItemDrop.ItemData, bool>)Delegate.CreateDelegate(typeof(Func<ItemDrop.ItemData, bool>), method);
        }

        /// <summary>Enchanted gear (but not Epic Loot's crafting materials): never counted or consumed from storage.</summary>
        public static bool IsEnchanted(ItemDrop.ItemData item) =>
            Available && s_isMagicItem(item) && !s_isMagicCraftingMaterial(item);

        /// <summary>Registers our inventory provider and removes Digi's. Run after every plugin's Awake.</summary>
        public static void RegisterProvider(ManualLogSource log,
            Func<List<ItemDrop.ItemData>> getItems, Func<string, int> count, Func<string, int, int> remove, Func<ItemDrop.ItemData, int, int> removeExact)
        {
            if (!Available) return;
            Type api = AccessTools.TypeByName("EpicLoot.API");
            MethodInfo register = AccessTools.Method(api, "RegisterInventoryProvider");
            MethodInfo unregister = AccessTools.Method(api, "UnregisterInventoryProvider");
            if (register == null)
            {
                log.LogWarning("Epic Loot has no inventory provider API in this version; the enchanting table won't use storage");
                return;
            }
            if (unregister != null && (bool)unregister.Invoke(null, new object[] { DigiProviderId }))
                log.LogWarning("Removed Digi's Epic Loot storage provider so chests aren't counted twice. Turn off Digi's storage module in its config.");
            bool ok = (bool)register.Invoke(null, new object[] { ProviderId, getItems, count, remove, removeExact });
            if (ok) log.LogInfo("Epic Loot's enchanting table now uses nearby storage");
            else log.LogWarning("Epic Loot refused our inventory provider");
        }
    }
}
