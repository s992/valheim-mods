using BepInEx.Configuration;
using UnityEngine;

namespace SearchEverything
{
    internal static class Settings
    {
        public static ConfigEntry<bool> RecipeSearch;

        public static ConfigEntry<bool> ChestSearch;
        public static ConfigEntry<float> DimOpacity;

        public static ConfigEntry<bool> Finder;
        public static ConfigEntry<KeyboardShortcut> FinderKey;
        public static ConfigEntry<bool> InventoryButton;
        public static ConfigEntry<int> Range;
        public static ConfigEntry<bool> CartsAndShips;

        public static ConfigEntry<int> MarkerSeconds;
        public static ConfigEntry<KeyboardShortcut> DismissKey;

        public static ConfigEntry<bool> LogLayout;

        /// <summary>Bumped whenever a setting changes, so cached results can tell they're stale.</summary>
        public static int Version { get; private set; }

        public static void Bind(ConfigFile config)
        {
            RecipeSearch = config.Bind("1. Recipes", "Recipe search", true,
                "A search box next to the Craft and Upgrade tabs filters the recipe list. Matches the item's name, its " +
                "ingredients and its type (bow, helmet, food...).");

            ChestSearch = config.Bind("2. Chests", "Chest search", true,
                "A search box at the top of an open chest. Items that don't match fade out, in the chest and in your inventory.");
            DimOpacity = config.Bind("2. Chests", "Faded opacity", 0.25f,
                new ConfigDescription("How visible items that don't match the chest search stay (0 = hidden, 1 = not faded).",
                    new AcceptableValueRange<float>(0f, 1f)));

            Finder = config.Bind("3. Nearby chests", "Nearby search", true,
                "A search window for everything in nearby chests. Picking a result marks the chests that hold it.");
            FinderKey = config.Bind("3. Nearby chests", "Open key", new KeyboardShortcut(KeyCode.K, KeyCode.LeftControl),
                "Opens the nearby search, in the world or in the inventory. Escape closes it. The game doesn't act on " +
                "this key while it opens the search (e.g. debug mode's K, which kills nearby enemies).");
            InventoryButton = config.Bind("3. Nearby chests", "Inventory button", true,
                "Shows a button above your inventory that opens the nearby search.");
            Range = config.Bind("3. Nearby chests", "Range", 64,
                new ConfigDescription(
                    "Chests within this many metres of the camera are searched. Chests further than the game keeps loaded " +
                    "(128 m from you on the default simulation distance) can't be seen; the window's status line says when that applies.",
                    new AcceptableValueRange<int>(5, 250)));
            CartsAndShips = config.Bind("3. Nearby chests", "Carts and ships", true,
                "Carts and ships count as chests.");

            MarkerSeconds = config.Bind("4. Markers", "Marker duration", 60,
                new ConfigDescription(
                    "Seconds a chest stays marked. A marker also goes away when you open its chest. 0: until opened or dismissed.",
                    new AcceptableValueRange<int>(0, 600)));
            DismissKey = config.Bind("4. Markers", "Dismiss key", new KeyboardShortcut(KeyCode.K, KeyCode.LeftControl, KeyCode.LeftShift),
                "Removes every marker.");

            LogLayout = config.Bind("5. Diagnostics", "Log UI layout", false,
                "Writes where the search boxes were placed, and the panels around them, to the BepInEx log. Useful when a " +
                "box overlaps something.");

            config.SettingChanged += (_, __) => Version++;
        }
    }
}
