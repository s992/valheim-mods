using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Valheim.UI;

namespace EverythingsANail
{
    // Become the stored tool just before the hammer is equipped (so the game equips it as that
    // tool: hand slot, build mode, animations), and undo it if equipping fails
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
    internal static class EquipItemPatch
    {
        // First, so other mods' prefixes (Epic Loot predicts which hand items get displaced) see the tool
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Humanoid __instance, ItemDrop.ItemData item)
        {
            // Player.m_localPlayer is still null while the character loads and re-equips its items
            if (!(__instance is Player player) || (Player.m_localPlayer && player != Player.m_localPlayer)) return;
            if (!Impersonation.IsHammer(item) || __instance.IsItemEquiped(item)) return;
            Impersonation.Disguise(player, item);
        }

        private static void Postfix(Humanoid __instance, ItemDrop.ItemData item)
        {
            if (Impersonation.IsHammer(item) && !__instance.IsItemEquiped(item)) Impersonation.Restore(item);
        }
    }

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem))]
    internal static class UnequipItemPatch
    {
        private static void Postfix(Humanoid __instance, ItemDrop.ItemData item)
        {
            if (Impersonation.IsHammer(item) && !__instance.IsItemEquiped(item)) Impersonation.Restore(item);
        }
    }

    // Safety net: a copy of the hammer (moved to a chest, dropped, put on a stand) is always a hammer
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.Clone))]
    internal static class ItemClonePatch
    {
        private static void Postfix(ItemDrop.ItemData __result) => Impersonation.Restore(__result);
    }

    // Show the tool's model in hand rather than the hammer's. Needed beyond looks: the fishing line
    // attaches to the rod model, and PlanBuild recognises its plan hammer by the held model.
    // VisEquipment is synced, so other players see the tool too.
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.SetupVisEquipment))]
    internal static class SetupVisEquipmentPatch
    {
        private static void Postfix(Humanoid __instance, VisEquipment visEq, bool isRagdoll)
        {
            if (isRagdoll || !visEq) return;
            ItemDrop.ItemData right = __instance.m_rightItem;
            if (Impersonation.IsDisguised(right))
            {
                visEq.SetRightItem(Impersonation.CurrentTool(right).Prefab.GetStableHashCode(), right.m_quality);
            }
            ItemDrop.ItemData left = __instance.m_leftItem;
            if (Impersonation.IsDisguised(left))
            {
                visEq.SetLeftItem(Impersonation.CurrentTool(left).Prefab.GetStableHashCode(), 0, left.m_quality);
            }
        }
    }

    // New build pieces are only discovered for the build menus of tools in your inventory (this is
    // the only caller), so a hammer counts as carrying every build tool it can become
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetAllPieceTables))]
    internal static class GetAllPieceTablesPatch
    {
        private static void Postfix(Inventory __instance, List<PieceTable> tables)
        {
            Player player = Player.m_localPlayer;
            if (!player || __instance != player.GetInventory() || !__instance.GetAllItems().Any(Impersonation.IsHammer)) return;
            foreach (ToolInfo tool in ToolCatalog.All)
            {
                PieceTable table = tool.Shared.m_buildPieces;
                if (table && !tables.Contains(table) && ToolCatalog.IsAvailable(player, tool)) tables.Add(table);
            }
        }
    }

    // Recipes are matched by name, so at a crafting station the hammer must be a hammer again
    [HarmonyPatch(typeof(Player), nameof(Player.SetCraftingStation))]
    internal static class SetCraftingStationPatch
    {
        private static void Postfix(Player __instance, CraftingStation station)
        {
            if (station && __instance == Player.m_localPlayer) ToolSwitcher.RevertToHammer(__instance);
        }
    }

    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
        typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
    internal static class TooltipPatch
    {
        private static void Postfix(ItemDrop.ItemData item, ref string __result)
        {
            if (!Impersonation.IsDisguised(item)) return;
            __result += "\n\n<color=orange>Your hammer, working as this tool.</color> " +
                        Localization.instance.Localize("Press <color=yellow>$KEY_OpenRadial</color> to switch tools.");
        }
    }

    // While the hammer is equipped, the radial key opens the tool menu instead of vanilla's
    [HarmonyPatch(typeof(Player), nameof(Player.HandleRadialInput))]
    internal static class HandleRadialInputPatch
    {
        private static bool Prefix(Player __instance) => !ToolMenu.TryOpen(__instance);
    }

    // Vanilla doesn't open the radial menu in build mode (except for repairing)
    [HarmonyPatch(typeof(RadialBase), nameof(RadialBase.InRadialBlockingPlaceMode), MethodType.Getter)]
    internal static class RadialBlockingPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!ToolMenu.Opening) return true;
            __result = false;
            return false;
        }
    }

    // Our menus use right-click for "more" and "back", so vanilla's drop hints don't apply
    [HarmonyPatch(typeof(KeyHintsRadial), nameof(KeyHintsRadial.UpdateRadialHints))]
    internal static class RadialHintsPatch
    {
        private static void Postfix(KeyHintsRadial __instance, RadialBase radial)
        {
            if (!ToolMenu.IsOurs(radial.CurrentConfig)) return;
            __instance.m_gamepadDrop.gameObject.SetActive(false);
            __instance.m_gamepadDropMulti.gameObject.SetActive(false);
            __instance.m_kbDrop.SetActive(false);
            __instance.m_kbDropMulti.SetActive(false);
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
    internal static class NewConnectionPatch
    {
        private static void Postfix(ZNetPeer peer) => ConfigSync.OnNewConnection(peer);
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo))]
    internal static class PeerInfoPatch
    {
        private static void Postfix(ZNet __instance, ZRpc rpc) => ConfigSync.OnPeerInfo(__instance.GetPeer(rpc));
    }

    [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
    internal static class InitTerminalPatch
    {
        private static void Postfix() => Commands.Register();
    }
}
