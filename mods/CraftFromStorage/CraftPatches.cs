using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using ValheimMods.Shared;

namespace CraftFromStorage
{
    // Counting: requirement checks see inventory + storage
    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirementItems))]
    internal static class HaveRequirementItemsPatch
    {
        // Runs every frame: contain our own errors (see PatchGuard)
        private static Exception Finalizer(Exception __exception) => PatchGuard.Contain(__exception, "counting materials in storage");

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            Hooks.ReplaceCalls(instructions, Hooks.CountItems, Hooks.Self(nameof(Hooks.CountForCrafting)), 1, "Player.HaveRequirementItems");
    }

    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
    internal static class HaveRequirementsPiecePatch
    {
        // Runs every frame: contain our own errors (see PatchGuard)
        private static Exception Finalizer(Exception __exception) => PatchGuard.Contain(__exception, "counting materials in storage");

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var counted = Hooks.ReplaceCalls(instructions, Hooks.CountItems, Hooks.Self(nameof(Hooks.CountForBuilding)), 1, "Player.HaveRequirements(Piece)");
            return Hooks.ReplaceCalls(counted, Hooks.HaveItem, Hooks.Self(nameof(Hooks.HaveForBuilding)), 1, "Player.HaveRequirements(Piece)");
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.GetFirstRequiredItem))]
    internal static class GetFirstRequiredItemPatch
    {
        // Runs every frame: contain our own errors (see PatchGuard)
        private static Exception Finalizer(Exception __exception) => PatchGuard.Contain(__exception, "counting materials in storage");

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var counted = Hooks.ReplaceCalls(instructions, Hooks.CountItems, Hooks.Self(nameof(Hooks.CountForCrafting)), 1, "Player.GetFirstRequiredItem");
            return Hooks.ReplaceCalls(counted, Hooks.GetItem, Hooks.Self(nameof(Hooks.GetItemForCrafting)), 1, "Player.GetFirstRequiredItem");
        }
    }

    // Consumption: inventory first (vanilla), the rest from chests we own
    [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
    internal static class ConsumeResourcesPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            Hooks.ReplaceCalls(instructions, Hooks.RemoveItemByName, Hooks.Self(nameof(Hooks.RemoveWithStorage)), 1, "Player.ConsumeResources");
    }

    // Requirement display: "have/need" (105/10), flashing red only when the combined count is short
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
    internal static class SetupRequirementPatch
    {
        // Runs every frame: contain our own errors (see PatchGuard)
        private static Exception Finalizer(Exception __exception) => PatchGuard.Contain(__exception, "the storage counts in the crafting menu");

        private static void Postfix(bool __result, Transform elementRoot, Piece.Requirement req, Player player, bool craft, int quality, int craftMultiplier)
        {
            if (!__result || req?.m_resItem == null || player != Player.m_localPlayer) return;
            bool feature = craft ? Settings.Crafting.Value : Settings.Building.Value;
            if (!Hooks.Active(feature)) return;
            Transform amount = elementRoot.transform.Find("res_amount");
            var amountText = amount ? amount.GetComponent<TMPro.TMP_Text>() : null;
            if (!amountText) return;

            string name = req.m_resItem.m_itemData.m_shared.m_name;
            int have = player.GetInventory().CountItems(name) + StorageNetwork.Count(name, -1, true, false);
            int need = req.GetAmount(quality) * craftMultiplier;
            amountText.text = $"{have}/{need}";
            bool free = ZoneSystem.instance.GetGlobalKey(craft ? GlobalKeys.NoCraftCost : GlobalKeys.NoBuildCost);
            amountText.color = have < need && !free && Mathf.Sin(Time.time * 10f) > 0f ? Color.red : Color.white;
        }
    }

    // Crafting has a timer: on press, start pulling what only chests other players own have; when the timer ends,
    // wait (briefly) for those pulls, then craft only if everything can be paid from the inventory and owned chests.
    internal static class CraftFlow
    {
        private const float MaxWait = 5f;

        private static List<ChestAccess.Pull> s_pulls;
        private static float s_deadline;
        private static bool s_resume;

        private static List<Need> CurrentNeeds(InventoryGui gui, Player player)
        {
            int quality = gui.m_craftUpgradeItem == null ? 1 : gui.m_craftUpgradeItem.m_quality + 1;
            int multiplier = gui.m_multiCrafting ? gui.m_multiCraftAmount : 1;
            return Needs.ForRecipe(player, gui.m_craftRecipe, quality, multiplier);
        }

        public static void OnCraftPressed(InventoryGui gui)
        {
            s_pulls = null;
            Player player = Player.m_localPlayer;
            if (gui.m_craftTimer < 0f || !player || !Hooks.Active(Settings.Crafting.Value)) return;
            List<Need> needs = CurrentNeeds(gui, player);
            List<ChestAccess.PullPlan> plans = Needs.PlanShortfall(needs, player.GetInventory(), out string problem);
            if (plans == null)
            {
                player.Message(MessageHud.MessageType.Center, problem);
                gui.m_craftTimer = -1f;
                return;
            }
            if (plans.Count == 0) return;
            s_pulls = plans.SelectMany(plan => ChestAccess.Start(plan, $"crafting {gui.m_craftRecipe.name}")).ToList();
            s_deadline = Time.time + MaxWait;
        }

        /// <summary>
        /// Whether the craft that fetched materials should hold off paying. Until the deadline: while fetches are on their
        /// way, and while a chest is being handed to us and the cost can't be paid yet. A fetch from a chest whose owner
        /// just handed it to us comes back empty, and the items are only payable once our copy shows us as the owner.
        /// </summary>
        public static bool ShouldWait(bool pullsPending, bool borrowsInFlight, bool payableNow, bool beforeDeadline) =>
            beforeDeadline && (pullsPending || (borrowsInFlight && !payableNow));

        /// <returns>False to skip this DoCrafting call.</returns>
        public static bool BeforeDoCrafting(InventoryGui gui, Player player)
        {
            if (player != Player.m_localPlayer || !Hooks.Active(Settings.Crafting.Value) || gui.m_craftRecipe == null) return true;
            Inventory inventory = player.GetInventory();
            List<Need> needs = CurrentNeeds(gui, player);
            if (s_pulls != null && ShouldWait(s_pulls.Any(p => !p.Finished), ChestAccess.BorrowsInFlight,
                    needs.All(n => n.PayableNow(inventory) >= n.Amount), Time.time < s_deadline))
            {
                s_resume = true;
                return false;
            }
            s_pulls = null;
            foreach (Need need in needs)
            {
                if (need.PayableNow(inventory) >= need.Amount) continue;
                // Counts include chests other players own; if their items didn't arrive, don't craft for free
                player.Message(MessageHud.MessageType.Center,
                    $"Storage: {Localization.instance.Localize(need.Name)} didn't arrive from storage, crafting stopped");
                return false;
            }
            return true;
        }

        public static void AfterUpdateRecipe(InventoryGui gui)
        {
            if (!s_resume) return;
            s_resume = false;
            // UpdateRecipe set the timer to -1 after our skipped DoCrafting; keep it past the end so it retries next frame
            gui.m_craftTimer = 10000f;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnCraftPressed))]
    internal static class OnCraftPressedPatch
    {
        private static void Postfix(InventoryGui __instance) => CraftFlow.OnCraftPressed(__instance);
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
    internal static class DoCraftingPatch
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(InventoryGui __instance, Player player) => CraftFlow.BeforeDoCrafting(__instance, player);

        // The one-ingredient branch removes with RemoveItem(name, need, quality) directly
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            Hooks.ReplaceCalls(instructions, Hooks.RemoveItemByName, Hooks.Self(nameof(Hooks.RemoveWithStorage)), 1, "InventoryGui.DoCrafting");
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe))]
    internal static class UpdateRecipePatch
    {
        // Runs every frame: contain our own errors (see PatchGuard)
        private static Exception Finalizer(Exception __exception) => PatchGuard.Contain(__exception, "crafting from storage");

        private static void Postfix(InventoryGui __instance) => CraftFlow.AfterUpdateRecipe(__instance);
    }

    // Building is instant, so materials only in chests other players own are fetched first and the click doesn't place;
    // the next click does (once they've arrived)
    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    internal static class TryPlacePiecePatch
    {
        private static List<ChestAccess.Pull> s_pulls;

        private static bool Prefix(Player __instance, Piece piece, ref bool __result)
        {
            if (__instance != Player.m_localPlayer || __instance.m_noPlacementCost || !Hooks.Active(Settings.Building.Value)) return true;
            List<Need> needs = Needs.ForPiece(piece);
            Inventory inventory = __instance.GetInventory();
            if (needs.All(n => n.PayableNow(inventory) >= n.Amount)) return true;

            __result = false;
            if (s_pulls != null && s_pulls.Any(p => !p.Finished))
            {
                __instance.Message(MessageHud.MessageType.Center, "Storage: still fetching materials…");
                return false;
            }
            foreach (Need need in needs) ChestAccess.BorrowFor(need.Name);
            List<ChestAccess.PullPlan> plans = Needs.PlanShortfall(needs, inventory, out string problem);
            if (plans == null)
            {
                __instance.Message(MessageHud.MessageType.Center, problem);
                return false;
            }
            s_pulls = plans.SelectMany(plan => ChestAccess.Start(plan, $"building {piece.name}")).ToList();
            __instance.Message(MessageHud.MessageType.Center, "Storage: fetching materials, place again in a moment");
            return false;
        }
    }
}
