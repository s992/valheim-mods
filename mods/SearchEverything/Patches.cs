using System;
using HarmonyLib;
using ValheimMods.Shared;

namespace SearchEverything
{
    [HarmonyPatch(typeof(Container), nameof(Container.Awake))]
    internal static class ContainerAwakePatch
    {
        private static void Postfix(Container __instance) => NearbyChests.Register(__instance);
    }

    // Our boxes and the button live on the inventory's panels; they're made the first time it opens
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Update))]
    internal static class InventoryGuiUpdatePostfix
    {
        private static void Postfix(InventoryGui __instance)
        {
            if (!InventoryGui.IsVisible() || !Player.m_localPlayer) return;
            RecipeSearch.Update(__instance);
            ChestSearch.Update(__instance);
            InventoryButton.Update(__instance);
        }

        private static Exception Finalizer(Exception __exception) => PatchGuard.Contain(__exception, "the search boxes");
    }
}
