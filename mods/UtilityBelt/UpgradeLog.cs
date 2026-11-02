using System.Collections.Generic;
using System.Linq;
using ValheimMods.Shared;

namespace UtilityBelt
{
    // TEMPORARY: traces every decision the upgrade code makes, to debug upgrades in game. Lines start with
    // "[upgrade]" in BepInEx/LogOutput.log. Remove once upgrades work.
    internal static class UpgradeLog
    {
        // Several of these run every frame while the crafting panel is open: each site logs only when its line changes
        private static readonly Dictionary<string, string> s_last = new Dictionary<string, string>();

        public static void Write(string message) => ModInfo.Log.LogInfo("[upgrade] " + message);

        public static void Changed(string site, string message)
        {
            if (s_last.TryGetValue(site, out string last) && last == message) return;
            s_last[site] = message;
            Write($"{site}: {message}");
        }

        public static string Describe(ItemDrop.ItemData item)
        {
            if (item == null) return "none";
            item.m_customData.TryGetValue(Tiers.DataKey, out string data);
            HashSet<Tier> owned = Tiers.Owned(item.m_customData, item.m_quality);
            return $"{item.m_shared.m_name} q{item.m_quality}/max{item.m_shared.m_maxQuality} " +
                   $"data={(data == null ? "<none>" : "'" + data + "'")} owned=[{Tiers.Format(owned)}] " +
                   $"next={Settings.Rules.Next(owned)?.ToString() ?? "none"} pos={item.m_gridPos} " +
                   $"equipped={item.m_equipped} hash={item.GetHashCode()}";
        }

        public static string Rules()
        {
            TierRules rules = Settings.Rules;
            return "rules: " + string.Join(", ", Tiers.All.Select(t => $"{t}={(rules.IsEnabled(t) ? "on" : "off")}/{rules.Gold(t)}g"));
        }

        public static string Belts(Player player)
        {
            if (!player) return "no player";
            var belts = player.GetInventory().GetAllItems().Where(Belt.IsBelt).Select(Describe).ToList();
            return belts.Count == 0 ? "no belts in inventory" : string.Join(" | ", belts);
        }

        public static string Recipe(Recipe recipe)
        {
            if (!recipe) return "no recipe";
            return $"recipe {recipe.name} enabled={recipe.m_enabled} noCraftOnlyUpgrade={recipe.m_noCraftOnlyUpgrade} " +
                   $"station={(recipe.m_craftingStation ? recipe.m_craftingStation.name : "none")} " +
                   $"minLevel={recipe.m_minStationLevel} resources=[" +
                   string.Join(", ", recipe.m_resources.Select(r =>
                       $"{(r.m_resItem ? r.m_resItem.gameObject.name : "<null>")} amount={r.m_amount} perLevel={r.m_amountPerLevel}")) + "]";
        }
    }
}
