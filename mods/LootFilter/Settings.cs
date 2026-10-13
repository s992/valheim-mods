using BepInEx.Configuration;
using UnityEngine;

namespace LootFilter
{
    internal static class Settings
    {
        public static ConfigEntry<string> FilteredItems;
        public static ConfigEntry<ItemGroups> FilteredGroups;
        public static ConfigEntry<Rarities> FilteredRarities;
        public static ConfigEntry<bool> IncludeMagicMaterials;

        public static ConfigEntry<bool> BlockAutoPickup;
        public static ConfigEntry<bool> HideEffects;
        public static ConfigEntry<bool> ShowInTooltip;

        public static ConfigEntry<KeyboardShortcut> ToggleItem;
        public static ConfigEntry<KeyboardShortcut> ToggleGroup;
        public static ConfigEntry<KeyboardShortcut> ToggleRarity;
        public static ConfigEntry<KeyboardShortcut> Reveal;

        public static void Bind(ConfigFile config)
        {
            FilteredItems = config.Bind("Rules", "FilteredItems", "",
                "Comma-separated item names to filter (internal prefab names, e.g. Resin, Stone, RoundLog). " +
                "Easiest to edit with the ToggleItem key while hovering an item.");
            FilteredGroups = config.Bind("Rules", "FilteredGroups", ItemGroups.None,
                "Item groups to filter. Doesn't apply to Epic Loot magic items, which use FilteredRarities.");
            FilteredRarities = config.Bind("Rules", "FilteredRarities", Rarities.None,
                "Epic Loot rarities to filter (only used when Epic Loot is installed). " +
                "For magic items, this is the only rule that applies.");
            IncludeMagicMaterials = config.Bind("Rules", "IncludeMagicMaterials", false,
                "Also apply FilteredRarities to Epic Loot's magic crafting materials and runestones.");

            BlockAutoPickup = config.Bind("Behaviour", "BlockAutoPickup", true,
                "Don't auto-pickup filtered items. They can still be picked up manually.");
            HideEffects = config.Bind("Behaviour", "HideEffects", true,
                "Turn off sparkles, lights and Epic Loot beams on filtered items lying on the ground.");
            ShowInTooltip = config.Bind("Behaviour", "ShowInTooltip", true,
                "Add a line to item tooltips saying whether (and why) the item is filtered.");

            ToggleItem = config.Bind("Keys", "ToggleItem", new KeyboardShortcut(KeyCode.Delete),
                "Filter/unfilter the hovered item (inventory, chest or on the ground).");
            ToggleGroup = config.Bind("Keys", "ToggleGroup", new KeyboardShortcut(KeyCode.Delete, KeyCode.LeftShift),
                "Filter/unfilter the hovered item's group (Weapons, Trophies, ...).");
            ToggleRarity = config.Bind("Keys", "ToggleRarity", new KeyboardShortcut(KeyCode.Delete, KeyCode.LeftControl),
                "Filter/unfilter the hovered item's Epic Loot rarity.");
            Reveal = config.Bind("Keys", "Reveal", new KeyboardShortcut(KeyCode.LeftAlt),
                "Hold to show effects on filtered items again.");
        }
    }
}
