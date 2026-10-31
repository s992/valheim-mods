using System;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CraftFromStorage
{
    internal static class Hotkeys
    {
        private static readonly KeyCode[] ModifierKeys =
            { KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt };

        private static readonly MethodInfo s_keyCodeToPath = AccessTools.Method(typeof(ZInput), "KeyCodeToPath");

        // Exact match through ZInput (the game uses the new input system): extra modifiers must not be held
        public static bool Pressed(ConfigEntry<KeyboardShortcut> entry)
        {
            KeyboardShortcut shortcut = entry.Value;
            if (shortcut.MainKey == KeyCode.None || !ZInput.GetKeyDown(shortcut.MainKey, false)) return false;
            var modifiers = shortcut.Modifiers.ToList();
            if (modifiers.Any(key => !ZInput.GetKey(key, false))) return false;
            return !ModifierKeys.Any(key => !modifiers.Contains(key) && ZInput.GetKey(key, false));
        }

        /// <summary>
        /// Whether this key is (one of) the game's current Use bindings, including after the player remapped Use.
        /// Compares input-system control paths, since display names can be localized.
        /// </summary>
        public static bool IsBoundToUse(KeyCode key)
        {
            if (key == KeyCode.None || ZInput.instance == null || s_keyCodeToPath == null) return false;
            ZInput.ButtonDef use = ZInput.instance.GetButtonDef("Use");
            if (use?.ButtonAction == null) return false;
            string path = s_keyCodeToPath.Invoke(null, new object[] { key, false }) as string;
            if (string.IsNullOrEmpty(path)) return false;
            InputControl control = InputSystem.FindControl(path);
            foreach (InputBinding binding in use.ButtonAction.bindings)
            {
                if (string.IsNullOrEmpty(binding.effectivePath)) continue;
                if (string.Equals(binding.effectivePath, path, StringComparison.OrdinalIgnoreCase)) return true;
                if (control != null && InputControlPath.Matches(binding.effectivePath, control)) return true;
            }
            return false;
        }

        public static bool IsTyping() =>
            global::Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible() || Minimap.InTextInput() ||
            (Chat.instance && Chat.instance.HasFocus());
    }
}
