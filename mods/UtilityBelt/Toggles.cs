using System;
using BepInEx.Configuration;
using UnityEngine;
using ValheimMods.Shared;

namespace UtilityBelt
{
    // The personal on/off keys for the wishbone, wisplight and feather fall upgrades
    internal sealed class Toggles : MonoBehaviour
    {
        private FrameGuard m_guard;

        private void Awake() => m_guard = new FrameGuard(this, "the belt's hotkeys");

        private void Update()
        {
            try
            {
                if (!Player.m_localPlayer || Hotkeys.IsTyping()) return;
                Toggle(Settings.WishboneKey, Settings.Wishbone, Tier.Wishbone);
                Toggle(Settings.WisplightKey, Settings.Wisplight, Tier.Wisplight);
                Toggle(Settings.FeatherFallKey, Settings.FeatherFall, Tier.FeatherFall);
            }
            catch (Exception e)
            {
                m_guard.Report(e);
            }
        }

        private static void Toggle(ConfigEntry<KeyboardShortcut> key, ConfigEntry<bool> setting, Tier tier)
        {
            if (!Hotkeys.Pressed(key)) return;
            // The change reaches the player's effects through Settings.Changed
            setting.Value = !setting.Value;
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,
                $"Utility belt: {Tiers.DisplayName(tier).ToLowerInvariant()} {(setting.Value ? "on" : "off")}");
        }
    }
}
