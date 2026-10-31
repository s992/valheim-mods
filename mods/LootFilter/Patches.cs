using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ValheimMods.Shared;

namespace LootFilter
{
    // Player.AutoPickup skips drops whose m_autoPickup is false (vanilla uses this for items
    // you dropped yourself). The check is swapped for one that also respects the filter.
    [HarmonyPatch(typeof(Player), nameof(Player.AutoPickup))]
    internal static class AutoPickupPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo autoPickup = AccessTools.Field(typeof(ItemDrop), nameof(ItemDrop.m_autoPickup));
            MethodInfo replacement = AccessTools.Method(typeof(AutoPickupPatch), nameof(CanAutoPickup));
            int replaced = 0;

            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.LoadsField(autoPickup))
                {
                    replaced++;
                    yield return new CodeInstruction(OpCodes.Call, replacement)
                    {
                        labels = instruction.labels,
                        blocks = instruction.blocks,
                    };
                }
                else
                {
                    yield return instruction;
                }
            }

            SafePatching.ExpectReplaced(replaced, 1, "Player.AutoPickup: m_autoPickup checks");
        }

        private static bool CanAutoPickup(ItemDrop drop) =>
            drop.m_autoPickup && !Rules.BlocksAutoPickup(drop.m_itemData);
    }

    // My Build Camera (EndosBuildCamera) picks up resources near its free camera in
    // BuildCameraBase.ExecuteCameraAutoPickup: for each drop that passes its checks, ending with
    // Inventory.CanAddItem, it moves the drop to the player and picks it up. That CanAddItem call is
    // swapped for one that also respects the filter, so filtered drops are skipped before they're moved.
    // Applied by hand from Plugin.Awake, only when the mod is installed.
    internal static class BuildCameraPickupPatch
    {
        public const string Guid = "EndosBuildCamera";

        public static void Apply(Harmony harmony, BepInEx.Logging.ManualLogSource log)
        {
            MethodInfo target = AccessTools.Method(AccessTools.TypeByName("MyValheimBuildCamera.BuildCameraBase"), "ExecuteCameraAutoPickup");
            if (target == null)
            {
                log.LogWarning("My Build Camera's camera pickup method wasn't found (different version?); its pickups aren't filtered");
                return;
            }
            try
            {
                harmony.Patch(target, transpiler: new HarmonyMethod(typeof(BuildCameraPickupPatch), nameof(Transpiler)));
            }
            catch (Exception e)
            {
                // Optional: the rest of the filter keeps working without it
                log.LogWarning($"My Build Camera's camera pickup changed (different version?); its pickups aren't filtered. {e.Message}");
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo canAddItem = AccessTools.Method(typeof(Inventory), nameof(Inventory.CanAddItem),
                new[] { typeof(ItemDrop.ItemData), typeof(int) });
            MethodInfo replacement = AccessTools.Method(typeof(BuildCameraPickupPatch), nameof(CanAddItem));
            int replaced = 0;

            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(canAddItem))
                {
                    replaced++;
                    yield return new CodeInstruction(OpCodes.Call, replacement)
                    {
                        labels = instruction.labels,
                        blocks = instruction.blocks,
                    };
                }
                else
                {
                    yield return instruction;
                }
            }

            SafePatching.ExpectReplaced(replaced, 1, "BuildCameraBase.ExecuteCameraAutoPickup: CanAddItem calls");
        }

        // Same stack shape as the instance call it replaces: (inventory, item, stack) -> bool
        private static bool CanAddItem(Inventory inventory, ItemDrop.ItemData item, int stack) =>
            inventory.CanAddItem(item, stack) && !Rules.BlocksAutoPickup(item);
    }

    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
        typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
    internal static class TooltipPatch
    {
        private static void Postfix(ItemDrop.ItemData item, bool crafting, ref string __result)
        {
            if (crafting || !Settings.ShowInTooltip.Value) return;

            string paused = Rules.Paused ? ", paused" : "";
            switch (Rules.Explain(item))
            {
                case FilterReason.Item:
                    __result += $"\n<color=#ff9a5c>Loot filter: filtered{(Rules.Paused ? " (paused)" : "")}</color>";
                    break;
                case FilterReason.Group:
                    __result += $"\n<color=#ff9a5c>Loot filter: filtered (group: {Groups.Describe(Groups.Of(item))}{paused})</color>";
                    break;
            }
        }
    }
}
