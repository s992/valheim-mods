using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

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

            if (replaced != 1)
                Debug.LogWarning($"[LootFilter] Expected one m_autoPickup check in Player.AutoPickup, found {replaced}; auto-pickup filtering may not work");
        }

        private static bool CanAutoPickup(ItemDrop drop) =>
            drop.m_autoPickup && !(Settings.BlockAutoPickup.Value && Rules.IsFiltered(drop.m_itemData));
    }

    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
        typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
    internal static class TooltipPatch
    {
        private static void Postfix(ItemDrop.ItemData item, bool crafting, ref string __result)
        {
            if (crafting || !Settings.ShowInTooltip.Value) return;

            switch (Rules.Explain(item))
            {
                case FilterReason.Item:
                    __result += "\n<color=#ff9a5c>Loot filter: filtered</color>";
                    break;
                case FilterReason.Group:
                    __result += $"\n<color=#ff9a5c>Loot filter: filtered (group: {Groups.Describe(Groups.Of(item))})</color>";
                    break;
            }
        }
    }
}
