using BepInEx.Configuration;
using UnityEngine;

namespace LootFilter
{
    // What happens to the sparkles (particles and lights) of filtered drops
    public enum SparkleMode
    {
        Hidden,
        Recolored,
        Unchanged,
    }

    internal static class Settings
    {
        public static ConfigEntry<string> FilteredItems;
        public static ConfigEntry<ItemGroups> FilteredGroups;

        public static ConfigEntry<bool> BlockAutoPickup;
        public static ConfigEntry<bool> ShowInTooltip;

        public static ConfigEntry<SparkleMode> FilteredSparkles;
        public static ConfigEntry<Color> SparkleColor;

        public static ConfigEntry<KeyboardShortcut> ToggleItem;
        public static ConfigEntry<KeyboardShortcut> ToggleGroup;
        public static ConfigEntry<KeyboardShortcut> Reveal;
        public static ConfigEntry<KeyboardShortcut> TogglePause;

        public static void Bind(ConfigFile config)
        {
            FilteredItems = config.Bind("Rules", "FilteredItems", "",
                "Comma-separated item names to filter (internal prefab names, e.g. Resin, Stone, RoundLog). " +
                "Easiest to edit with the ToggleItem key while hovering an item.");
            FilteredGroups = config.Bind("Rules", "FilteredGroups", ItemGroups.None,
                "Item groups to filter.");

            BlockAutoPickup = config.Bind("Behaviour", "BlockAutoPickup", true,
                "Don't auto-pickup filtered items. They can still be picked up manually.");
            ShowInTooltip = config.Bind("Behaviour", "ShowInTooltip", true,
                "Add a line to item tooltips saying whether (and why) the item is filtered.");

            FilteredSparkles = config.Bind("Effects", "FilteredSparkles", SparkleMode.Hidden,
                "What to do with the sparkles and glow of filtered drops: hide them, recolor them with SparkleColor, or leave them alone.");
            SparkleColor = config.Bind("Effects", "SparkleColor", new Color(0.55f, 0.55f, 0.55f, 0.35f),
                "Color of filtered drops' sparkles when FilteredSparkles is Recolored. The alpha (A) value sets how visible they are.");

            ToggleItem = config.Bind("Keys", "ToggleItem", new KeyboardShortcut(KeyCode.Delete),
                "Filter/unfilter the hovered item (inventory, chest or on the ground).");
            ToggleGroup = config.Bind("Keys", "ToggleGroup", new KeyboardShortcut(KeyCode.Delete, KeyCode.LeftShift),
                "Filter/unfilter the hovered item's group (Weapons, Trophies, ...).");
            Reveal = config.Bind("Keys", "Reveal", new KeyboardShortcut(KeyCode.LeftAlt),
                "Hold to show the original effects of filtered items.");
            TogglePause = config.Bind("Keys", "TogglePause", new KeyboardShortcut(KeyCode.Insert),
                "Pause/resume the filter: while paused, filtered items are auto-picked up and keep their effects. " +
                "The filter is always active again after restarting the game.");
        }
    }
}
