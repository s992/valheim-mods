using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace UtilityBelt
{
    // The belt's upgrades, in the order they're bought. The base belt (+150 carry weight) is tier 0 and isn't listed:
    // it can't be turned off.
    internal enum Tier
    {
        SwampKey = 1,
        Wishbone = 2,
        FrostResist = 3,
        Wisplight = 4,
        FeatherFall = 5,
        ExtraCarry = 6,
    }

    internal static class Tiers
    {
        public static readonly Tier[] All = (Tier[])Enum.GetValues(typeof(Tier));

        // Item quality runs from 1 (the base belt) to one more than the number of tiers
        public static readonly int MaxQuality = 1 + All.Length;

        // Which upgrades a belt has, in its item data. Quality alone can't say: a tier that was turned off when the
        // belt was upgraded was skipped, so quality 3 isn't always tiers 1 and 2.
        public const string DataKey = "s992.UtilityBelt.upgrades";

        public const string Coins = "Coins";

        // The item each upgrade costs (besides gold), by prefab name; null when it costs gold only
        public static string UpgradeItem(Tier tier) => tier switch
        {
            Tier.SwampKey => "CryptKey",
            Tier.Wishbone => "Wishbone",
            Tier.FrostResist => "CapeWolf",
            Tier.Wisplight => "Demister",
            Tier.FeatherFall => "CapeFeather",
            _ => null,
        };

        // The same item as players know it
        public static string UpgradeItemName(Tier tier) => tier switch
        {
            Tier.SwampKey => "swamp key",
            Tier.Wishbone => "wishbone",
            Tier.FrostResist => "wolf fur cape",
            Tier.Wisplight => "wisplight",
            Tier.FeatherFall => "feather cape",
            _ => null,
        };

        public static string DisplayName(Tier tier) => tier switch
        {
            Tier.SwampKey => "Swamp key",
            Tier.Wishbone => "Wishbone",
            Tier.FrostResist => "Frost resistance",
            Tier.Wisplight => "Wisplight",
            Tier.FeatherFall => "Feather fall",
            Tier.ExtraCarry => "Reinforced",
            _ => tier.ToString(),
        };

        public static string Effect(Tier tier) => tier switch
        {
            Tier.SwampKey => "opens sunken crypts",
            Tier.Wishbone => "finds hidden treasure and ores",
            Tier.FrostResist => "frost resistant, like a wolf fur cape",
            Tier.Wisplight => "clears the mist around you",
            Tier.FeatherFall => "slow fall, no fall damage",
            Tier.ExtraCarry => "+150 more carry weight",
            _ => "",
        };

        /// <summary>The upgrades a belt has, from its item data.</summary>
        /// <param name="quality">
        /// Used when the item data has no record (a belt spawned with a quality by a console command): it then has the
        /// first quality - 1 tiers.
        /// </param>
        public static HashSet<Tier> Owned(IDictionary<string, string> customData, int quality)
        {
            if (customData != null && customData.TryGetValue(DataKey, out string text)) return Parse(text);
            return new HashSet<Tier>(All.Take(Math.Max(0, quality - 1)));
        }

        public static HashSet<Tier> Parse(string text)
        {
            var tiers = new HashSet<Tier>();
            foreach (string part in (text ?? "").Split(','))
            {
                if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) &&
                    Enum.IsDefined(typeof(Tier), number))
                    tiers.Add((Tier)number);
            }
            return tiers;
        }

        public static string Format(IEnumerable<Tier> tiers) =>
            string.Join(",", tiers.Distinct().OrderBy(t => t).Select(t => ((int)t).ToString(CultureInfo.InvariantCulture)));
    }

    // The server's rules: which tiers exist and what they cost. Built from the settings, so plain values for tests.
    internal sealed class TierRules
    {
        private readonly HashSet<Tier> m_enabled;
        private readonly Dictionary<Tier, int> m_gold;

        public TierRules(IEnumerable<Tier> enabled, IDictionary<Tier, int> goldCosts)
        {
            m_enabled = new HashSet<Tier>(enabled);
            m_gold = new Dictionary<Tier, int>(goldCosts);
        }

        public static TierRules Default => new TierRules(Tiers.All, Tiers.All.ToDictionary(t => t, DefaultGold));

        public static int DefaultGold(Tier tier) => tier == Tier.ExtraCarry ? 1500 : 0;

        public bool IsEnabled(Tier tier) => m_enabled.Contains(tier);

        public int Gold(Tier tier) => m_gold.TryGetValue(tier, out int gold) ? Math.Max(0, gold) : 0;

        // An upgrade works only while its tier is on: turning a tier off takes it from belts that already have it
        public bool IsActive(ICollection<Tier> owned, Tier tier) => owned.Contains(tier) && IsEnabled(tier);

        /// <summary>The upgrade a belt gets next: the first tier that's on and that it doesn't have. Null when none.</summary>
        public Tier? Next(ICollection<Tier> owned)
        {
            foreach (Tier tier in Tiers.All)
            {
                if (IsEnabled(tier) && !owned.Contains(tier)) return tier;
            }
            return null;
        }

        /// <summary>How many of an item (by prefab name) the upgrade to a tier costs.</summary>
        public int Amount(Tier tier, string prefabName)
        {
            if (prefabName == Tiers.Coins) return Gold(tier);
            return prefabName == Tiers.UpgradeItem(tier) ? 1 : 0;
        }

        public string Cost(Tier tier)
        {
            var parts = new List<string>();
            string item = Tiers.UpgradeItemName(tier);
            if (item != null) parts.Add("1 " + item);
            if (Gold(tier) > 0) parts.Add(Gold(tier).ToString(CultureInfo.InvariantCulture) + " gold");
            return parts.Count == 0 ? "free" : string.Join(" and ", parts);
        }

        /// <summary>The belt's lines in its item tooltip: what it has, what's off, and the next upgrade.</summary>
        /// <param name="wanted">Whether the player has an upgrade's effect turned on in their own settings.</param>
        public string Describe(ICollection<Tier> owned, Func<Tier, bool> wanted)
        {
            var text = new StringBuilder("\n\n<color=orange>Upgrades</color>");
            foreach (Tier tier in Tiers.All.Where(owned.Contains))
            {
                string line = $"{Tiers.DisplayName(tier)}: {Tiers.Effect(tier)}";
                if (!IsEnabled(tier)) line = $"<color=#888888>{Tiers.DisplayName(tier)}: turned off on this server</color>";
                else if (!wanted(tier)) line = $"<color=#888888>{Tiers.DisplayName(tier)}: off in your settings</color>";
                text.Append("\n  ").Append(line);
            }
            if (!Tiers.All.Any(owned.Contains)) text.Append("\n  none yet");

            Tier? next = Next(owned);
            text.Append(next == null
                ? "\nFully upgraded"
                : $"\nNext at a workbench: <color=orange>{Tiers.DisplayName(next.Value)}</color> ({Cost(next.Value)})");
            return text.ToString();
        }
    }
}
