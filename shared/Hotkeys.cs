using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace ValheimMods.Shared
{
    // Keys are read through ZInput: the game uses Unity's new input system, which BepInEx's KeyboardShortcut.IsDown()
    // doesn't see
    internal static class Hotkeys
    {
        private static readonly KeyCode[] s_modifierKeys =
        {
            KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl,
            KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt,
        };

        // Exact match: modifiers that aren't part of the shortcut must not be held, so Delete, Shift+Delete and
        // Ctrl+Delete can do different things
        public static bool Pressed(ConfigEntry<KeyboardShortcut> entry)
        {
            KeyboardShortcut shortcut = entry.Value;
            if (shortcut.MainKey == KeyCode.None || !ZInput.GetKeyDown(shortcut.MainKey, false)) return false;
            var modifiers = shortcut.Modifiers.ToList();
            if (modifiers.Any(key => !ZInput.GetKey(key, false))) return false;
            return !s_modifierKeys.Any(key => !modifiers.Contains(key) && ZInput.GetKey(key, false));
        }

        public static bool Held(ConfigEntry<KeyboardShortcut> entry)
        {
            KeyboardShortcut shortcut = entry.Value;
            return shortcut.MainKey != KeyCode.None && ZInput.GetKey(shortcut.MainKey, false) &&
                   shortcut.Modifiers.All(key => ZInput.GetKey(key, false));
        }

        // Hotkeys stay quiet while the player types or a menu is open. The inventory doesn't count: some mods' keys
        // act on hovered items there.
        public static bool IsTyping() =>
            global::Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible() || Minimap.InTextInput() ||
            (Chat.instance && Chat.instance.HasFocus());
    }
}
