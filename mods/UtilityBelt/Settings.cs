using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace UtilityBelt
{
    internal static class Settings
    {
        // Server-synced (Jotunn sends the server's values to everyone, and only admins can change them in game)
        public static ConfigEntry<int> Price;
        private static readonly Dictionary<Tier, ConfigEntry<bool>> s_enabled = new Dictionary<Tier, ConfigEntry<bool>>();
        private static readonly Dictionary<Tier, ConfigEntry<int>> s_gold = new Dictionary<Tier, ConfigEntry<int>>();

        // Each player's own
        public static ConfigEntry<bool> Wishbone;
        public static ConfigEntry<bool> Wisplight;
        public static ConfigEntry<bool> FeatherFall;
        public static ConfigEntry<KeyboardShortcut> WishboneKey;
        public static ConfigEntry<KeyboardShortcut> WisplightKey;
        public static ConfigEntry<KeyboardShortcut> FeatherFallKey;

        // Raised when anything here changes, including values arriving from the server
        public static event Action Changed;

        private static TierRules s_rules;

        public static TierRules Rules => s_rules ??= new TierRules(
            Tiers.All.Where(t => s_enabled[t].Value),
            Tiers.All.ToDictionary(t => t, t => s_gold[t].Value));

        public static void Bind(ConfigFile config)
        {
            const string synced = " Set by the server in multiplayer.";
            int order = 100;
            Price = config.Bind("1. Server", "Haldor price", 950,
                new ConfigDescription("Gold coins Haldor asks for a new belt. He sells it once he sells Megingjord." + synced,
                    new AcceptableValueRange<int>(1, 100000), Admin(order--)));
            foreach (Tier tier in Tiers.All)
            {
                string name = $"Tier {(int)tier} {Tiers.DisplayName(tier).ToLowerInvariant()}";
                string item = Tiers.UpgradeItemName(tier);
                string cost = item == null ? "gold only" : $"a {item} plus this gold";
                s_enabled[tier] = config.Bind("1. Server", name, true,
                    new ConfigDescription(
                        $"Whether the belt has this upgrade ({Tiers.Effect(tier)}). When off, belts skip it when upgrading, " +
                        "and belts that already have it lose its effect until it's back on." + synced,
                        null, Admin(order--)));
                s_gold[tier] = config.Bind("1. Server", name + " gold", TierRules.DefaultGold(tier),
                    new ConfigDescription($"Gold coins this upgrade costs at the workbench ({cost})." + synced,
                        new AcceptableValueRange<int>(0, 100000), Admin(order--)));
            }

            Wishbone = config.Bind("2. Personal", "Wishbone", true,
                "Use the belt's wishbone upgrade (pings near buried treasure and ores). Just for you.");
            WishboneKey = config.Bind("2. Personal", "Wishbone key", new KeyboardShortcut(KeyCode.PageUp),
                "Turns the belt's wishbone on or off.");
            Wisplight = config.Bind("2. Personal", "Wisplight", true,
                "Use the belt's wisplight upgrade (the wisp that clears mist). Just for you.");
            WisplightKey = config.Bind("2. Personal", "Wisplight key", KeyboardShortcut.Empty,
                "Turns the belt's wisplight on or off.");
            FeatherFall = config.Bind("2. Personal", "Feather fall", true,
                "Use the belt's feather fall upgrade (slow fall, no fall damage). Just for you.");
            FeatherFallKey = config.Bind("2. Personal", "Feather fall key", KeyboardShortcut.Empty,
                "Turns the belt's feather fall on or off.");

            Price.SettingChanged += OnChanged;
            foreach (Tier tier in Tiers.All)
            {
                s_enabled[tier].SettingChanged += OnChanged;
                s_gold[tier].SettingChanged += OnChanged;
            }
            Wishbone.SettingChanged += OnChanged;
            Wisplight.SettingChanged += OnChanged;
            FeatherFall.SettingChanged += OnChanged;
        }

        // Whether this player wants an upgrade's effect (the personal on/off settings)
        public static bool Wanted(Tier tier) => tier switch
        {
            Tier.Wishbone => Wishbone.Value,
            Tier.Wisplight => Wisplight.Value,
            Tier.FeatherFall => FeatherFall.Value,
            _ => true,
        };

        private static ConfigurationManagerAttributes Admin(int order) =>
            new ConfigurationManagerAttributes { IsAdminOnly = true, Order = order };

        private static void OnChanged(object sender, EventArgs e) => Reload();

        // Also called when the server's values arrive, in case they come without a change event
        public static void Reload()
        {
            s_rules = null;
            Changed?.Invoke();
        }
    }
}
