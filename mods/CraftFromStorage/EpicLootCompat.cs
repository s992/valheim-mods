using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;

namespace CraftFromStorage
{
    // Optional: tells enchanted Epic Loot gear apart, through its official API (EpicLoot.API) found at runtime, so
    // crafting never uses up an enchanted item that happens to share a name with a material
    internal static class EpicLootCompat
    {
        public const string Guid = "randyknapp.mods.epicloot";

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
                if (!Available) log.LogWarning("Epic Loot API not found; enchanted items in chests can't be told apart from plain ones");
            }
            catch (Exception e)
            {
                log.LogWarning($"Epic Loot hookup failed; enchanted items in chests can't be told apart: {e.Message}");
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
    }
}
