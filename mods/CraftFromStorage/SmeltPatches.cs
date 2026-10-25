using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace CraftFromStorage
{
    // Smelters (also furnace, kiln, windmill, spinning wheel, eitr refinery) take the selected material from the
    // inventory first, then chests we own, then fetch a small batch from chests other players own.
    internal static class SmeltFlow
    {
        public sealed class Material
        {
            public string Prefab;
            public string Name;
            public ItemDrop Drop;
        }

        private static readonly Dictionary<Smelter, List<ChestAccess.Pull>> s_pulls = new Dictionary<Smelter, List<ChestAccess.Pull>>();

        public static string SmelterPrefab(Smelter smelter) => StorageNetwork.PrefabName(smelter.gameObject);

        // One entry per input item. A smelter can list the same input more than once (vanilla only ever uses the first
        // match); duplicates made Shift+E cycling jump back, since a selection is stored by prefab name.
        public static List<Material> Materials(Smelter smelter)
        {
            var groups = smelter.m_conversion.Where(c => c.m_from != null).GroupBy(c => c.m_from.gameObject.name).ToList();
            if (groups.Count != smelter.m_conversion.Count(c => c.m_from != null) && s_reportedDuplicates.Add(SmelterPrefab(smelter)))
                StorageLog.Info($"{SmelterPrefab(smelter)} lists the same input more than once: " +
                                string.Join(", ", groups.Where(g => g.Count() > 1).Select(g => $"{g.Key} x{g.Count()}")));
            return groups
                .Select(g => new Material { Prefab = g.Key, Name = g.First().m_from.m_itemData.m_shared.m_name, Drop = g.First().m_from })
                .ToList();
        }

        private static readonly HashSet<string> s_reportedDuplicates = new HashSet<string>();

        private static int PositionOf(List<Material> options, Material material) =>
            material == null ? -1 : options.FindIndex(m => m.Prefab == material.Prefab);

        public static int InInventory(string name) => Player.m_localPlayer.GetInventory().CountItems(name);
        public static int InStorage(string name) => StorageNetwork.Count(name, -1, true, false);
        private static int Available(Material m) => InInventory(m.Name) + InStorage(m.Name);
        public static bool Blocklisted(string prefab) => Settings.Blocklist.Contains(prefab);

        /// <summary>The player's choice for this kind of smelter, else the first available material that isn't blocklisted.</summary>
        public static Material Selected(Smelter smelter, List<Material> materials)
        {
            string chosen = Settings.GetSelection(SmelterPrefab(smelter));
            Material selected = materials.FirstOrDefault(m => m.Prefab == chosen);
            if (selected != null) return selected;
            return materials.FirstOrDefault(m => !Blocklisted(m.Prefab) && Available(m) > 0)
                   ?? materials.FirstOrDefault(m => !Blocklisted(m.Prefab));
        }

        public static void Cycle(Smelter smelter, Humanoid user)
        {
            List<Material> materials = Materials(smelter);
            Material current = Selected(smelter, materials);
            // Everything you have, including blocklisted ones so they can be picked and un-blocklisted
            List<Material> options = materials.Where(m => Available(m) > 0 || m == current).ToList();
            if (options.Count == 0)
            {
                user.Message(MessageHud.MessageType.Center, "$msg_noprocessableitems");
                return;
            }
            Material next = options[(PositionOf(options, current) + 1) % options.Count];
            Settings.SetSelection(SmelterPrefab(smelter), next.Prefab);
            user.Message(MessageHud.MessageType.TopLeft,
                $"{smelter.m_name}: {next.Name}{(Blocklisted(next.Prefab) ? " (blocklisted)" : "")}");
        }

        public static string HoverLines(Smelter smelter)
        {
            List<Material> materials = Materials(smelter);
            Material selected = Selected(smelter, materials);
            if (selected == null) return "";
            // Only ask for chests when the inventory and chests we own have none left to insert
            if (InInventory(selected.Name) + StorageNetwork.Count(selected.Name, -1, true, true) == 0)
                ChestAccess.BorrowFor(selected.Name);
            List<Material> options = materials.Where(m => Available(m) > 0 || m == selected).ToList();
            string state = Blocklisted(selected.Prefab)
                ? $" <color=orange>blocklisted</color> ({Settings.BlocklistKey.Value} to allow)"
                : $" — {InInventory(selected.Name)} in inventory, {InStorage(selected.Name)} in storage";
            string text = $"\nNext: {selected.Name}{state}";
            if (options.Count > 1)
                text += $"\n[<color=yellow><b>{ChangeKeyLabel()}</b></color>] change ({PositionOf(options, selected) + 1}/{options.Count})";
            return Localization.instance.Localize(text);
        }

        /// <returns>False to skip vanilla OnAddOre (we handled it or refused).</returns>
        public static bool BeforeAddOre(Smelter smelter, Humanoid user, ref ItemDrop.ItemData item, ref bool result)
        {
            if (user == Player.m_localPlayer && Settings.Smelting.Value && SmelterInFlight.Queue(smelter) >= smelter.m_maxOre)
            {
                user.Message(MessageHud.MessageType.Center, "$msg_itsfull");
                result = false;
                return false;
            }
            if (item != null || user != Player.m_localPlayer || !Hooks.Active(Settings.Smelting.Value)) return true;
            List<Material> materials = Materials(smelter);
            if (materials.Count == 0) return true;
            Material selected = Selected(smelter, materials);
            result = false;
            // Null only when every material is blocklisted: vanilla's own pick would ignore the blocklist
            if (selected == null)
            {
                user.Message(MessageHud.MessageType.Center, "Storage: everything this takes is blocklisted");
                return false;
            }
            if (Blocklisted(selected.Prefab))
            {
                user.Message(MessageHud.MessageType.Center, $"Storage: {Localization.instance.Localize(selected.Name)} is blocklisted");
                return false;
            }
            ItemDrop.ItemData fromInventory = user.GetInventory().GetItem(selected.Name);
            if (fromInventory != null)
            {
                item = fromInventory;
                return true;
            }
            if (ChestAccess.TakeFromOwned(selected.Name, 1, -1, true, $"{smelter.m_name}") == 1)
            {
                // Let vanilla do the rest (checks, message, RPC to the smelter's owner). Its RemoveItem(item, 1) on our
                // inventory is a no-op for an item that isn't in it; a stack of 2 takes the quiet "not here" path.
                item = selected.Drop.m_itemData.Clone();
                item.m_dropPrefab = selected.Drop.gameObject;
                item.m_stack = 2;
                return true;
            }
            return FetchBatch(smelter, user, selected.Name, smelter.m_maxOre - SmelterInFlight.Queue(smelter));
        }

        /// <returns>False to skip vanilla OnAddFuel.</returns>
        public static bool BeforeAddFuel(Smelter smelter, Humanoid user, ItemDrop.ItemData item, ref bool result)
        {
            if (user == Player.m_localPlayer && Settings.Smelting.Value && smelter.m_fuelItem &&
                SmelterInFlight.Fuel(smelter) > smelter.m_maxFuel - 1)
            {
                user.Message(MessageHud.MessageType.Center, "$msg_itsfull");
                result = false;
                return false;
            }
            if (item != null || user != Player.m_localPlayer || !Hooks.Active(Settings.Smelting.Value) || !smelter.m_fuelItem) return true;
            string fuel = smelter.m_fuelItem.m_itemData.m_shared.m_name;
            // Vanilla handles fuel you carry, a full smelter and (for blocklisted fuel) everything else
            if (user.GetInventory().HaveItem(fuel) ||
                Blocklisted(smelter.m_fuelItem.gameObject.name) || InStorage(fuel) == 0) return true;

            result = false;
            if (ChestAccess.TakeFromOwned(fuel, 1, -1, true, $"{smelter.m_name} fuel") == 1)
            {
                user.Message(MessageHud.MessageType.Center, "$msg_added " + fuel);
                smelter.m_nview.InvokeRPC("RPC_AddFuel");
                result = true;
                return false;
            }
            return FetchBatch(smelter, user, fuel, smelter.m_maxFuel - Mathf.CeilToInt(SmelterInFlight.Fuel(smelter)));
        }

        // Pull a few into the inventory; the next press (or hold-to-repeat) inserts them from there
        private static bool FetchBatch(Smelter smelter, Humanoid user, string name, int wanted)
        {
            if (s_pulls.TryGetValue(smelter, out var pending) && pending.Any(p => !p.Finished))
            {
                user.Message(MessageHud.MessageType.Center, "Storage: still fetching…");
                return false;
            }
            int amount = Mathf.Min(wanted, InStorage(name));
            if (amount <= 0)
            {
                user.Message(MessageHud.MessageType.Center, "$msg_donthaveany " + name);
                return false;
            }
            var slots = new ChestAccess.SlotReservation();
            ChestAccess.PullPlan plan = ChestAccess.PlanPulls(name, -1, amount, slots);
            // Fewer is fine for a smelter: fetch what fits (at least one)
            if (plan.Steps.Count == 0)
            {
                user.Message(MessageHud.MessageType.Center, plan.SlotsShort > 0
                    ? "Storage: need 1 free inventory slot to fetch from chests other players have"
                    : "$msg_donthaveany " + name);
                return false;
            }
            s_pulls[smelter] = ChestAccess.Start(plan, smelter.m_name);
            user.Message(MessageHud.MessageType.Center, $"Storage: fetching {plan.Total} {Localization.instance.Localize(name)}");
            return false;
        }

        /// <summary>True when the default Alt Place + Use cycles materials (no dedicated key set).</summary>
        public static bool UsesAltUse => Settings.ChangeMaterialKey.Value.MainKey == KeyCode.None;

        private static string ChangeKeyLabel() => UsesAltUse ? "$KEY_AltPlace + $KEY_Use" : Settings.ChangeMaterialKey.Value.ToString();

        public static void CycleOnHover(Player player)
        {
            Smelter smelter = HoveredOreSwitchSmelter(player);
            if (smelter) Cycle(smelter, player);
        }

        public static void ToggleBlocklistOnHover(Player player)
        {
            Smelter smelter = HoveredOreSwitchSmelter(player);
            if (!smelter) return;
            Material selected = Selected(smelter, Materials(smelter));
            if (selected == null) return;
            Settings.ToggleBlocklisted(selected.Prefab);
            player.Message(MessageHud.MessageType.TopLeft,
                $"{selected.Name}: {(Blocklisted(selected.Prefab) ? "never put into smelters automatically" : "allowed")}");
        }

        public static Smelter HoveredOreSwitchSmelter(Player player)
        {
            GameObject hover = player ? player.GetHoverObject() : null;
            Switch sw = hover ? hover.GetComponentInParent<Switch>() : null;
            Smelter smelter = sw ? sw.GetComponentInParent<Smelter>() : null;
            return smelter && smelter.m_addOreSwitch == sw ? smelter : null;
        }

        public static void Prune()
        {
            foreach (Smelter dead in s_pulls.Keys.Where(s => !s).ToList()) s_pulls.Remove(dead);
        }
    }

    [HarmonyPatch(typeof(Smelter), nameof(Smelter.OnHoverAddOre))]
    internal static class SmelterHoverOrePatch
    {
        private static void Postfix(Smelter __instance, ref string __result)
        {
            if (!Player.m_localPlayer || !Settings.Smelting.Value) return;
            int queued = __instance.GetQueueSize(), effective = SmelterInFlight.Queue(__instance);
            if (effective != queued)
                __result = ReplaceFirst(__result, $"({queued}/{__instance.m_maxOre})", $"({effective}/{__instance.m_maxOre})");
            if (Hooks.Active(Settings.Smelting.Value)) __result += SmeltFlow.HoverLines(__instance);
        }

        public static string ReplaceFirst(string text, string old, string replacement)
        {
            int at = text.IndexOf(old, System.StringComparison.Ordinal);
            return at < 0 ? text : text.Substring(0, at) + replacement + text.Substring(at + old.Length);
        }
    }

    [HarmonyPatch(typeof(Smelter), nameof(Smelter.OnHoverAddFuel))]
    internal static class SmelterHoverFuelPatch
    {
        private static void Postfix(Smelter __instance, ref string __result)
        {
            if (!Player.m_localPlayer || !__instance.m_fuelItem || !Settings.Smelting.Value) return;
            float shown = Mathf.Ceil(__instance.GetFuel()), effective = Mathf.Ceil(SmelterInFlight.Fuel(__instance));
            if (effective != shown)
                __result = SmelterHoverOrePatch.ReplaceFirst(__result, $" {shown}/{__instance.m_maxFuel})", $" {effective}/{__instance.m_maxFuel})");
            if (!Hooks.Active(Settings.Smelting.Value)) return;
            string fuel = __instance.m_fuelItem.m_itemData.m_shared.m_name;
            if (SmeltFlow.InInventory(fuel) + StorageNetwork.Count(fuel, -1, true, true) == 0) ChestAccess.BorrowFor(fuel);
            __result += Localization.instance.Localize($"\n{SmeltFlow.InStorage(fuel)} {fuel} in storage");
        }
    }

    [HarmonyPatch(typeof(Smelter), nameof(Smelter.OnAddOre))]
    internal static class SmelterAddOrePatch
    {
        private static bool Prefix(Smelter __instance, Humanoid user, ref ItemDrop.ItemData item, ref bool __result) =>
            SmeltFlow.BeforeAddOre(__instance, user, ref item, ref __result);

        private static void Postfix(Smelter __instance, Humanoid user, bool __result)
        {
            if (__result && user == Player.m_localPlayer) SmelterInFlight.OreSent(__instance);
        }
    }

    [HarmonyPatch(typeof(Smelter), nameof(Smelter.OnAddFuel))]
    internal static class SmelterAddFuelPatch
    {
        private static bool Prefix(Smelter __instance, Humanoid user, ItemDrop.ItemData item, ref bool __result) =>
            SmeltFlow.BeforeAddFuel(__instance, user, item, ref __result);

        private static void Postfix(Smelter __instance, Humanoid user, bool __result)
        {
            if (__result && user == Player.m_localPlayer) SmelterInFlight.FuelSent(__instance);
        }
    }

    // Alt Place + Use (Shift+E) on a smelter's input changes the material, unless a dedicated key is set (vanilla passes
    // the alt flag to switches and ignores it)
    [HarmonyPatch(typeof(Switch), nameof(Switch.Interact))]
    internal static class SwitchInteractPatch
    {
        private static bool Prefix(Switch __instance, Humanoid character, bool hold, bool alt, ref bool __result)
        {
            if (!alt || !SmeltFlow.UsesAltUse || character != Player.m_localPlayer || !Hooks.Active(Settings.Smelting.Value)) return true;
            Smelter smelter = __instance.GetComponentInParent<Smelter>();
            if (!smelter || smelter.m_addOreSwitch != __instance) return true;
            __result = false;
            // Holding Shift+E must neither insert nor keep cycling
            if (!hold)
            {
                SmeltFlow.Cycle(smelter, character);
                __result = true;
            }
            return false;
        }
    }
}
