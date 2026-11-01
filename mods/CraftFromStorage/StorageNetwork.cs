using System.Collections.Generic;
using UnityEngine;

namespace CraftFromStorage
{
    // Which chests count as storage right now, and how many of an item they hold.
    // Read-only: nothing here changes an inventory (ChestAccess does that).
    internal static class StorageNetwork
    {
        private const float RefreshInterval = 0.5f;
        private const float UnresponsiveSeconds = 30f;

        private static readonly List<Container> s_all = new List<Container>();
        private static readonly HashSet<Inventory> s_inventories = new HashSet<Inventory>();
        // Facts about a container that don't change after it's placed (on a player, a tombstone, placed by a player)
        private static readonly Dictionary<Container, bool> s_staticOk = new Dictionary<Container, bool>();
        private static readonly Dictionary<ZDOID, float> s_unresponsiveUntil = new Dictionary<ZDOID, float>();

        // Nearest first. Candidates: everything in range we may use, incl. chests other players own even when MUC is unhealthy
        // (borrowing them still works). Usable: candidates whose contents can be taken or pulled right now.
        private static readonly List<Container> s_candidates = new List<Container>();
        private static readonly List<Container> s_usable = new List<Container>();
        private static readonly List<(Container Chest, float Distance)> s_sortBuffer = new List<(Container, float)>();

        private static readonly Dictionary<(string Name, int Quality, bool MatchWorldLevel, bool OwnedOnly), int> s_counts =
            new Dictionary<(string, int, bool, bool), int>();
        private static int s_contentVersion;
        private static int s_countsVersion = -1;
        private static int s_settingsVersion = -1;
        private static int s_setSignature;
        private static float s_nextRefresh;

        public static int SkippedNoOwner { get; private set; }
        public static int InUseByOthers { get; private set; }
        public static int Unresponsive { get; private set; }
        public static float EffectiveRange { get; private set; }
        public static bool RangeCapped { get; private set; }

        public static IReadOnlyList<Container> Candidates { get { Refresh(); return s_candidates; } }
        public static IReadOnlyList<Container> Usable { get { Refresh(); return s_usable; } }

        public static void Register(Container container)
        {
            if (!container || container.GetInventory() == null) return;
            s_all.Add(container);
            s_inventories.Add(container.GetInventory());
        }

        public static void OnInventoryChanged(Inventory inventory)
        {
            if (s_inventories.Contains(inventory)) s_contentVersion++;
        }

        /// <summary>Counts may be stale (ownership changed, a borrow finished): recount on next use.</summary>
        public static void Invalidate()
        {
            s_contentVersion++;
            s_nextRefresh = 0f;
        }

        public static void MarkUnresponsive(Container container)
        {
            // A chest we own answers to us, so it can't be "not answering"
            if (!container || !container.m_nview || !container.m_nview.IsValid() || container.m_nview.IsOwner()) return;
            s_unresponsiveUntil[container.m_nview.GetZDO().m_uid] = Time.time + UnresponsiveSeconds;
            Invalidate();
        }

        public static bool IsSinglePlayer() =>
            ZNet.instance && ZNet.instance.IsServer() && !ZNet.instance.IsDedicated() && ZNet.instance.GetConnectedPeers().Count == 0;

        /// <summary>We own it, or nobody does and we're alone (then it's safe to claim).</summary>
        public static bool IsOwnedForUse(Container container)
        {
            ZNetView nview = container.m_nview;
            if (!nview || !nview.IsValid()) return false;
            return nview.IsOwner() || (nview.GetZDO().GetOwner() == 0L && IsSinglePlayer());
        }

        // Enchanted Epic Loot gear is never used up as a crafting material
        public static bool IsUsableItem(ItemDrop.ItemData item) => !EpicLootCompat.IsEnchanted(item);

        public static bool Matches(ItemDrop.ItemData item, string name, int quality, bool matchWorldLevel) =>
            item.m_shared.m_name == name && (quality < 0 || item.m_quality == quality) &&
            (!matchWorldLevel || item.m_worldLevel >= Game.m_worldLevel) && IsUsableItem(item);

        /// <summary>Same semantics as vanilla Inventory.CountItems, summed over storage (inventory not included).</summary>
        public static int Count(string name, int quality, bool matchWorldLevel, bool ownedOnly)
        {
            Refresh();
            if (s_countsVersion != s_contentVersion || s_settingsVersion != Settings.Version)
            {
                s_counts.Clear();
                s_countsVersion = s_contentVersion;
                s_settingsVersion = Settings.Version;
            }
            var key = (name, quality, matchWorldLevel, ownedOnly);
            if (s_counts.TryGetValue(key, out int cached)) return cached;

            int total = 0;
            foreach (Container chest in s_usable)
            {
                if (ownedOnly && !IsOwnedForUse(chest)) continue;
                total += CountIn(chest.GetInventory(), name, quality, matchWorldLevel);
            }
            s_counts[key] = total;
            return total;
        }

        public static int CountIn(Inventory inventory, string name, int quality, bool matchWorldLevel)
        {
            int total = 0;
            foreach (ItemDrop.ItemData item in inventory.m_inventory)
                if (Matches(item, name, quality, matchWorldLevel)) total += item.m_stack;
            return total;
        }

        /// <summary>Any usable item with this name from storage, e.g. as the "which ingredient" answer for one-ingredient recipes.</summary>
        public static ItemDrop.ItemData FindItem(string name, int quality)
        {
            foreach (Container chest in Usable)
                foreach (ItemDrop.ItemData item in chest.GetInventory().m_inventory)
                    if (Matches(item, name, quality, true)) return item;
            return null;
        }

        private static void Refresh()
        {
            if (Time.time < s_nextRefresh) return;
            s_nextRefresh = Time.time + RefreshInterval;

            s_candidates.Clear();
            s_usable.Clear();
            SkippedNoOwner = InUseByOthers = Unresponsive = 0;
            Player player = Player.m_localPlayer;
            if (!player || !ZNet.instance)
            {
                s_setSignature = 0;
                return;
            }

            Prune();

            float cap = GuaranteedLoadedRadius(ZNet.instance.GetSyncedSimulationDistance());
            RangeCapped = Settings.Range.Value > cap;
            EffectiveRange = Mathf.Min(Settings.Range.Value, cap);

            Vector3 origin = player.transform.position;
            long playerId = player.GetPlayerID();
            bool singlePlayer = IsSinglePlayer();
            s_sortBuffer.Clear();
            foreach (Container chest in s_all)
            {
                if (!chest.isActiveAndEnabled || !IsStaticallyOk(chest) || !IsAllowedKind(chest)) continue;
                ZNetView nview = chest.m_nview;
                if (!nview || !nview.IsValid()) continue;
                float distance = Vector3.Distance(origin, chest.transform.position);
                if (distance > EffectiveRange) continue;
                if (!chest.CheckAccess(playerId)) continue;
                if (chest.m_checkGuardStone && !PrivateArea.CheckAccess(chest.transform.position, 0f, false, false)) continue;
                ZDO zdo = nview.GetZDO();
                if (zdo.GetBool("MUC_Ignore")) continue;
                if (zdo.GetOwner() == 0L && !singlePlayer)
                {
                    // Nobody is responsible for writing it, so neither MUC nor we touch it
                    SkippedNoOwner++;
                    continue;
                }
                if (s_unresponsiveUntil.TryGetValue(zdo.m_uid, out float until))
                {
                    // Became ours since (borrowed, or opened): usable again right away
                    if (Time.time < until && !nview.IsOwner())
                    {
                        Unresponsive++;
                        continue;
                    }
                    s_unresponsiveUntil.Remove(zdo.m_uid);
                }
                s_sortBuffer.Add((chest, distance));
            }
            s_sortBuffer.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            int signature = 17;
            foreach (var (chest, _) in s_sortBuffer)
            {
                s_candidates.Add(chest);
                bool owned = IsOwnedForUse(chest);
                if (!owned && chest.m_nview.GetZDO().GetInt(ZDOVars.s_inUse) == 1) InUseByOthers++;
                if (owned || MucHealth.CanUseNonOwnedChests)
                {
                    s_usable.Add(chest);
                    signature = signature * 31 + chest.GetInstanceID() * (owned ? 3 : 7);
                }
            }
            // Chests entering/leaving range or changing owner change what counts mean
            if (signature != s_setSignature)
            {
                s_setSignature = signature;
                s_contentVersion++;
            }
        }

        private static SimulationDistance s_radiusFor;
        private static float s_radius;

        /// <summary>
        /// How far from the player chests are certain to be loaded, wherever the player stands in their 64 m zone.
        /// Mirrors ZDOMan.FindSectorObjects: zones up to N rings out are loaded, except that non-classic settings skip
        /// zones whose centre is (N + 0.5) zones or more away (the corners). The answer is the distance from the
        /// player's zone to the nearest zone that isn't loaded. Classic N gives N × 64 m; e.g. non-classic 2 gives ~90 m.
        /// </summary>
        public static float GuaranteedLoadedRadius(SimulationDistance distance)
        {
            if (distance.Equals(s_radiusFor) && s_radius > 0f) return s_radius;
            int rings = Mathf.Max(0, distance.NearSimulationDistance);
            float zone = ZoneSystem.c_ZoneSize;
            float best = (rings + 1) * zone;
            for (int x = -(rings + 1); x <= rings + 1; x++)
            {
                for (int y = -(rings + 1); y <= rings + 1; y++)
                {
                    bool outsideRings = Mathf.Abs(x) > rings || Mathf.Abs(y) > rings;
                    bool cornerSkipped = !distance.IsClassic && new Vector2(x, y).magnitude * zone >= (rings + 0.5f) * zone;
                    if (!outsideRings && !cornerSkipped) continue;
                    // Gap between the player's zone and that zone
                    float dx = Mathf.Max(0, Mathf.Abs(x) - 1) * zone, dy = Mathf.Max(0, Mathf.Abs(y) - 1) * zone;
                    best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy));
                }
            }
            s_radiusFor = distance;
            s_radius = Mathf.Max(zone, best);
            return s_radius;
        }

        /// <summary>
        /// Drops destroyed containers. Every container that loads is registered (dungeon chests, crates, ...), so this
        /// also runs on a timer, not only when storage is queried, or the lists would grow all session.
        /// </summary>
        public static void Prune()
        {
            if (s_all.RemoveAll(c => !c) == 0) return;
            s_inventories.Clear();
            foreach (Container c in s_all) s_inventories.Add(c.GetInventory());
            var dead = new List<Container>();
            foreach (Container c in s_staticOk.Keys) if (!c) dead.Add(c);
            foreach (Container c in dead) s_staticOk.Remove(c);
        }

        private static bool IsStaticallyOk(Container chest)
        {
            if (s_staticOk.TryGetValue(chest, out bool ok)) return ok;
            Piece piece = chest.GetComponentInParent<Piece>();
            ok = !chest.GetComponentInParent<Player>() && !chest.GetComponentInParent<TombStone>() && piece && piece.IsPlacedByPlayer();
            s_staticOk[chest] = ok;
            return ok;
        }

        private static bool IsAllowedKind(Container chest)
        {
            if (!Settings.CartsAndShips.Value && (chest.m_wagon || chest.GetComponentInParent<Ship>())) return false;
            return Settings.Excluded.Count == 0 || !Settings.Excluded.Contains(PrefabName(chest.m_nview ? chest.m_nview.gameObject : chest.gameObject));
        }

        public static string PrefabName(GameObject go) => go.name.Replace("(Clone)", "").Trim();
    }
}
