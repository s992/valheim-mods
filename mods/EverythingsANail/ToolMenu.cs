using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Valheim.UI;
using Object = UnityEngine.Object;

namespace EverythingsANail
{
    // The radial menu while the hammer is equipped: the best (or current) tool of each group, a
    // right-click into a group for its other tools, and the vanilla menu as the last entry
    internal static class ToolMenu
    {
        private static ToolsOpenConfig s_openConfig;

        // True while our menu is being opened, so vanilla's "no radial in build mode" check can be skipped
        public static bool Opening { get; private set; }

        public static bool IsOurs(IRadialConfig config) => config is TopConfig || config is GroupConfig;

        // Player.HandleRadialInput prefix: true when the press was handled here
        public static bool TryOpen(Player player)
        {
            if (Hud.InRadial() || ToolSwitcher.EquippedHammer(player) == null) return false;
            bool keyboard = ZInput.GetButtonDown("OpenRadial");
            // Vanilla's gamepad rule: hold the radial button, not in build mode (it's bound to build
            // actions in some layouts)
            bool gamepad = !player.InPlaceMode() && !ZInput.GetButton("JoyAltKeys") && ZInput.GetButtonPressedTimer("JoyRadial") > 0.33f;
            if (!keyboard && !gamepad) return false;

            RadialBase radial = Hud.instance.m_radialMenu;
            // CanOpen is cleared on close until the button is pressed again (vanilla sets it on the press)
            if (!radial.CanOpen)
            {
                if (!keyboard) return false;
                radial.CanOpen = true;
            }
            if (Hud.IsPieceSelectionVisible()) return true;

            if (!s_openConfig) s_openConfig = ScriptableObject.CreateInstance<ToolsOpenConfig>();
            Opening = true;
            try
            {
                radial.Open(s_openConfig);
            }
            finally
            {
                Opening = false;
            }
            return true;
        }

        // Right-click goes into a group at the top level and back out inside one. Vanilla binds
        // right-click to "back" and Shift+right-click to dropping, so wrap its handlers and change
        // only what our menus need. LoadSettings reinstalls vanilla's handlers on every normal open.
        public static void OverrideInput(RadialBase radial)
        {
            Func<bool> back = radial.GetBack;
            Func<bool> openSubMenu = radial.GetOpenThrowMenu;
            Func<bool> secondary = radial.GetThrow;
            radial.GetBack = () => radial.CurrentConfig is TopConfig
                ? ZInput.GetButtonUp("JoyRadialBack")
                : back != null && back();
            radial.GetOpenThrowMenu = () => radial.CurrentConfig is TopConfig
                ? ZInput.GetButtonUp("RadialSecondaryInteract") || ZInput.GetButtonUp("JoyRadialSecondaryInteract")
                : !(radial.CurrentConfig is GroupConfig) && openSubMenu != null && openSubMenu();
            radial.GetThrow = () => !IsOurs(radial.CurrentConfig) && secondary != null && secondary();
        }

        public static RadialMenuElement CreateToolElement(ToolInfo tool, ToolInfo current, int more)
        {
            GroupElement element = Object.Instantiate(RadialData.SO.GroupElement);
            element.Name = tool.DisplayName;
            var notes = new List<string>();
            if (tool == current) notes.Add("In hand");
            if (more > 0) notes.Add($"Right-click: {more} more");
            element.SubTitle = string.Join("  ·  ", notes);
            element.Icon.sprite = tool.Icon;
            element.Icon.gameObject.SetActive(tool.Icon);
            element.Interact = () =>
            {
                ToolSwitcher.Switch(Player.m_localPlayer, tool);
                return true;
            };
            element.CloseOnInteract = () => true;
            // The same highlight vanilla uses for equipped items
            element.Activated = tool == current ? 1f : 0f;
            return element;
        }

        public static ToolInfo CurrentTool() =>
            Impersonation.CurrentTool(ToolSwitcher.EquippedHammer(Player.m_localPlayer));
    }

    // RadialBase.Open only loads the input settings and resets its state for OpenRadialConfig, so
    // derive from it. Re-implementing the interface makes this InitRadialConfig the one that runs.
    internal sealed class ToolsOpenConfig : OpenRadialConfig, IRadialConfig
    {
        public new void InitRadialConfig(RadialBase radial)
        {
            radial.OnInteractionDelay = delay => PlayerController.SetTakeInputDelay(delay);
            radial.ShouldAnimateIn = true;
            ToolMenu.OverrideInput(radial);
            radial.Open(new TopConfig());
        }
    }

    internal sealed class TopConfig : IRadialConfig
    {
        public string LocalizedName => "Tools";

        public Sprite Sprite => null;

        public void InitRadialConfig(RadialBase radial)
        {
            ToolInfo current = ToolMenu.CurrentTool();
            var elements = new List<RadialMenuElement>();
            foreach (List<ToolInfo> group in ToolCatalog.AvailableGroups(Player.m_localPlayer))
            {
                ToolInfo shown = group.Contains(current) ? current : group[0];
                RadialMenuElement element = ToolMenu.CreateToolElement(shown, current, group.Count - 1);
                if (group.Count > 1)
                {
                    string key = shown.Group;
                    element.TryOpenSubRadial = (menu, index) =>
                    {
                        menu.BackIndex = index;
                        menu.QueuedOpen(new GroupConfig(key), this);
                        return true;
                    };
                }
                elements.Add(element);
            }

            GroupElement main = Object.Instantiate(RadialData.SO.GroupElement);
            main.Init(RadialData.SO.MainGroupConfig, this, radial);
            main.Name = "Main menu";
            elements.Add(main);

            radial.ConstructRadial(elements);
        }
    }

    // All available tools of one group (all pickaxes, say), best first
    internal sealed class GroupConfig : IRadialConfig
    {
        private readonly string m_group;

        public GroupConfig(string group) => m_group = group;

        public string LocalizedName => m_group;

        public Sprite Sprite => null;

        public void InitRadialConfig(RadialBase radial)
        {
            ToolInfo current = ToolMenu.CurrentTool();
            List<ToolInfo> tools = ToolCatalog.AvailableGroups(Player.m_localPlayer)
                .FirstOrDefault(group => group[0].Group == m_group) ?? new List<ToolInfo>();
            radial.ConstructRadial(tools.Select(tool => ToolMenu.CreateToolElement(tool, current, 0)).ToList());
        }
    }
}
