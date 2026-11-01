using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CraftFromStorage
{
    internal static class UseKey
    {
        private static readonly MethodInfo s_keyCodeToPath = AccessTools.Method(typeof(ZInput), "KeyCodeToPath");

        /// <summary>
        /// Whether this key is (one of) the game's current Use bindings, including after the player remapped Use.
        /// Compares input-system control paths, since display names can be localized.
        /// </summary>
        public static bool IsBound(KeyCode key)
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

    }
}
