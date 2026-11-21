using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ValheimMods.Shared;

namespace BuildRadius
{
    internal static class Settings
    {
        // Server-synced (Jotunn sends the server's values to everyone, and only admins can change them in game)
        public static ConfigEntry<float> Multiplier;
        public static ConfigEntry<string> Overrides;

        // Overrides, parsed whenever the settings change
        public static Dictionary<string, float> ParsedOverrides { get; private set; } = new Dictionary<string, float>();

        // Raised when anything here changes, including values arriving from the server
        public static event Action Changed;

        public static void Bind(ConfigFile config)
        {
            const string synced = " Set by the server in multiplayer.";
            Multiplier = config.Bind("1. Server", "Multiplier", 2f,
                new ConfigDescription(
                    "Scales every crafting station's build radius, and the extra radius each extension adds. 1 is the " +
                    "game's own radius. Modded stations are included." +
                    synced,
                    new AcceptableValueRange<float>(0.5f, 10f), Admin(2)));
            Overrides = config.Bind("1. Server", "Station radii", "",
                new ConfigDescription(
                    "Exact base radius in metres for named stations, as name:metres separated by commas, e.g. " +
                    "piece_workbench:40, forge:30. Names are prefab names (piece_workbench, forge, piece_stonecutter, " +
                    "piece_artisanstation, blackforge, piece_magetable) or station names without the $. Extensions " +
                    "still add their bonus times the multiplier. Stations not listed use the multiplier." + synced,
                    null, Admin(1)));

            Multiplier.SettingChanged += OnChanged;
            Overrides.SettingChanged += OnChanged;
            Parse();
        }

        private static ConfigurationManagerAttributes Admin(int order) =>
            new ConfigurationManagerAttributes { IsAdminOnly = true, Order = order };

        private static void OnChanged(object sender, EventArgs e) => Reload();

        // Also called when the server's values arrive, in case they come without a change event
        public static void Reload()
        {
            Parse();
            Changed?.Invoke();
        }

        private static void Parse()
        {
            var problems = new List<string>();
            ParsedOverrides = Radius.ParseOverrides(Overrides.Value, problems);
            if (problems.Count > 0)
            {
                ModInfo.Log.LogWarning(
                    $"Station radii: ignored {string.Join(", ", problems)} (use name:metres, " +
                    $"{Radius.MinOverride}-{Radius.MaxOverride} m)");
            }
        }
    }
}
