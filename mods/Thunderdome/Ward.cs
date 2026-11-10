using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimMods.Shared;

namespace Thunderdome
{
    // On every ward, placed or still a placement ghost. A placed ward can add the same NoMonsters area the traders
    // have, so the game's own monster AI turns away, and/or strike down hostile creatures inside its dome. A ghost does
    // neither: monsters would react to wherever the player aims the hammer.
    internal sealed class Ward : MonoBehaviour, Hoverable
    {
        private const float ScanInterval = 0.5f;
        // Untyped damage skips resistances; this is far past any creature's health
        private const float KillDamage = 1e7f;

        private static readonly List<Ward> s_instances = new List<Ward>();
        // Creatures already struck (by any ward) whose death hasn't happened yet, so they aren't struck twice
        private static readonly HashSet<Character> s_struck = new HashSet<Character>();
        private static readonly List<Character> s_targets = new List<Character>();

        private Piece m_piece;
        private CircleProjector m_ring;
        private GameObject m_dome;
        private GameObject m_area;
        private float m_areaRadius = -1f;
        private bool m_ghost;
        private bool m_placed;
        private float m_scanTimer;
        private FrameGuard m_guard;

        private void Awake()
        {
            m_piece = GetComponent<Piece>();
            m_guard = new FrameGuard(this, "the Thunderdome's lightning");
            // Off until Start knows this is a ghost; the ring is only for placing
            m_ring = GetComponentInChildren<CircleProjector>(true);
            if (m_ring) m_ring.gameObject.SetActive(false);
        }

        private void Start()
        {
            m_ghost = Player.IsPlacementGhost(gameObject);
            var view = GetComponent<ZNetView>();
            m_placed = !m_ghost && view && view.IsValid();
            if (!m_ghost && !m_placed) return;

            s_instances.Add(this);
            Refresh();
            if (m_ghost && m_ring) m_ring.gameObject.SetActive(true);
        }

        private void OnDestroy() => s_instances.Remove(this);

        public static void RefreshAll()
        {
            foreach (Ward ward in s_instances)
            {
                try { ward.Refresh(); }
                catch (Exception e) { ModInfo.Log.LogError($"Couldn't update a ward: {e}"); }
            }
        }

        private void Refresh()
        {
            float radius = Settings.Radius.Value;
            if (m_ring)
            {
                m_ring.m_radius = radius;
                // Vanilla spaces the guard stone's 80 dashes over 32 m: keep that spacing at any radius
                m_ring.m_nrOfSegments = Mathf.Max(20, Mathf.RoundToInt(radius * 2.5f));
            }
            UpdateDome(radius);
            if (!m_placed) return;
            if (!Targeting.Repels(Settings.Mode.Value)) RemoveArea();
            else if (m_areaRadius != radius) RebuildArea(radius);
        }

        private void Update()
        {
            if (!m_placed) return;
            try { Tick(); }
            catch (Exception e) { m_guard.Report(e); }
        }

        // Each client strikes only the creatures it owns (whoever runs their AI), so a creature is struck once however
        // many players are nearby, and the kill happens where its health lives
        private void Tick()
        {
            m_scanTimer -= Time.deltaTime;
            if (m_scanTimer > 0f) return;
            m_scanTimer = ScanInterval;
            if (!Targeting.Strikes(Settings.Mode.Value) || !WardPiece.Lightning) return;

            s_struck.RemoveWhere(c => !c);
            float radiusSq = Settings.Radius.Value * Settings.Radius.Value;
            Vector3 centre = transform.position;
            // Killing removes creatures from the game's list, so pick targets first
            s_targets.Clear();
            foreach (Character character in Character.GetAllCharacters())
            {
                if (!character || s_struck.Contains(character) || character.IsDead()) continue;
                ZNetView view = character.m_nview;
                if (!view || !view.IsValid() || !view.IsOwner()) continue;
                BaseAI ai = character.GetBaseAI();
                if (!Targeting.ShouldStrike(character.GetFaction(), character.IsPlayer(), character.IsTamed(), ai && ai.IsAggravated()))
                    continue;
                if ((character.GetCenterPoint() - centre).sqrMagnitude > radiusSq) continue;
                s_targets.Add(character);
            }
            foreach (Character target in s_targets) Strike(target);
            s_targets.Clear();
        }

        private static void Strike(Character target)
        {
            s_struck.Add(target);
            // A networked, harmless copy of the incinerator's bolt, so every nearby player sees and hears it
            Instantiate(WardPiece.Lightning, target.transform.position, Quaternion.identity);

            // No loot: the drop component's own death handler checks the flag, and the ragdoll only saves loot when
            // the dying creature still has the component
            var drop = target.GetComponent<CharacterDrop>();
            if (drop)
            {
                drop.SetDropsEnabled(false);
                DestroyImmediate(drop);
            }

            var hit = new HitData { m_point = target.GetCenterPoint(), m_dir = Vector3.down };
            hit.m_damage.m_damage = KillDamage;
            target.Damage(hit);
        }

        private void UpdateDome(float radius)
        {
            Material material = WardPiece.Dome;
            bool wanted = material && (m_ghost || Settings.ShowDome.Value);
            if (!wanted)
            {
                if (m_dome) m_dome.SetActive(false);
                return;
            }
            if (!m_dome) m_dome = CreateDome(material);
            m_dome.SetActive(true);
            // Unity's sphere is 1 m across, so its scale is the diameter
            m_dome.transform.localScale = Vector3.one * (radius * 2f);
        }

        private GameObject CreateDome(Material material)
        {
            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            // Only ever looked at: anything solid would get in the way of placing, hovering and walking
            DestroyImmediate(dome.GetComponent<Collider>());
            dome.name = "ForceField";
            dome.transform.SetParent(transform, false);
            var renderer = dome.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return dome;
        }

        // EffectArea reads its collider's bounds once, in Awake, into a static list that monster AI checks. So a new
        // radius needs a new area, and the object stays inactive until the collider is sized.
        private void RebuildArea(float radius)
        {
            RemoveArea();
            var area = new GameObject("NoMonsterArea");
            area.SetActive(false);
            area.layer = LayerMask.NameToLayer("character_trigger");
            area.transform.SetParent(transform, false);
            var collider = area.AddComponent<SphereCollider>();
            collider.radius = radius;
            collider.isTrigger = true;
            area.AddComponent<EffectArea>().m_type = EffectArea.Type.NoMonsters;
            area.SetActive(true);
            m_area = area;
            m_areaRadius = radius;
        }

        private void RemoveArea()
        {
            if (m_area) Destroy(m_area);
            m_area = null;
            m_areaRadius = -1f;
        }

        public string GetHoverName() => m_piece ? m_piece.m_name : "";

        public float GetHoverOffset() => 0f;

        public string GetHoverText()
        {
            string radius = Settings.Radius.Value.ToString("0", CultureInfo.InvariantCulture);
            string what = Settings.Mode.Value switch
            {
                WardMode.Repel => "$piece_thunderdome_repel",
                WardMode.Both => "$piece_thunderdome_both",
                _ => "$piece_thunderdome_strike",
            };
            return Localization.instance.Localize($"{GetHoverName()}\n{what} {radius} m");
        }
    }
}
