using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using ValheimMods.Shared;

namespace SearchEverything
{
    /// <summary>This mod's keys on top of shared/Hotkeys: configured shortcuts go through it, fixed keys through here.</summary>
    internal static class SearchKeys
    {
        /// <summary>One of our hotkeys went down this frame and will be acted on, so the game should ignore it.</summary>
        public static bool OurKeyPressed() =>
            (Settings.Finder.Value && Hotkeys.Pressed(Settings.FinderKey)) ||
            (Markers.Count > 0 && Hotkeys.Pressed(Settings.DismissKey));

        // Our own boxes are checked directly as well: TextInput.IsVisible covers them only through a patch
        public static bool IsTyping() =>
            InputGuard.Active || Hotkeys.IsTyping() || (Hud.instance && Hud.instance.m_buildUi.SearchFieldFocused);

        /// <summary>
        /// A fixed key went down this frame: the search window's arrows, Enter and Escape, and Escape the way the
        /// inventory reads it. They belong to a text dialog, like the game's own, not to shortcuts a player configures.
        /// </summary>
        [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
            Justification = "Fixed dialog keys, not configured shortcuts; shared/Hotkeys only covers ConfigEntry shortcuts")]
        public static bool Down(KeyCode key) => ZInput.GetKeyDown(key, false);
    }
}
