using HarmonyLib;

namespace StabilityView
{
    // Snapshot just before the local player builds or removes something, so the
    // overlay shows exactly what that change did
    [HarmonyPatch]
    internal static class AutoSnapshotPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
        private static void BeforePlace(Player __instance) => Capture(__instance);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.RemovePiece))]
        private static void BeforeRemove(Player __instance) => Capture(__instance);

        private static void Capture(Player player)
        {
            if (player == Player.m_localPlayer && Settings.AutoSnapshot.Value && Overlay.Instance && Overlay.Instance.IsShowing)
                Snapshot.Capture();
        }
    }
}
