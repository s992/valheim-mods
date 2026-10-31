using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace LootFilter
{
    // Keys are read through ZInput: the game uses Unity's new input system, which BepInEx's
    // KeyboardShortcut.IsDown() doesn't see
    internal static class Input
    {
        private static readonly KeyCode[] ModifierKeys =
        {
            KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl,
            KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt,
        };

        // Exact match: modifiers that aren't part of the shortcut must not be held,
        // so Delete, Shift+Delete and Ctrl+Delete don't trigger each other
        public static bool Pressed(ConfigEntry<KeyboardShortcut> entry)
        {
            KeyboardShortcut shortcut = entry.Value;
            if (shortcut.MainKey == KeyCode.None || !ZInput.GetKeyDown(shortcut.MainKey, false))
                return false;
            var modifiers = shortcut.Modifiers.ToList();
            if (modifiers.Any(key => !ZInput.GetKey(key, false)))
                return false;
            return !ModifierKeys.Any(key => !modifiers.Contains(key) && ZInput.GetKey(key, false));
        }

        public static bool Held(ConfigEntry<KeyboardShortcut> entry)
        {
            KeyboardShortcut shortcut = entry.Value;
            return shortcut.MainKey != KeyCode.None
                && ZInput.GetKey(shortcut.MainKey, false)
                && shortcut.Modifiers.All(key => ZInput.GetKey(key, false));
        }

        // Unlike most hotkeys these work with the inventory open, since that's where items are marked
        public static bool IsTyping() =>
            Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible() || Minimap.InTextInput()
            || (Chat.instance && Chat.instance.HasFocus());
    }
}
