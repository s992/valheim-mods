using System;
using BepInEx.Configuration;

namespace Thunderdome
{
    internal static class Settings
    {
        // Server-synced (Jotunn sends the server's values to everyone, and only admins can change them in game)
        public static ConfigEntry<WardMode> Mode;
        public static ConfigEntry<float> Radius;
        public static ConfigEntry<string> Cost;

        // Each player's own
        public static ConfigEntry<bool> ShowDome;

        // Raised when anything here changes, including values arriving from the server
        public static event Action Changed;

        public static void Bind(ConfigFile config)
        {
            const string synced = " Set by the server in multiplayer.";
            Mode = config.Bind("1. Server", "Mode", WardMode.Thunderdome,
                new ConfigDescription(
                    "Thunderdome: lightning kills any hostile creature inside the dome, bosses included, and it drops " +
                    "nothing. Repel: monsters turn away like at the traders' camps. Both: monsters turn away, and " +
                    "lightning kills any that come in anyway." + synced,
                    null, Admin(3)));
            Radius = config.Bind("1. Server", "Radius", 30f,
                new ConfigDescription(
                    "Size of every ward's dome in metres. Lightning strikes inside the dome. Repelling reaches a " +
                    "little further: the area is a box as wide as the dome, and monsters with nothing to chase stay " +
                    "about 15 m beyond it. Changing this updates placed wards." + synced,
                    new AcceptableValueRange<float>(5f, 100f), Admin(2)));
            Cost = config.Bind("1. Server", "Cost", CostList.Default,
                new ConfigDescription(
                    "What a ward costs to build, as ItemPrefabName:amount separated by commas. Internal names, e.g. " +
                    "ElderBark is Ancient Bark. Applies to wards built after the change." + synced,
                    null, Admin(1)));
            ShowDome = config.Bind("2. Personal", "Show dome", true,
                "Show the dome around placed wards, like the traders'. It always shows while placing a ward. Just for you.");

            Mode.SettingChanged += OnChanged;
            Radius.SettingChanged += OnChanged;
            Cost.SettingChanged += OnChanged;
            ShowDome.SettingChanged += OnChanged;
        }

        private static ConfigurationManagerAttributes Admin(int order) =>
            new ConfigurationManagerAttributes { IsAdminOnly = true, Order = order };

        private static void OnChanged(object sender, EventArgs e) => Reload();

        // Also called when the server's values arrive, in case they come without a change event
        public static void Reload() => Changed?.Invoke();
    }
}
