using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace Storage
{
    internal static class Settings
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<KeyboardShortcut> ToggleKey;
        public static ConfigEntry<float> Range;
        public static ConfigEntry<bool> ShowStatus;

        public static ConfigEntry<bool> Crafting;
        public static ConfigEntry<bool> Building;
        public static ConfigEntry<bool> Smelting;

        public static ConfigEntry<bool> CartsAndShips;
        public static ConfigEntry<string> ExcludedContainers;

        public static ConfigEntry<string> SmeltBlocklist;
        public static ConfigEntry<KeyboardShortcut> BlocklistKey;
        public static ConfigEntry<string> SmeltSelections;

        public static ConfigEntry<bool> Borrow;
        public static ConfigEntry<bool> LogActions;

        /// <summary>Bumped whenever a setting changes, so cached results can tell they're stale.</summary>
        public static int Version { get; private set; }

        public static HashSet<string> Excluded { get; private set; } = new HashSet<string>();
        public static HashSet<string> Blocklist { get; private set; } = new HashSet<string>();

        public static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("1. General", "Use nearby storage", true,
                "Crafting, building and smelting use materials from nearby chests. The toggle key flips this.");
            ToggleKey = config.Bind("1. General", "Toggle key", new KeyboardShortcut(KeyCode.Home),
                "Turns \"use nearby storage\" on and off.");
            Range = config.Bind("1. General", "Range", 30f,
                new ConfigDescription(
                    "Chests within this many metres of you are used. It's capped at the distance the game guarantees chests are " +
                    "loaded (128 m on the default simulation distance); the status line says when the cap applies.",
                    new AcceptableValueRange<float>(5f, 250f)));
            ShowStatus = config.Bind("1. General", "Show status line", true,
                "Shows a line at the top of the screen while crafting, building or hovering a smelter: how many chests are used, " +
                "how many were skipped and why.");

            Crafting = config.Bind("2. Features", "Crafting", true, "Crafting stations (and upgrades) use nearby storage.");
            Building = config.Bind("2. Features", "Building", true, "Building pieces use nearby storage.");
            Smelting = config.Bind("2. Features", "Smelting", true,
                "Smelters, furnaces, kilns, windmills, spinning wheels and eitr refineries take materials and fuel from nearby storage.");

            CartsAndShips = config.Bind("3. Chests", "Carts and ships", false,
                "Carts and ships nearby count as storage.");
            ExcludedContainers = config.Bind("3. Chests", "Excluded containers", "",
                "Container prefab names that never count as storage, comma separated (e.g. piece_chest_private).");

            SmeltBlocklist = config.Bind("4. Smelting", "Blocklist", "FineWood,RoundLog,YggdrasilWood",
                "Item prefab names that are never put into a smelter automatically, from your inventory or storage, comma separated. " +
                "Using an item from your hotbar on a smelter still works. The blocklist key toggles the material shown when hovering a smelter.");
            BlocklistKey = config.Bind("4. Smelting", "Blocklist key", new KeyboardShortcut(KeyCode.End),
                "While hovering a smelter's input, adds or removes its selected material from the blocklist.");
            SmeltSelections = config.Bind("4. Smelting", "Selected materials", "",
                "Managed by the mod: which material each kind of smelter takes (changed in game with Shift + use).");

            Borrow = config.Bind("5. Diagnostics", "Borrow chests", true,
                "Ask other players for ownership of nearby chests before they're needed, so crafting and building can take " +
                "from them instantly. Off: those chests are only reached by fetching items into your inventory.");
            LogActions = config.Bind("5. Diagnostics", "Log actions", true,
                "Writes every item the mod takes from or fetches out of a chest to BepInEx/StorageOps.log.");

            Parse();
            config.SettingChanged += (_, __) => Parse();
        }

        private static void Parse()
        {
            Excluded = SplitNames(ExcludedContainers.Value);
            Blocklist = SplitNames(SmeltBlocklist.Value);
            Version++;
        }

        public static HashSet<string> SplitNames(string value) =>
            new HashSet<string>(
                (value ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0),
                StringComparer.OrdinalIgnoreCase);

        public static void ToggleBlocklisted(string prefabName)
        {
            var names = SplitNames(SmeltBlocklist.Value);
            if (!names.Remove(prefabName)) names.Add(prefabName);
            SmeltBlocklist.Value = string.Join(",", names);
        }

        // Selections are stored as "SmelterPrefab=ItemPrefab;..." in one config string (per player, local)
        public static string GetSelection(string smelterPrefab)
        {
            foreach (string pair in (SmeltSelections.Value ?? "").Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0 && pair.Substring(0, eq) == smelterPrefab) return pair.Substring(eq + 1);
            }
            return null;
        }

        public static void SetSelection(string smelterPrefab, string itemPrefab)
        {
            var pairs = (SmeltSelections.Value ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !p.StartsWith(smelterPrefab + "=", StringComparison.Ordinal)).ToList();
            pairs.Add($"{smelterPrefab}={itemPrefab}");
            SmeltSelections.Value = string.Join(";", pairs);
        }
    }
}
