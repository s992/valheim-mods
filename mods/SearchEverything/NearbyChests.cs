using System.Collections.Generic;
using UnityEngine;

namespace SearchEverything
{
    /// <summary>
    /// Player-built containers near the camera that the player may open. Read-only: every client already receives
    /// chest contents (Container reloads them from the synced ZDO when they change), so no ownership is needed.
    /// </summary>
    internal static class NearbyChests
    {
        private static readonly List<Container> s_all = new List<Container>();
        // Facts that don't change after a container is placed (on a player, a tombstone, built by a player)
        private static readonly Dictionary<Container, bool> s_built = new Dictionary<Container, bool>();

        public static readonly List<(Container Chest, float Distance)> Found = new List<(Container, float)>();
        public static Vector3 Origin { get; private set; }
        /// <summary>How far around the player the game is sure to have chests loaded (0 when unknown).</summary>
        public static float LoadedRadius { get; private set; }

        public static void Register(Container container)
        {
            if (container && container.GetInventory() != null) s_all.Add(container);
        }

        public static void Scan()
        {
            Found.Clear();
            Prune();
            Player player = Player.m_localPlayer;
            if (!player || !ZNet.instance) return;

            LoadedRadius = GuaranteedLoadedRadius(ZNet.instance.GetSyncedSimulationDistance());
            Camera camera = Utils.GetMainCamera();
            Origin = camera ? camera.transform.position : player.transform.position;
            float range = Settings.Range.Value;
            long playerId = player.GetPlayerID();
            foreach (Container chest in s_all)
            {
                if (!chest.isActiveAndEnabled || !IsPlayerBuilt(chest)) continue;
                if (!Settings.CartsAndShips.Value && (chest.m_wagon || chest.GetComponentInParent<Ship>())) continue;
                ZNetView nview = chest.m_nview;
                if (!nview || !nview.IsValid()) continue;
                float distance = Vector3.Distance(Origin, chest.transform.position);
                if (distance > range) continue;
                if (!chest.CheckAccess(playerId)) continue;
                if (chest.m_checkGuardStone && !PrivateArea.CheckAccess(chest.transform.position, 0f, false, false)) continue;
                Found.Add((chest, distance));
            }
            Found.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        }

        private static void Prune()
        {
            if (s_all.RemoveAll(c => !c) == 0) return;
            var dead = new List<Container>();
            foreach (Container c in s_built.Keys) if (!c) dead.Add(c);
            foreach (Container c in dead) s_built.Remove(c);
        }

        private static bool IsPlayerBuilt(Container chest)
        {
            if (s_built.TryGetValue(chest, out bool ok)) return ok;
            Piece piece = chest.GetComponentInParent<Piece>();
            ok = !chest.GetComponentInParent<Player>() && !chest.GetComponentInParent<TombStone>() && piece && piece.IsPlacedByPlayer();
            s_built[chest] = ok;
            return ok;
        }

        private static SimulationDistance s_radiusFor;
        private static float s_radius;

        /// <summary>
        /// How far from the player chests are certain to be loaded, wherever the player stands in their 64 m zone
        /// (same reasoning as CraftFromStorage's StorageNetwork: zones up to N rings out are loaded, and non-classic
        /// settings skip the corner zones). Classic N gives N × 64 m.
        /// </summary>
        private static float GuaranteedLoadedRadius(SimulationDistance distance)
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
                    float dx = Mathf.Max(0, Mathf.Abs(x) - 1) * zone, dy = Mathf.Max(0, Mathf.Abs(y) - 1) * zone;
                    best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy));
                }
            }
            s_radiusFor = distance;
            s_radius = Mathf.Max(zone, best);
            return s_radius;
        }
    }
}
