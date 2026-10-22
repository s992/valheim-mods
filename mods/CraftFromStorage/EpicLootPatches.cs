using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Storage
{
    // Epic Loot's enchanting table counts and removes through inventory providers, synchronously. So it only ever sees
    // chests we own; opening it (its count queries) borrows nearby chests holding what it asks about.
    internal static class EpicLootPatches
    {
        private const string ToggleName = "StorageChestTrophiesToggle";
        private static MethodInfo s_refreshSacrifice;

        public static void Apply(Harmony harmony, ManualLogSource log)
        {
            if (!EpicLootCompat.Available) return;

            // Screens that count the inventory directly instead of through the provider (Temper)
            PatchCounts(harmony, log, "EpicLoot.TemperPanel", "HaveRequirements");
            PatchCounts(harmony, log, "EpicLoot.RequirementElement", "Update");

            // Sacrifice tab checkbox (Epic Loot UI internals, not its API: missing → no checkbox, no chest trophies)
            Type sacrificeUi = AccessTools.TypeByName("EpicLoot_UnityLib.SacrificeUI");
            MethodInfo onEnable = sacrificeUi == null ? null : AccessTools.Method(sacrificeUi, "OnEnable");
            s_refreshSacrifice = sacrificeUi == null ? null : AccessTools.Method(sacrificeUi, "RefreshAvailableItems");
            if (onEnable != null && s_refreshSacrifice != null)
                harmony.Patch(onEnable, postfix: new HarmonyMethod(typeof(EpicLootPatches), nameof(AddTrophyToggle)));
            else
                log.LogWarning("Epic Loot's Sacrifice tab changed; the \"Include chest trophies\" checkbox is unavailable");
        }

        private static void PatchCounts(Harmony harmony, ManualLogSource log, string typeName, string methodName)
        {
            MethodInfo method = AccessTools.Method(AccessTools.TypeByName(typeName) ?? typeof(void), methodName);
            if (method == null)
            {
                log.LogWarning($"{typeName}.{methodName} not found; that Epic Loot screen won't count storage");
                return;
            }
            harmony.Patch(method, transpiler: new HarmonyMethod(typeof(EpicLootPatches), nameof(CountTranspiler)));
        }

        private static IEnumerable<CodeInstruction> CountTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
            Hooks.ReplaceCalls(instructions, Hooks.CountItems, Hooks.Self(nameof(Hooks.CountForEpicLoot)), 1,
                $"{original.DeclaringType?.Name}.{original.Name}");

        // ---------------------------------------------------------------- provider

        private static bool Active => Hooks.Active(Settings.EpicLoot.Value);

        public static void RegisterProvider(ManualLogSource log) =>
            EpicLootCompat.RegisterProvider(log, GetItems, Count, Remove, RemoveExact);

        private static int Count(string name)
        {
            if (!Active) return 0;
            ChestAccess.BorrowFor(name);
            return StorageNetwork.Count(name, -1, true, true);
        }

        private static int Remove(string name, int amount) =>
            Active && amount > 0 ? ChestAccess.TakeFromOwned(name, amount, -1, true, "Epic Loot") : 0;

        // Only trophies (Sacrifice tab), only from chests we own, only while the checkbox is ticked. Gear in chests is
        // never offered, so "select all" can't sacrifice or enchant the warehouse.
        private static List<ItemDrop.ItemData> GetItems()
        {
            var items = new List<ItemDrop.ItemData>();
            if (!Active || !Settings.ChestTrophies.Value) return items;
            ChestAccess.BorrowFor("trophies", IsTrophy);
            foreach (Container chest in StorageNetwork.Usable)
            {
                if (!StorageNetwork.IsOwnedForUse(chest) || !ChestAccess.PrepareOwned(chest)) continue;
                items.AddRange(chest.GetInventory().m_inventory.Where(item => IsTrophy(item) && StorageNetwork.IsUsableItem(item)));
            }
            return items;
        }

        private static int RemoveExact(ItemDrop.ItemData item, int amount) =>
            Active && Settings.ChestTrophies.Value && IsTrophy(item) ? ChestAccess.TakeExact(item, amount, "Epic Loot sacrifice") : 0;

        private static bool IsTrophy(ItemDrop.ItemData item) => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy;

        // ---------------------------------------------------------------- Sacrifice checkbox

        private static void AddTrophyToggle(MonoBehaviour __instance)
        {
            try
            {
                object list = AccessTools.Field(__instance.GetType(), "AvailableItems")?.GetValue(__instance);
                var selectAll = list == null ? null : AccessTools.Field(list.GetType(), "SelectAllToggle")?.GetValue(list) as Toggle;
                if (!selectAll) return;
                Transform parent = selectAll.transform.parent;
                if (parent.Find(ToggleName)) return;

                // A copy of Epic Loot's own "Select All" checkbox, one row above it
                GameObject copy = UnityEngine.Object.Instantiate(selectAll.gameObject, parent, false);
                copy.name = ToggleName;
                var source = (RectTransform)selectAll.transform;
                ((RectTransform)copy.transform).anchoredPosition = source.anchoredPosition + new Vector2(0f, source.rect.height + 4f);
                foreach (Text text in copy.GetComponentsInChildren<Text>(true)) text.text = "Include chest trophies";
                foreach (TMPro.TMP_Text text in copy.GetComponentsInChildren<TMPro.TMP_Text>(true)) text.text = "Include chest trophies";

                Toggle toggle = copy.GetComponent<Toggle>();
                toggle.onValueChanged = new Toggle.ToggleEvent();
                toggle.SetIsOnWithoutNotify(Settings.ChestTrophies.Value);
                toggle.interactable = true;
                toggle.onValueChanged.AddListener(on =>
                {
                    Settings.ChestTrophies.Value = on;
                    s_refreshSacrifice.Invoke(__instance, null);
                });
            }
            catch (Exception e)
            {
                StorageLog.Warning($"Couldn't add the Sacrifice tab checkbox: {e.Message}");
            }
        }
    }
}
