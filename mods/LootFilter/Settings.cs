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
        public static ConfigEntry<Rarities> FilteredRarities;
        public static ConfigEntry<Rarities> OutdatedRarities;
        public static ConfigEntry<int> OutdatedBiomeMargin;
        public static ConfigEntry<bool> IncludeMagicMaterials;

        public static ConfigEntry<bool> BlockAutoPickup;
        public static ConfigEntry<bool> ShowInTooltip;

        public static ConfigEntry<SparkleMode> FilteredSparkles;
        public static ConfigEntry<Color> SparkleColor;
        public static ConfigEntry<bool> HideLootBeams;

        public static ConfigEntry<KeyboardShortcut> ToggleItem;
        public static ConfigEntry<KeyboardShortcut> ToggleGroup;
        public static ConfigEntry<KeyboardShortcut> ToggleRarity;
        public static ConfigEntry<KeyboardShortcut> ToggleOutdatedRarity;
        public static ConfigEntry<KeyboardShortcut> Reveal;

        public static void Bind(ConfigFile config)
        {
            FilteredItems = config.Bind("Rules", "FilteredItems", "",
                "Comma-separated item names to filter (internal prefab names, e.g. Resin, Stone, RoundLog). " +
                "Easiest to edit with the ToggleItem key while hovering an item.");
            FilteredGroups = config.Bind("Rules", "FilteredGroups", ItemGroups.None,
                "Item groups to filter. Doesn't apply to Epic Loot magic items, which use the rarity rules.");
            FilteredRarities = config.Bind("Rules", "FilteredRarities", Rarities.None,
                "Epic Loot rarities to always filter (only used when Epic Loot is installed). " +
                "For magic items, the rarity rules are the only ones that apply.");
            OutdatedRarities = config.Bind("Rules", "OutdatedRarities", Rarities.None,
                "Epic Loot rarities to filter only when the item is from an earlier biome than the one you're in, " +
                "e.g. in the Plains, hide Magic items from the Black Forest but keep Plains and Mistlands ones. " +
                "Item tiers come from Epic Loot's own item data.");
            OutdatedBiomeMargin = config.Bind("Rules", "OutdatedBiomeMargin", 0,
                new ConfigDescription("How many biomes behind your current one an item may be before it counts as outdated. " +
                    "0 = anything from an earlier biome; 1 = keep the previous biome's items too.",
                    new AcceptableValueRange<int>(0, 8)));
            IncludeMagicMaterials = config.Bind("Rules", "IncludeMagicMaterials", false,
                "Also apply FilteredRarities to Epic Loot's magic crafting materials and runestones.");

            BlockAutoPickup = config.Bind("Behaviour", "BlockAutoPickup", true,
                "Don't auto-pickup filtered items. They can still be picked up manually.");
            ShowInTooltip = config.Bind("Behaviour", "ShowInTooltip", true,
                "Add a line to item tooltips saying whether (and why) the item is filtered.");

            FilteredSparkles = config.Bind("Effects", "FilteredSparkles", SparkleMode.Hidden,
                "What to do with the sparkles and glow of filtered drops: hide them, recolor them with SparkleColor, or leave them alone.");
            SparkleColor = config.Bind("Effects", "SparkleColor", new Color(0.55f, 0.55f, 0.55f, 0.35f),
                "Color of filtered drops' sparkles when FilteredSparkles is Recolored. The alpha (A) value sets how visible they are.");
            HideLootBeams = config.Bind("Effects", "HideLootBeams", true,
                "Hide Epic Loot's loot beams on filtered drops. Independent of FilteredSparkles.");

            ToggleItem = config.Bind("Keys", "ToggleItem", new KeyboardShortcut(KeyCode.Delete),
                "Filter/unfilter the hovered item (inventory, chest or on the ground).");
            ToggleGroup = config.Bind("Keys", "ToggleGroup", new KeyboardShortcut(KeyCode.Delete, KeyCode.LeftShift),
                "Filter/unfilter the hovered item's group (Weapons, Trophies, ...).");
            ToggleRarity = config.Bind("Keys", "ToggleRarity", new KeyboardShortcut(KeyCode.Delete, KeyCode.LeftControl),
                "Always filter/unfilter the hovered item's Epic Loot rarity.");
            ToggleOutdatedRarity = config.Bind("Keys", "ToggleOutdatedRarity",
                new KeyboardShortcut(KeyCode.Delete, KeyCode.LeftControl, KeyCode.LeftShift),
                "Filter/unfilter outdated items of the hovered item's Epic Loot rarity (see OutdatedRarities).");
            Reveal = config.Bind("Keys", "Reveal", new KeyboardShortcut(KeyCode.LeftAlt),
                "Hold to show the original effects of filtered items.");
        }
    }
}
