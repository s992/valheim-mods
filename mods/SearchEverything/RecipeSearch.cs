using System;
using System.Collections.Generic;
using GUIFramework;
using HarmonyLib;
using UnityEngine;

namespace SearchEverything
{
    /// <summary>A search box on the crafting panel's tab row that filters the recipe list.</summary>
    internal static class RecipeSearch
    {
        private static GuiInputField s_field;
        private static string[] s_words = Array.Empty<string>();

        public static string[] Words => s_words;

        public static void Update(InventoryGui gui)
        {
            if (!Settings.RecipeSearch.Value)
            {
                if (s_field && s_field.gameObject.activeSelf)
                {
                    bool hadQuery = s_words.Length > 0;
                    Clear();
                    s_field.gameObject.SetActive(false);
                    if (hadQuery) gui.UpdateCraftingPanel();
                }
                return;
            }

            if (!s_field)
            {
                s_field = SearchField.Create(gui.m_crafting, "SearchEverything_RecipeSearch", "Search recipes");
                if (!s_field) return;
                // Right of the Craft/Upgrade tabs, below the station level, above the line under the tabs
                SearchField.Place((RectTransform)s_field.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-10f, -69f), new Vector2(230f, 31f));
                s_field.onValueChanged.AddListener(text =>
                {
                    s_words = Matcher.Parse(text);
                    if (InventoryGui.instance && InventoryGui.IsVisible()) InventoryGui.instance.UpdateCraftingPanel();
                });
                SearchField.LogLayout("Recipe search box", (RectTransform)s_field.transform);
            }
            if (!s_field.gameObject.activeSelf) s_field.gameObject.SetActive(true);
        }

        public static void Clear()
        {
            s_words = Array.Empty<string>();
            if (s_field && s_field.text.Length > 0) s_field.SetTextWithoutNotify("");
        }
    }

    // The game builds the list from this argument (a reused list of the station's known recipes): drop the non-matches
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipeList))]
    internal static class UpdateRecipeListPatch
    {
        private static void Prefix(List<Recipe> recipes)
        {
            string[] words = RecipeSearch.Words;
            if (words.Length > 0 && recipes != null) recipes.RemoveAll(recipe => !Matcher.Matches(recipe, words));
        }
    }
}
