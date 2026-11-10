using System.Collections.Generic;

namespace Thunderdome
{
    // The "Cost" setting: "ItemPrefabName:amount" entries separated by commas.
    internal static class CostList
    {
        public const string Default = "SurtlingCore:5,FineWood:10,ElderBark:10,GreydwarfEye:10";

        // Entries that don't parse end up in `problems` rather than silently changing what a ward costs
        public static List<KeyValuePair<string, int>> Parse(string text, List<string> problems)
        {
            var entries = new List<KeyValuePair<string, int>>();
            foreach (string raw in (text ?? "").Split(','))
            {
                string entry = raw.Trim();
                if (entry.Length == 0) continue;
                string[] parts = entry.Split(':');
                if (parts.Length != 2 || parts[0].Trim().Length == 0
                    || !int.TryParse(parts[1].Trim(), out int amount) || amount < 1)
                {
                    problems.Add(entry);
                    continue;
                }
                entries.Add(new KeyValuePair<string, int>(parts[0].Trim(), amount));
            }
            return entries;
        }
    }
}
