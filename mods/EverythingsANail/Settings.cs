using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace EverythingsANail
{
    // What a tool does, detected from its item data rather than a list of names,
    // so tools added by other mods land in the right category
    [Flags]
    public enum ToolCategories
    {
        None = 0,
        Building = 1,
        Butchering = 2,
        Harvesting = 4,
        Fishing = 8,
        Logging = 16,
        Mining = 32,
    }

    // Which tools the hammer may become. Either the local config or, in multiplayer, the server's.
    internal sealed class Rules
    {
        public ToolCategories Categories;
        public HashSet<string> OnlyAllow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static HashSet<string> ParseList(string text) =>
            new HashSet<string>(
                (text ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(name => name.Trim())
                    .Where(name => name.Length > 0),
                StringComparer.OrdinalIgnoreCase);

        public static string FormatList(IEnumerable<string> names) => string.Join(", ", names);
    }

    internal static class Settings
    {
        public static ConfigEntry<ToolCategories> Categories;
        public static ConfigEntry<string> OnlyAllow;
        public static ConfigEntry<string> Exclude;

        // Raised when the rules in force change: a local edit or new values from the server
        public static event Action RulesChanged;

        private static Rules s_local;

        public static void Bind(ConfigFile config)
        {
            const string enforced = " In multiplayer the server's setting is used when the server runs this mod.";
            Categories = config.Bind("Tools", "Categories",
                ToolCategories.Building | ToolCategories.Butchering | ToolCategories.Harvesting | ToolCategories.Fishing,
                "Kinds of tools the hammer can become. Building: anything with a build menu (hoe, cultivator, " +
                "tools from other mods). Butchering: butcher knife. Harvesting: scythe. Fishing: fishing rod. " +
                "Logging: one-handed axes. Mining: pickaxes. The hammer itself is always available." + enforced);
            OnlyAllow = config.Bind("Tools", "OnlyAllow", "",
                "Comma-separated internal item names (e.g. AxeJotunBane, FishingRod). When set, only these tools " +
                "are available and Categories is ignored. The nail_tools console command lists the names." + enforced);
            Exclude = config.Bind("Tools", "Exclude", "",
                "Comma-separated internal item names that are never available, even if their category is enabled." + enforced);

            Categories.SettingChanged += (_, __) => OnLocalChanged();
            OnlyAllow.SettingChanged += (_, __) => OnLocalChanged();
            Exclude.SettingChanged += (_, __) => OnLocalChanged();
        }

        public static Rules Local
        {
            get
            {
                if (s_local == null)
                {
                    s_local = new Rules
                    {
                        Categories = Categories.Value,
                        OnlyAllow = Rules.ParseList(OnlyAllow.Value),
                        Exclude = Rules.ParseList(Exclude.Value),
                    };
                }
                return s_local;
            }
        }

        // The server's rules when connected to a server that runs the mod, otherwise the local ones
        public static Rules Active => ConfigSync.Remote ?? Local;

        private static void OnLocalChanged()
        {
            s_local = null;
            ConfigSync.Broadcast();
            NotifyRulesChanged();
        }

        public static void NotifyRulesChanged() => RulesChanged?.Invoke();
    }
}
