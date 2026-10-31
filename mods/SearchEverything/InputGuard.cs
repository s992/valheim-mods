using System.Collections.Generic;
using GUIFramework;
using HarmonyLib;
using UnityEngine;

namespace SearchEverything
{
    /// <summary>
    /// Keeps the game from acting on keys typed into our search boxes. While one is focused (or the nearby search is
    /// open), TextInput.IsVisible() reports true: the check the game already uses to pause player input, the camera,
    /// the pause menu, chat and the map while its own text dialogs are open. Other mods' hotkeys usually check it too.
    /// </summary>
    internal static class InputGuard
    {
        private static readonly List<GuiInputField> s_fields = new List<GuiInputField>();
        private static int s_lastActiveFrame = -10;

        public static bool WindowOpen;

        public static void Register(GuiInputField field) => s_fields.Add(field);

        /// <summary>Counts this frame as typing (e.g. the frame Escape closed the window).</summary>
        public static void MarkActive() => s_lastActiveFrame = Time.frameCount;

        /// <summary>
        /// Also true for one frame after focus is lost: Escape both unfocuses a box (or closes the window) and is read
        /// by the inventory and the pause menu, in whichever order Unity runs them that frame.
        /// </summary>
        public static bool Active
        {
            get
            {
                if (IsActiveNow()) s_lastActiveFrame = Time.frameCount;
                return Time.frameCount - s_lastActiveFrame <= 1;
            }
        }

        private static bool IsActiveNow()
        {
            if (WindowOpen) return true;
            for (int i = s_fields.Count - 1; i >= 0; i--)
            {
                GuiInputField field = s_fields[i];
                if (!field)
                {
                    s_fields.RemoveAt(i);
                    continue;
                }
                if (field.isFocused) return true;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
    internal static class TextInputIsVisiblePatch
    {
        private static void Postfix(ref bool __result) => __result = __result || InputGuard.Active;
    }

    // TextInput.IsVisible is small enough that Mono may inline it into callers, which would bypass the patch above.
    // The places that matter most are guarded directly too: player actions, movement and look, the mouse cursor, and
    // Escape opening the pause menu.
    [HarmonyPatch(typeof(Player), nameof(Player.TakeInput))]
    internal static class PlayerTakeInputPatch
    {
        private static void Postfix(ref bool __result) => __result = __result && !InputGuard.Active;
    }

    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.TakeInput))]
    internal static class PlayerControllerTakeInputPatch
    {
        private static void Postfix(ref bool __result) => __result = __result && !InputGuard.Active;
    }

    [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture))]
    internal static class UpdateMouseCapturePatch
    {
        private static void Postfix()
        {
            if (!InputGuard.WindowOpen) return;
            ZCursor.LockState = CursorLockMode.None;
            ZCursor.Show();
        }
    }

    [HarmonyPatch(typeof(Menu), nameof(Menu.Update))]
    internal static class MenuUpdatePatch
    {
        // While hidden, Menu.Update only checks whether to open (and re-localizes now and then)
        private static bool Prefix() => Menu.IsVisible() || !InputGuard.Active;
    }

    // The inventory closes on its Inventory/Use buttons (Tab, E) and Escape, and opens on Tab, none of which checks
    // TextInput. Swallow the buttons; Escape is a raw key, so skip Hide() for that one frame instead.
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Update))]
    internal static class InventoryGuiUpdatePatch
    {
        internal static bool BlockHide;

        private static void Prefix()
        {
            Player player = Player.m_localPlayer;
            if (!InputGuard.Active || !player || player.IsDead()) return;
            ZInput.ResetButtonStatus("Inventory");
            ZInput.ResetButtonStatus("Use");
            ZInput.ResetButtonStatus("JoyButtonB");
            ZInput.ResetButtonStatus("JoyButtonY");
            BlockHide = ZInput.GetKeyDown(KeyCode.Escape, false);
        }

        private static void Finalizer() => BlockHide = false;
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    internal static class InventoryGuiHidePatch
    {
        private static bool Prefix() => !InventoryGuiUpdatePatch.BlockHide;

        // Searches start empty next time
        private static void Postfix()
        {
            if (InventoryGuiUpdatePatch.BlockHide) return;
            RecipeSearch.Clear();
            ChestSearch.Clear();
        }
    }
}
