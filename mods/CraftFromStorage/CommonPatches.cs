using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Storage
{
    [HarmonyPatch(typeof(Container), nameof(Container.Awake))]
    internal static class ContainerAwakePatch
    {
        private static void Postfix(Container __instance) => StorageNetwork.Register(__instance);
    }

    // Recount when any tracked chest's contents change (also fires when a chest reloads from the network)
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.Changed))]
    internal static class InventoryChangedPatch
    {
        private static void Postfix(Inventory __instance) => StorageNetwork.OnInventoryChanged(__instance);
    }

    [HarmonyPatch(typeof(Game), nameof(Game.Start))]
    internal static class GameStartPatch
    {
        private static void Postfix() => ChestAccess.RegisterRpcs();
    }

    // "storage" console command: status, and an audit of what's in reach (to compare before/after a test session)
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
    internal static class TerminalPatch
    {
        private static void Postfix()
        {
            new Terminal.ConsoleCommand("storage", "[status|audit] - nearby storage: what's used, and item totals",
                args =>
                {
                    string sub = args.Args.Length > 1 ? args.Args[1].ToLowerInvariant() : "status";
                    if (sub == "audit") Audit(args.Context);
                    else
                    {
                        args.Context?.AddString(Status.Line());
                        args.Context?.AddString($"Borrowing this session: asked {ChestAccess.BorrowsSent}, granted {ChestAccess.BorrowsGranted}, " +
                                                $"handed to others {ChestAccess.HandedAway}");
                    }
                },
                optionsFetcher: () => new List<string> { "status", "audit" });
        }

        private static void Audit(Terminal terminal)
        {
            Player player = Player.m_localPlayer;
            if (!player)
            {
                terminal?.AddString("Storage audit needs a player in the world");
                return;
            }
            var totals = new SortedDictionary<string, int>();
            void Add(Inventory inventory)
            {
                foreach (ItemDrop.ItemData item in inventory.m_inventory)
                {
                    string key = item.m_shared.m_name;
                    totals[key] = (totals.TryGetValue(key, out int n) ? n : 0) + item.m_stack;
                }
            }
            IReadOnlyList<Container> chests = StorageNetwork.Candidates;
            foreach (Container chest in chests) Add(chest.GetInventory());
            Add(player.GetInventory());

            int items = totals.Values.Sum();
            string summary = $"Storage audit: {chests.Count} chests in reach + your inventory: {items} items, {totals.Count} kinds";
            terminal?.AddString(summary + " (full list in BepInEx/StorageOps.log)");
            StorageLog.Action("AUDIT " + summary);
            foreach (var pair in totals)
                StorageLog.Action($"AUDIT   {Localization.instance.Localize(pair.Key)} ({pair.Key}): {pair.Value}");
        }
    }
}
