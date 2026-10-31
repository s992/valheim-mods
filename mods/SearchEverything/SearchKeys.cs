using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SearchEverything
{
    internal static class Hotkeys
    {
        private static readonly KeyCode[] ModifierKeys =
            { KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt };

        private delegate Key KeyCodeToKeyFn(KeyCode keyCode, bool logWarning);
        private static KeyCodeToKeyFn s_keyCodeToKey;
        private static bool s_lookedUp;
        private static readonly HashSet<string> s_reportedDevices = new HashSet<string>();

        // Exact match: extra modifiers must not be held, so K and Shift+K can do different things
        public static bool Pressed(ConfigEntry<KeyboardShortcut> entry)
        {
            KeyboardShortcut shortcut = entry.Value;
            if (shortcut.MainKey == KeyCode.None || !KeyDown(shortcut.MainKey)) return false;
            var modifiers = shortcut.Modifiers.ToList();
            if (modifiers.Any(key => !KeyHeld(key))) return false;
            return !ModifierKeys.Any(key => !modifiers.Contains(key) && KeyHeld(key));
        }

        public static bool KeyDown(KeyCode key) => KeyState(key, true);

        public static bool KeyHeld(KeyCode key) => KeyState(key, false);

        /// <summary>
        /// The game's raw key reads (ZInput.GetKeyDown/GetKey) only look at Keyboard.current, the keyboard device that
        /// last sent input. With a second device that reports as a keyboard (some mice, macro pads, headsets, virtual
        /// keyboards), raw reads can miss the real keyboard while key bindings (WASD...) still work. Ours check every
        /// keyboard, and say once in the log when a key came from a keyboard that isn't the current one.
        /// </summary>
        private static bool KeyState(KeyCode code, bool down)
        {
            Key key = ToKey(code);
            if (key == Key.None) return down ? ZInput.GetKeyDown(code, false) : ZInput.GetKey(code, false);
            foreach (InputDevice device in InputSystem.devices)
            {
                if (!(device is Keyboard keyboard)) continue;
                KeyControl control = keyboard[key];
                if (control == null || !(down ? control.wasPressedThisFrame : control.isPressed)) continue;
                if (down) ReportIfNotCurrent(keyboard, code);
                return true;
            }
            return false;
        }

        private static Key ToKey(KeyCode code)
        {
            if (code == KeyCode.None || code >= KeyCode.Mouse0) return Key.None;
            if (!s_lookedUp)
            {
                s_lookedUp = true;
                MethodInfo method = AccessTools.Method(typeof(ZInput), "KeyCodeToKey", new[] { typeof(KeyCode), typeof(bool) });
                if (method != null) s_keyCodeToKey = (KeyCodeToKeyFn)Delegate.CreateDelegate(typeof(KeyCodeToKeyFn), method);
                else Plugin.Log.LogWarning("ZInput.KeyCodeToKey wasn't found; hotkeys read the game's current keyboard only");
            }
            if (s_keyCodeToKey == null) return Key.None;
            try
            {
                return s_keyCodeToKey(code, false);
            }
            catch (Exception)
            {
                return Key.None;
            }
        }

        private static void ReportIfNotCurrent(Keyboard keyboard, KeyCode code)
        {
            Keyboard current = Keyboard.current;
            if (current == keyboard) return;
            string pair = $"{keyboard.name}|{(current != null ? current.name : "none")}";
            if (!s_reportedDevices.Add(pair)) return;
            string all = string.Join(", ", InputSystem.devices.OfType<Keyboard>().Select(k => $"'{k.displayName}' ({k.name})"));
            Plugin.Log.LogWarning(
                $"{code} came from keyboard '{keyboard.displayName}' ({keyboard.name}), but the game's current keyboard is " +
                $"{(current != null ? $"'{current.displayName}' ({current.name})" : "none")}. Keys the game and other mods read " +
                $"directly (debug keys, Configuration Manager's hotkey) only see the current one. Keyboards: {all}");
        }

        /// <summary>One of our hotkeys went down this frame and will be acted on, so the game should ignore it.</summary>
        public static bool OurKeyPressed() =>
            (Settings.Finder.Value && Pressed(Settings.FinderKey)) || (Markers.Count > 0 && Pressed(Settings.DismissKey));

        // Our own boxes are checked directly as well: TextInput.IsVisible covers them only through a patch
        public static bool IsTyping() =>
            InputGuard.Active || global::Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible() || Minimap.InTextInput() ||
            (Chat.instance && Chat.instance.HasFocus()) || (Hud.instance && Hud.instance.m_buildUi.SearchFieldFocused);
    }
}
