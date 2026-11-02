using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ValheimMods.Shared;

namespace UtilityBelt
{
    // ---- Upgrading at the workbench ----
    //
    // The game prices an upgrade from the recipe and the target quality alone. A belt's next upgrade depends on which
    // ones it already has (tiers that were off when it was upgraded got skipped), so these patches tell the cost
    // code which belt is being looked at.

    internal static class UpgradeContext
    {
        // The belt the crafting panel is working on, while it's working on one
        public static ItemDrop.ItemData Item;

        /// <summary>The belt being upgraded to a quality: the one in context, else the first in the inventory.</summary>
        public static ItemDrop.ItemData Resolve(int targetQuality)
        {
            if (Belt.IsBelt(Item) && Item.m_quality == targetQuality - 1) return Item;
            Player player = Player.m_localPlayer;
            if (!player) return null;
            return player.GetInventory().GetAllItems()
                .FirstOrDefault(item => Belt.IsBelt(item) && item.m_quality == targetQuality - 1);
        }

        public static Tier? NextTier(ItemDrop.ItemData belt) =>
            belt == null ? null : Settings.Rules.Next(Belt.Owned(belt));
    }

    // The cost of each resource for the belt's next upgrade. No finalizer: this is also what's charged, so a failure
    // must stop the upgrade rather than make it free.
    [HarmonyPatch(typeof(Piece.Requirement), nameof(Piece.Requirement.GetAmount))]
    internal static class RequirementAmount
    {
        private static bool Prefix(Piece.Requirement __instance, int qualityLevel, ref int __result)
        {
            if (qualityLevel <= 1 || !Belt.IsBeltRequirement(__instance)) return true;
            ItemDrop.ItemData belt = UpgradeContext.Resolve(qualityLevel);
            Tier? next = UpgradeContext.NextTier(belt);
            __result = next != null && __instance.m_resItem
                ? Settings.Rules.Amount(next.Value, __instance.m_resItem.gameObject.name)
                : 0;
            string resource = __instance.m_resItem ? __instance.m_resItem.gameObject.name : "<null>";
            UpgradeLog.Changed($"GetAmount({resource}, q{qualityLevel})",
                $"= {__result} for next={next?.ToString() ?? "none"} context={(UpgradeContext.Item == null ? "none" : "set")} " +
                $"belt={UpgradeLog.Describe(belt)}");
            return false;
        }
    }

    // No upgrade left (every remaining tier is off) means it can't be done, rather than a free upgrade to nothing.
    // A new belt can't be crafted either: Haldor sells them.
    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Recipe), typeof(bool), typeof(int), typeof(int))]
    internal static class UpgradeAllowed
    {
        private static bool Prefix(Recipe recipe, bool discover, int qualityLevel, ref bool __result)
        {
            if (discover || !Belt.IsBeltRecipe(recipe)) return true;
            if (qualityLevel > 1 && UpgradeContext.NextTier(UpgradeContext.Resolve(qualityLevel)) != null) return true;
            __result = false;
            UpgradeLog.Changed($"HaveRequirements(q{qualityLevel})",
                $"blocked by the mod: belt={UpgradeLog.Describe(UpgradeContext.Resolve(qualityLevel))}");
            return false;
        }

        private static void Postfix(Player __instance, Recipe recipe, bool discover, int qualityLevel, bool __result)
        {
            if (discover || !Belt.IsBeltRecipe(recipe)) return;
            UpgradeLog.Changed($"HaveRequirements(q{qualityLevel})",
                $"= {__result} station={(__instance.GetCurrentCraftingStation() ? __instance.GetCurrentCraftingStation().GetLevel().ToString(System.Globalization.CultureInfo.InvariantCulture) : "none")} " +
                $"stationOk={__instance.RequiredCraftingStation(recipe, qualityLevel, true)} noCost={__instance.NoCostCheat()} " +
                $"belt={UpgradeLog.Describe(UpgradeContext.Resolve(qualityLevel))}");
        }
    }

    // Every upgrade works at a level 1 workbench: the game wants one level per quality, more than a workbench has
    [HarmonyPatch(typeof(Recipe), nameof(Recipe.GetRequiredStationLevel))]
    internal static class StationLevel
    {
        private static bool Prefix(Recipe __instance, ref int __result)
        {
            if (!Belt.IsBeltRecipe(__instance)) return true;
            __result = Math.Max(1, __instance.m_minStationLevel);
            return false;
        }
    }

    // The upgrade list: leaves out belts with nothing left to buy, and checks each belt's own cost
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.AddRecipeToList))]
    internal static class UpgradeList
    {
        private static bool Prefix(Player player, Recipe recipe, ItemDrop.ItemData item, ref bool canCraft)
        {
            if (item == null || !Belt.IsBeltRecipe(recipe)) return true;
            if (UpgradeContext.NextTier(item) == null)
            {
                UpgradeLog.Write($"upgrade list: left out (nothing to buy) {UpgradeLog.Describe(item)}");
                return false;
            }
            bool vanilla = canCraft;
            UpgradeContext.Item = item;
            try
            {
                canCraft = player.HaveRequirements(recipe, false, item.m_quality + 1) ||
                           ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost);
            }
            finally
            {
                UpgradeContext.Item = null;
            }
            UpgradeLog.Write($"upgrade list: canCraft={canCraft} (game said {vanilla}) {UpgradeLog.Describe(item)}");
            return true;
        }

        private static Exception Finalizer(Exception __exception) =>
            PatchGuard.Contain(__exception, "the belt's upgrade list");
    }

    // The selected recipe's panel (requirements, craft button), redrawn every frame while it's open
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe))]
    internal static class UpgradePanel
    {
        private static void Prefix(InventoryGui __instance)
        {
            UpgradeContext.Item = __instance.m_selectedRecipe.ItemData;
            if (Belt.IsBeltRecipe(__instance.m_selectedRecipe.Recipe))
                UpgradeLog.Changed("panel", $"selected {UpgradeLog.Describe(UpgradeContext.Item)}");
        }

        private static Exception Finalizer(Exception __exception)
        {
            UpgradeContext.Item = null;
            return PatchGuard.Contain(__exception, "the belt's upgrade panel");
        }
    }

    // The upgrade itself. The game replaces the belt with a new item one quality up, and the new upgrade is written
    // onto that item afterwards. Whether the new item already carries the old one's data varies (in testing it did
    // from the second upgrade on), so it's found by quality and position, never by whether it has a record.
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
    internal static class Upgrade
    {
        internal sealed class Pending
        {
            public ItemDrop.ItemData Old;
            public Vector2i Position;
            public int Quality;
            public Dictionary<string, string> Data;

            // The new belt: one quality up and not the old item itself. Several could match only when the player
            // carries another belt already at that quality; one without a different record is preferred then.
            public bool IsUpgraded(ItemDrop.ItemData item) =>
                Belt.IsBelt(item) && item.m_quality == Quality && !ReferenceEquals(item, Old);

            public bool HasOtherRecord(ItemDrop.ItemData item) =>
                item.m_customData.TryGetValue(Tiers.DataKey, out string text) &&
                !(Old.m_customData.TryGetValue(Tiers.DataKey, out string oldText) && text == oldText);
        }

        private static bool Prefix(InventoryGui __instance, out Pending __state)
        {
            __state = null;
            ItemDrop.ItemData old = __instance.m_craftUpgradeItem;
            if (!Belt.IsBeltRecipe(__instance.m_craftRecipe)) return true;
            Player player = Player.m_localPlayer;
            UpgradeLog.Write($"DoCrafting start: upgrading {UpgradeLog.Describe(old)}; {UpgradeLog.Rules()}; " +
                             $"noCost={player.NoCostCheat()} noCraftCostKey={ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost)}; " +
                             $"inventory: {UpgradeLog.Belts(player)}; {UpgradeLog.Recipe(__instance.m_craftRecipe)}");
            if (old == null) return true;
            UpgradeContext.Item = old;
            var owned = new HashSet<Tier>(Belt.Owned(old));
            Tier? next = Settings.Rules.Next(owned);
            // Nothing left to buy; the no-cost cheats would otherwise raise its quality for nothing
            if (next == null)
            {
                UpgradeLog.Write("DoCrafting skipped: nothing left to buy");
                return false;
            }
            UpgradeLog.Write($"DoCrafting: next={next}, game will check HaveRequirements(q{old.m_quality + 1}) = " +
                             $"{player.HaveRequirements(__instance.m_craftRecipe, false, old.m_quality + 1)}");
            owned.Add(next.Value);
            __state = new Pending
            {
                Old = old,
                Position = old.m_gridPos,
                Quality = old.m_quality + 1,
                Data = new Dictionary<string, string>(old.m_customData) { [Tiers.DataKey] = Tiers.Format(owned) },
            };
            return true;
        }

        private static void Postfix(Player player, Pending __state)
        {
            if (__state == null) return;
            Inventory inventory = player.GetInventory();
            ItemDrop.ItemData upgraded = inventory.GetItemAt(__state.Position.x, __state.Position.y);
            UpgradeLog.Write($"DoCrafting end: expected q{__state.Quality} at {__state.Position}, found there {UpgradeLog.Describe(upgraded)}");
            if (!__state.IsUpgraded(upgraded))
            {
                // Not where the old one was (another mod moved it)
                List<ItemDrop.ItemData> candidates = inventory.GetAllItems().Where(__state.IsUpgraded).ToList();
                upgraded = candidates.FirstOrDefault(item => !__state.HasOtherRecord(item)) ?? candidates.FirstOrDefault();
                UpgradeLog.Write($"DoCrafting end: searched the inventory, found {UpgradeLog.Describe(upgraded)}");
            }
            // None when the game didn't upgrade after all (requirements no longer met)
            if (upgraded == null)
            {
                UpgradeLog.Write($"DoCrafting end: no upgraded belt; inventory: {UpgradeLog.Belts(player)}");
                return;
            }
            upgraded.m_customData = __state.Data;
            UpgradeLog.Write($"DoCrafting end: recorded {UpgradeLog.Describe(upgraded)}; inventory: {UpgradeLog.Belts(player)}");
        }

        // Clears the context whatever happened; exceptions go on and stop the upgrade (it's paid for)
        private static Exception Finalizer(Exception __exception)
        {
            UpgradeContext.Item = null;
            return __exception;
        }
    }

    // ---- What the upgrades do ----

    // Wishbone, wisplight, feather fall and the extra carry weight are the same status effects the real items give
    // while worn, added alongside the game's own equipment effects
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UpdateEquipmentStatusEffects))]
    internal static class EquipmentEffects
    {
        // The effects this mod added to each character, so it removes only its own
        private static readonly ConditionalWeakTable<Humanoid, HashSet<int>> s_added =
            new ConditionalWeakTable<Humanoid, HashSet<int>>();

        private static void Postfix(Humanoid __instance)
        {
            HashSet<int> added = s_added.GetOrCreateValue(__instance);
            var wanted = new List<StatusEffect>();
            ItemDrop.ItemData belt = __instance.m_utilityItem;
            if (Belt.IsBelt(belt))
            {
                HashSet<Tier> owned = Belt.Owned(belt);
                foreach (Tier tier in Tiers.All)
                {
                    if (!Settings.Rules.IsActive(owned, tier) || !Settings.Wanted(tier)) continue;
                    StatusEffect effect = Belt.EffectFor(tier);
                    if (effect) wanted.Add(effect);
                }
            }
            var wantedHashes = new HashSet<int>(wanted.Select(e => e.NameHash()));

            foreach (int hash in added.Where(h => !wantedHashes.Contains(h)).ToList())
            {
                added.Remove(hash);
                // Leave it when worn gear gives it too (a feather cape with the belt)
                if (!__instance.m_equipmentStatusEffects.Any(e => e.NameHash() == hash))
                    __instance.m_seman.RemoveStatusEffect(hash);
            }
            foreach (StatusEffect effect in wanted)
            {
                added.Add(effect.NameHash());
                // Also after the game removed it for gear just taken off that gave the same effect
                if (!__instance.m_seman.HaveStatusEffect(effect.NameHash())) __instance.m_seman.AddStatusEffect(effect);
            }
        }

        private static Exception Finalizer(Exception __exception) =>
            PatchGuard.Contain(__exception, "the belt's effects");
    }

    // Frost resistance: the wolf fur cape's damage modifiers, which is also what stops freezing
    [HarmonyPatch(typeof(Player), nameof(Player.ApplyArmorDamageMods))]
    internal static class FrostResistance
    {
        private static void Postfix(Player __instance, ref HitData.DamageModifiers mods)
        {
            if (!Belt.HasActive(__instance, Tier.FrostResist)) return;
            ItemDrop.ItemData.SharedData cape = Belt.SharedOf(Tiers.UpgradeItem(Tier.FrostResist));
            if (cape != null) mods.Apply(cape.m_damageModifiers);
        }

        private static Exception Finalizer(Exception __exception) =>
            PatchGuard.Contain(__exception, "the belt's frost resistance");
    }

    // The swamp key: doors that want one open for a belt with that upgrade
    [HarmonyPatch(typeof(Door), nameof(Door.HaveKey))]
    internal static class SwampKey
    {
        private static void Postfix(Door __instance, Humanoid player, ref bool __result)
        {
            if (__result || !__instance.m_keyItem ||
                __instance.m_keyItem.gameObject.name != Tiers.UpgradeItem(Tier.SwampKey)) return;
            if (Belt.HasActive(player, Tier.SwampKey)) __result = true;
        }
    }

    // ---- Haldor and the tooltip ----

    // Haldor lists the belt right after Megingjord whenever he offers Megingjord, at the server's price. Added to
    // what he shows rather than to his stock, so nothing is left behind when the mod is removed.
    [HarmonyPatch(typeof(Trader), nameof(Trader.GetAvailableItems))]
    internal static class HaldorStock
    {
        private const string Megingjord = "BeltStrength";
        private static Trader.TradeItem s_belt;

        private static void Postfix(List<Trader.TradeItem> __result)
        {
            int index = __result.FindIndex(t => t.m_prefab && t.m_prefab.name == Megingjord);
            if (index < 0 || !Belt.Prefab) return;
            Trader.TradeItem megingjord = __result[index];
            s_belt ??= new Trader.TradeItem
            {
                m_prefab = Belt.Prefab,
                m_stack = 1,
                // The store reads these without a null check (Unity fills them with "" for the game's own items);
                // a null tooltip broke the rest of Haldor's list
                m_name = "",
                m_tooltip = "",
                m_buyPlayerEffects = megingjord.m_buyPlayerEffects ?? new EffectList(),
                m_levelUpEffect = megingjord.m_levelUpEffect,
            };
            s_belt.m_price = Settings.Price.Value;
            if (!__result.Contains(s_belt)) __result.Insert(index + 1, s_belt);
        }

        private static Exception Finalizer(Exception __exception) =>
            PatchGuard.Contain(__exception, "Haldor's belt listing");
    }

    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
        typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
    internal static class Tooltip
    {
        private static void Postfix(ItemDrop.ItemData item, ref string __result)
        {
            if (!Belt.IsBelt(item)) return;
            // The crafting panel describes the recipe's template item, which never has upgrades: describe the belt
            // being upgraded instead
            ItemDrop.ItemData belt = Belt.Prefab && ReferenceEquals(item, Belt.Prefab.m_itemData) &&
                                     Belt.IsBelt(UpgradeContext.Item)
                ? UpgradeContext.Item
                : item;
            __result += Settings.Rules.Describe(Belt.Owned(belt), Settings.Wanted);
        }

        private static Exception Finalizer(Exception __exception) =>
            PatchGuard.Contain(__exception, "the belt's tooltip");
    }
}
