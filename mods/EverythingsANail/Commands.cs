using System.Linq;

namespace EverythingsANail
{
    internal static class Commands
    {
        public static void Register()
        {
            new Terminal.ConsoleCommand("nail_tools",
                "Lists the tools the hammer can become, with their internal names (for the OnlyAllow and Exclude settings)",
                args => ListTools(args.Context));
        }

        private static void ListTools(Terminal terminal)
        {
            if (terminal == null) return;
            Player player = Player.m_localPlayer;
            Rules rules = Settings.Active;
            string source = ConfigSync.Remote != null ? "the server" : "your config";
            terminal.AddString($"Rules from {source}: categories {rules.Categories}" +
                               (rules.OnlyAllow.Count > 0 ? $", only allow {Rules.FormatList(rules.OnlyAllow)} (categories ignored)" : "") +
                               (rules.Exclude.Count > 0 ? $", exclude {Rules.FormatList(rules.Exclude)}" : ""));
            foreach (ToolInfo tool in ToolCatalog.All.OrderBy(tool => tool.Category).ThenBy(tool => tool.Group).ThenByDescending(tool => tool.Rank))
            {
                bool allowed = ToolCatalog.IsAllowed(tool);
                bool unlocked = ToolCatalog.IsUnlocked(player, tool);
                string status = allowed && unlocked ? "<color=green>available</color>"
                    : string.Join(", ", new[] { allowed ? null : "not allowed", unlocked ? null : "never owned" }.Where(text => text != null));
                terminal.AddString($"  {tool.Category}: {tool.Prefab} ({tool.DisplayName}) - {status}");
            }
        }
    }
}
