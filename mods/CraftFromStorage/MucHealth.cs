using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;

namespace Storage
{
    public enum MucState
    {
        /// <summary>Not checked yet (plugins still loading).</summary>
        Unknown,
        /// <summary>MUC is loaded, recent enough, and its patches are in place: non-owned chests can be used.</summary>
        Healthy,
        /// <summary>MUC isn't installed: only chests this client owns can be used.</summary>
        Missing,
        /// <summary>MUC is older than the version this mod was checked against.</summary>
        TooOld,
        /// <summary>MUC is loaded but some of its patches are missing, e.g. one threw and aborted the rest (MUC issue #19).</summary>
        PatchesMissing,
    }

    // Decides whether chests this client doesn't own may be used. Without a working MUC, a non-owner's edit to a
    // chest is never saved and gets overwritten on the next reload, so in that case we stay owned-only.
    public static class MucHealth
    {
        public const string Guid = "com.maxsch.valheim.MultiUserChest";
        public static readonly System.Version MinVersion = new System.Version(0, 6, 2);

        public static MucState State { get; private set; } = MucState.Unknown;

        /// <summary>One line for logs and the status line; empty when healthy.</summary>
        public static string Problem { get; private set; } = "";

        public static bool CanUseNonOwnedChests => State == MucState.Healthy;

        // The patches our pulls and MUC's multi-user behaviour depend on. MUC applies them all with one PatchAll,
        // so if one throws the later ones are missing too; checking each catches a partial failure.
        private static IEnumerable<(string Name, MethodBase Method, Func<Patches, IEnumerable<Patch>> Kind)> RequiredPatches()
        {
            yield return ("Inventory.AddItem(item, amount, x, y)",
                AccessTools.Method(typeof(Inventory), nameof(Inventory.AddItem),
                    new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) }),
                p => p.Prefixes);
            yield return ("Inventory.AddItem(item)",
                AccessTools.Method(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) }),
                p => p.Prefixes);
            yield return ("Inventory.RemoveItem(item)",
                AccessTools.Method(typeof(Inventory), nameof(Inventory.RemoveItem), new[] { typeof(ItemDrop.ItemData) }),
                p => p.Prefixes);
            yield return ("Container.RPC_RequestOpen",
                AccessTools.Method(typeof(Container), nameof(Container.RPC_RequestOpen)),
                p => p.Prefixes);
            yield return ("InventoryGui.UpdateContainer",
                AccessTools.Method(typeof(InventoryGui), nameof(InventoryGui.UpdateContainer)),
                p => p.Transpilers);
        }

        /// <summary>Run after every plugin's Awake (MUC patches in its Awake).</summary>
        public static void Check(ManualLogSource log)
        {
            (State, Problem) = Evaluate();
            if (State == MucState.Healthy)
                log.LogInfo($"MultiUserChest {Chainloader.PluginInfos[Guid].Metadata.Version} found and healthy: chests other players own can be used");
            else
                log.LogWarning($"{Problem}. Only chests you own will be used (in multiplayer that's usually the ones you opened last).");
        }

        private static (MucState, string) Evaluate()
        {
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out var info))
                return (MucState.Missing, "MultiUserChest isn't installed");

            System.Version version = info.Metadata.Version;
            if (version < MinVersion)
                return (MucState.TooOld, $"MultiUserChest {version} is older than {MinVersion}");

            var missing = new List<string>();
            foreach (var (name, method, kind) in RequiredPatches())
            {
                if (method == null)
                {
                    missing.Add($"{name} (method not found in this game version)");
                    continue;
                }
                Patches patches = Harmony.GetPatchInfo(method);
                if (patches == null || !kind(patches).Any(p => p.owner == Guid))
                    missing.Add(name);
            }
            if (missing.Count > 0)
                return (MucState.PatchesMissing,
                    $"MultiUserChest {version} is loaded but its patches are missing on {string.Join(", ", missing)} " +
                    "(another mod may have broken its patching; see MUC issue #19)");

            return (MucState.Healthy, "");
        }
    }
}
