using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace Storage
{
    internal static class Hotkeys
    {
        private static readonly KeyCode[] ModifierKeys =
            { KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt };

        // Exact match through ZInput (the game uses the new input system): extra modifiers must not be held
        public static bool Pressed(ConfigEntry<KeyboardShortcut> entry)
        {
            KeyboardShortcut shortcut = entry.Value;
            if (shortcut.MainKey == KeyCode.None || !ZInput.GetKeyDown(shortcut.MainKey, false)) return false;
            var modifiers = shortcut.Modifiers.ToList();
            if (modifiers.Any(key => !ZInput.GetKey(key, false))) return false;
            return !ModifierKeys.Any(key => !modifiers.Contains(key) && ZInput.GetKey(key, false));
        }

        public static bool IsTyping() =>
            global::Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible() || Minimap.InTextInput() ||
            (Chat.instance && Chat.instance.HasFocus());
    }
}
