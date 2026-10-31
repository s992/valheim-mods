using System;
using System.Collections.Generic;
using GUIFramework;
using HarmonyLib;
using UnityEngine;

namespace SearchEverything
{
    /// <summary>
    /// A search box in place of an open chest's name (the name becomes its placeholder). Items that don't match fade
    /// out in the chest and in the player's inventory; every item stays in its slot.
    /// </summary>
    internal static class ChestSearch
    {
        private static GuiInputField s_field;
        private static string[] s_words = Array.Empty<string>();
        private static Container s_container;
        private static bool s_nameHidden;
        private static readonly HashSet<InventoryGrid> s_faded = new HashSet<InventoryGrid>();

        public static void Update(InventoryGui gui)
        {
            bool show = Settings.ChestSearch.Value && gui.IsContainerOpen();
            if (!show)
            {
                if (s_field && s_field.gameObject.activeSelf)
                {
                    Clear();
                    s_field.gameObject.SetActive(false);
                }
                ShowName(gui, true);
                s_container = null;
                return;
            }

            if (!s_field)
            {
                s_field = SearchField.Create(gui.m_container, "SearchEverything_ChestSearch", "");
                if (!s_field) return;
                // Where the chest's name is, between Take All and Stack All
                SearchField.Place((RectTransform)s_field.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -19f), new Vector2(270f, 31f));
                s_field.onValueChanged.AddListener(text => s_words = Matcher.Parse(text));
                SearchField.LogLayout("Chest search box", (RectTransform)s_field.transform);
            }
            if (!s_field.gameObject.activeSelf) s_field.gameObject.SetActive(true);
            ShowName(gui, false);

            if (gui.m_currentContainer != s_container)
            {
                s_container = gui.m_currentContainer;
                Clear();
                string name = s_container ? Matcher.Localize(s_container.GetInventory().GetName()) : "";
                SearchField.SetPlaceholder(s_field, $"Search {name}".Trim());
            }
        }

        public static void Clear()
        {
            s_words = Array.Empty<string>();
            if (s_field && s_field.text.Length > 0) s_field.SetTextWithoutNotify("");
        }

        private static void ShowName(InventoryGui gui, bool show)
        {
            if (!gui.m_containerName || s_nameHidden == !show) return;
            gui.m_containerName.gameObject.SetActive(show);
            s_nameHidden = !show;
        }

        /// <summary>Runs after the game has drawn a grid (every frame while the inventory is open).</summary>
        public static void Apply(InventoryGrid grid)
        {
            bool searching = s_words.Length > 0;
            if (!searching && !s_faded.Contains(grid)) return;

            float faded = Settings.DimOpacity.Value;
            foreach (InventoryElement element in grid.m_elements) SetAlpha(element, searching ? faded : 1f);
            if (searching)
            {
                foreach (ItemDrop.ItemData item in grid.m_inventory.GetAllItems())
                {
                    if (!Matcher.Matches(item, s_words)) continue;
                    InventoryElement element = grid.GetElement(item.m_gridPos.x, item.m_gridPos.y, grid.m_inventory.GetWidth());
                    if (element) SetAlpha(element, 1f);
                }
                s_faded.Add(grid);
            }
            else s_faded.Remove(grid);
        }

        private static void SetAlpha(InventoryElement element, float alpha)
        {
            var group = element.GetComponent<CanvasGroup>();
            if (!group)
            {
                if (alpha >= 1f) return;
                group = element.gameObject.AddComponent<CanvasGroup>();
            }
            group.alpha = alpha;
        }
    }

    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
    internal static class InventoryGridUpdateGuiPatch
    {
        private static void Postfix(InventoryGrid __instance)
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui && (__instance == gui.m_playerGrid || __instance == gui.m_containerGrid)) ChestSearch.Apply(__instance);
        }
    }
}
