using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace SearchEverything
{
    internal static class Hotkeys
    {
        private static readonly KeyCode[] ModifierKeys =
            { KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt };

        // Exact match through ZInput (the game uses the new input system): extra modifiers must not be held,
        // so K and Shift+K can do different things
        public static bool Pressed(ConfigEntry<KeyboardShortcut> entry)
        {
            KeyboardShortcut shortcut = entry.Value;
            if (shortcut.MainKey == KeyCode.None || !ZInput.GetKeyDown(shortcut.MainKey, false)) return false;
            var modifiers = shortcut.Modifiers.ToList();
            if (modifiers.Any(key => !ZInput.GetKey(key, false))) return false;
            return !ModifierKeys.Any(key => !modifiers.Contains(key) && ZInput.GetKey(key, false));
        }

        // Our own boxes are checked directly as well: TextInput.IsVisible covers them only through a patch
        public static bool IsTyping() =>
            InputGuard.Active || global::Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible() || Minimap.InTextInput() ||
            (Chat.instance && Chat.instance.HasFocus()) || (Hud.instance && Hud.instance.m_buildUi.SearchFieldFocused);
    }
}
