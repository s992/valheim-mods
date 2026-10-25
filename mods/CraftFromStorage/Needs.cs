using System.Collections.Generic;

namespace CraftFromStorage
{
    // What an action is about to consume, mirroring vanilla's own rules (ConsumeResources / DoCrafting)
    internal readonly struct Need
    {
        public readonly string Name;
        public readonly int Amount;
        public readonly int Quality;

        public Need(string name, int amount, int quality)
        {
            Name = name;
            Amount = amount;
            Quality = quality;
        }

        /// <summary>How much of it the inventory plus chests we own hold right now (what can be paid synchronously).</summary>
        public int PayableNow(Inventory inventory) =>
            inventory.CountItems(Name, Quality, true) + StorageNetwork.Count(Name, Quality, true, true);
    }

    internal static class Needs
    {
        public static List<Need> ForRecipe(Player player, Recipe recipe, int quality, int multiplier)
        {
            var needs = new List<Need>();
            if (!recipe || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost)) return needs;
            if (recipe.m_requireOnlyOneIngredient)
            {
                recipe.GetAmount(quality, out int need, out ItemDrop.ItemData single, multiplier);
                if (single != null && need > 0) needs.Add(new Need(single.m_shared.m_name, need, single.m_quality));
                return needs;
            }
            // Same filter as Player.ConsumeResources
            CraftingStation station = player.GetCurrentCraftingStation();
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if ((station != null && station.m_upgrader != requirement.m_upgraderResource) ||
                    (station == null && requirement.m_upgraderResource) || !requirement.m_resItem) continue;
                int amount = requirement.GetAmount(quality) * multiplier;
                if (amount > 0) Add(needs, requirement.m_resItem.m_itemData.m_shared.m_name, amount);
            }
            return needs;
        }

        public static List<Need> ForPiece(Piece piece)
        {
            var needs = new List<Need>();
            if (!piece || ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey())) return needs;
            foreach (Piece.Requirement requirement in piece.m_resources)
                if (requirement.m_resItem && requirement.m_amount > 0)
                    Add(needs, requirement.m_resItem.m_itemData.m_shared.m_name, requirement.m_amount);
            return needs;
        }

        private static void Add(List<Need> needs, string name, int amount)
        {
            for (int i = 0; i < needs.Count; i++)
            {
                if (needs[i].Name != name) continue;
                needs[i] = new Need(name, needs[i].Amount + amount, -1);
                return;
            }
            needs.Add(new Need(name, amount, -1));
        }

        /// <summary>
        /// Plans pulls for everything the inventory and owned chests can't cover. All-or-nothing: returns null (with a
        /// message for the player) if anything is missing or there's no room, so nothing half-arrives.
        /// </summary>
        public static List<ChestAccess.PullPlan> PlanShortfall(List<Need> needs, Inventory inventory, out string problem)
        {
            problem = null;
            var plans = new List<ChestAccess.PullPlan>();
            var slots = new ChestAccess.SlotReservation();
            int slotsShort = 0;
            foreach (Need need in needs)
            {
                int shortfall = need.Amount - need.PayableNow(inventory);
                if (shortfall <= 0) continue;
                ChestAccess.PullPlan plan = ChestAccess.PlanPulls(need.Name, need.Quality, shortfall, slots);
                if (plan.Missing > 0)
                {
                    problem = $"Storage: not enough {Localization.instance.Localize(need.Name)} in reach";
                    return null;
                }
                slotsShort += plan.SlotsShort;
                plans.Add(plan);
            }
            if (slotsShort > 0)
            {
                problem = $"Storage: need {slotsShort} free inventory slot{(slotsShort == 1 ? "" : "s")} to fetch materials from chests other players have";
                return null;
            }
            return plans;
        }
    }
}
