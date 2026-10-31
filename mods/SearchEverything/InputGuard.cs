using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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
    // Also false on the frame one of our hotkeys is pressed: debug mode's keys (K kills nearby enemies, also Z, B, L)
    // are read inside a TakeInput check with raw GetKeyDown, so Ctrl+K would still count as K there
    [HarmonyPatch(typeof(Player), nameof(Player.TakeInput))]
    internal static class PlayerTakeInputPatch
    {
        private static void Postfix(ref bool __result) =>
            __result = __result && !InputGuard.Active && !Hotkeys.OurKeyPressed();
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

    // The camera zooms on the mouse wheel unless the inventory, map, chat... is open; it doesn't check TextInput,
    // so the wheel would zoom while scrolling the nearby search's results
    [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateCamera))]
    internal static class UpdateCameraZoomPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo wheel = AccessTools.Method(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel));
            MethodInfo replacement = AccessTools.Method(typeof(UpdateCameraZoomPatch), nameof(ScrollWheel));
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(wheel))
                {
                    replaced++;
                    yield return new CodeInstruction(OpCodes.Call, replacement) { labels = instruction.labels, blocks = instruction.blocks };
                }
                else yield return instruction;
            }
            if (replaced == 0) Plugin.Log.LogWarning("Camera zoom wasn't found; the mouse wheel may zoom while the nearby search is open");
        }

        private static float ScrollWheel() => InputGuard.WindowOpen ? 0f : ZInput.GetMouseScrollWheel();
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
