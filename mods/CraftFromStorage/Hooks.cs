using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Storage
{
    // Replacements for single vanilla calls (Inventory.CountItems/HaveItem/GetItem/RemoveItem) inside the requirement
    // checks. Each takes the same arguments as the call it replaces, with the inventory first, and only adds storage when
    // the inventory is the local player's.
    internal static class Hooks
    {
        public static bool Active(bool feature)
        {
            Player player = Player.m_localPlayer;
            return Settings.Enabled.Value && feature && player && !player.NoCostCheat();
        }

        private static bool IsLocalPlayers(Inventory inventory) =>
            Player.m_localPlayer && inventory == Player.m_localPlayer.m_inventory;

        private static bool BuildingNow() => Player.m_localPlayer && Player.m_localPlayer.InPlaceMode();

        // Crafting/build menus count everything we could get: chests we own, plus chests others own (pulled before use)
        public static int CountForCrafting(Inventory inventory, string name, int quality, bool matchWorldLevel) =>
            inventory.CountItems(name, quality, matchWorldLevel) +
            (IsLocalPlayers(inventory) && Active(Settings.Crafting.Value) ? StorageNetwork.Count(name, quality, matchWorldLevel, false) : 0);

        public static int CountForBuilding(Inventory inventory, string name, int quality, bool matchWorldLevel) =>
            inventory.CountItems(name, quality, matchWorldLevel) +
            (IsLocalPlayers(inventory) && Active(Settings.Building.Value) ? StorageNetwork.Count(name, quality, matchWorldLevel, false) : 0);

        public static bool HaveForBuilding(Inventory inventory, string name, bool matchWorldLevel) =>
            inventory.HaveItem(name, matchWorldLevel) ||
            (IsLocalPlayers(inventory) && Active(Settings.Building.Value) && StorageNetwork.Count(name, -1, matchWorldLevel, false) > 0);

        // Epic Loot removes synchronously, so its screens only count chests we own
        public static int CountForEpicLoot(Inventory inventory, string name, int quality, bool matchWorldLevel) =>
            inventory.CountItems(name, quality, matchWorldLevel) +
            (IsLocalPlayers(inventory) && Active(Settings.EpicLoot.Value) ? StorageNetwork.Count(name, quality, matchWorldLevel, true) : 0);

        // One-ingredient recipes need an item to name the ingredient; when only storage has it, use one from storage
        public static ItemDrop.ItemData GetItemForCrafting(Inventory inventory, string name, int quality, bool isPrefabName)
        {
            ItemDrop.ItemData item = inventory.GetItem(name, quality, isPrefabName);
            if (item != null || isPrefabName || !IsLocalPlayers(inventory) || !Active(Settings.Crafting.Value)) return item;
            return StorageNetwork.FindItem(name, quality);
        }

        /// <summary>
        /// Vanilla removes from the inventory; whatever it couldn't is taken from chests we own. Materials in chests
        /// other players own were pulled into the inventory before this runs (crafting) or the action was held back
        /// until they were (building), so anything still missing here means vanilla's check let it through.
        /// </summary>
        public static void RemoveWithStorage(Inventory inventory, string name, int amount, int itemQuality, bool worldLevelBased)
        {
            bool feature = BuildingNow() ? Settings.Building.Value : Settings.Crafting.Value || Settings.EpicLoot.Value;
            if (!IsLocalPlayers(inventory) || !Active(feature))
            {
                inventory.RemoveItem(name, amount, itemQuality, worldLevelBased);
                return;
            }
            int before = inventory.CountItems(name, itemQuality, worldLevelBased);
            // Still the original call, so other mods' hooks on it (Epic Loot's magic transfer) see the inventory part
            inventory.RemoveItem(name, amount, itemQuality, worldLevelBased);
            int missing = amount - (before - inventory.CountItems(name, itemQuality, worldLevelBased));
            if (missing <= 0) return;
            missing -= ChestAccess.TakeFromOwned(name, missing, itemQuality, worldLevelBased, BuildingNow() ? "building" : "crafting");
            if (missing > 0)
                StorageLog.Warning($"{missing} {name} couldn't be taken from storage; that part of the cost was free");
        }

        /// <summary>Replaces every call to <paramref name="target"/> with <paramref name="replacement"/>; warns when the count is off.</summary>
        public static IEnumerable<CodeInstruction> ReplaceCalls(IEnumerable<CodeInstruction> instructions, MethodInfo target,
            MethodInfo replacement, int expected, string where)
        {
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (target != null && instruction.Calls(target))
                {
                    replaced++;
                    yield return new CodeInstruction(OpCodes.Call, replacement) { labels = instruction.labels, blocks = instruction.blocks };
                }
                else yield return instruction;
            }
            if (replaced != expected)
                Debug.LogWarning($"[Storage] {where}: expected {expected} call(s) to {target?.Name ?? "a missing method"}, replaced {replaced}. " +
                                 "Nearby storage may not be counted there.");
        }

        public static readonly MethodInfo CountItems =
            AccessTools.Method(typeof(Inventory), nameof(Inventory.CountItems), new[] { typeof(string), typeof(int), typeof(bool) });
        public static readonly MethodInfo HaveItem =
            AccessTools.Method(typeof(Inventory), nameof(Inventory.HaveItem), new[] { typeof(string), typeof(bool) });
        public static readonly MethodInfo GetItem =
            AccessTools.Method(typeof(Inventory), nameof(Inventory.GetItem), new[] { typeof(string), typeof(int), typeof(bool) });
        public static readonly MethodInfo RemoveItemByName =
            AccessTools.Method(typeof(Inventory), nameof(Inventory.RemoveItem), new[] { typeof(string), typeof(int), typeof(int), typeof(bool) });

        public static MethodInfo Self(string name) => AccessTools.Method(typeof(Hooks), name);
    }
}
