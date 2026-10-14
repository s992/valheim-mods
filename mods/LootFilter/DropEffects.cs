using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LootFilter
{
    // Changes the effects of filtered drops lying on the ground, keeping the model and hover text:
    // - Epic Loot's loot beam is hidden (HideLootBeams)
    // - other particles and lights (sparkles, glow) are hidden, recolored or left alone (FilteredSparkles)
    // Everything changed is remembered so it can be put back exactly.
    internal sealed class DropEffects : MonoBehaviour
    {
        private const float ScanInterval = 0.25f;

        private sealed class State
        {
            public readonly List<Behaviour> DisabledBehaviours = new List<Behaviour>();
            public readonly List<GameObject> HiddenObjects = new List<GameObject>();
            public readonly List<ParticleSystem> StoppedParticles = new List<ParticleSystem>();
            public readonly Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient> RecoloredParticles =
                new Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient>();
            public readonly Dictionary<Light, (Color color, float intensity)> RecoloredLights =
                new Dictionary<Light, (Color, float)>();
        }

        // Epic Loot adds a LootBeam component to magic drops, which spawns its beam object (_beam) in Update
        private static readonly Type LootBeamType = AccessTools.TypeByName("EpicLoot.LootBeams.LootBeam");
        private static readonly FieldInfo LootBeamObject = LootBeamType != null ? AccessTools.Field(LootBeamType, "_beam") : null;

        private readonly Dictionary<ItemDrop, State> _changed = new Dictionary<ItemDrop, State>();
        private float _nextScan;
        private int _seenVersion = -1;
        private bool _wasRevealing;
        private bool _effectSettingsChanged;

        private void Awake()
        {
            Settings.FilteredSparkles.SettingChanged += OnEffectSettingChanged;
            Settings.SparkleColor.SettingChanged += OnEffectSettingChanged;
            Settings.HideLootBeams.SettingChanged += OnEffectSettingChanged;
        }

        private void OnEffectSettingChanged(object sender, EventArgs e) => _effectSettingsChanged = true;

        private void Update()
        {
            bool revealing = Input.Held(Settings.Reveal);
            bool dirty = revealing != _wasRevealing || _seenVersion != Rules.Version || _effectSettingsChanged;
            if (!dirty && Time.time < _nextScan) return;

            // New effect settings: put everything back, then apply them fresh
            if (_effectSettingsChanged)
            {
                RestoreAll();
                _effectSettingsChanged = false;
            }

            _wasRevealing = revealing;
            _seenVersion = Rules.Version;
            _nextScan = Time.time + ScanInterval;
            Scan(revealing);
        }

        private void OnDestroy() => RestoreAll();

        private void RestoreAll()
        {
            foreach (State state in _changed.Values) Restore(state);
            _changed.Clear();
        }

        private void Scan(bool revealing)
        {
            bool anyEffect = Settings.HideLootBeams.Value || Settings.FilteredSparkles.Value != SparkleMode.Unchanged;

            foreach (ItemDrop drop in ItemDrop.s_instances)
            {
                if (!drop) continue;

                // Items you dropped yourself (m_autoPickup off) are left alone
                bool change = anyEffect && !revealing && drop.m_autoPickup && Rules.IsFiltered(drop.m_itemData);
                bool isChanged = _changed.TryGetValue(drop, out State state);

                if (change)
                {
                    if (!isChanged) _changed[drop] = state = new State();
                    // Run every scan: effects such as Epic Loot's beam can appear after the drop spawns
                    Apply(drop, state);
                }
                else if (isChanged)
                {
                    Restore(state);
                    _changed.Remove(drop);
                }
            }

            // Forget drops that were picked up or despawned
            foreach (ItemDrop gone in _changed.Keys.Where(d => !d).ToList())
                _changed.Remove(gone);
        }

        private static void Apply(ItemDrop drop, State state)
        {
            GameObject beam = null;
            if (LootBeamType != null && drop.GetComponent(LootBeamType) is Behaviour lootBeam)
            {
                beam = LootBeamObject?.GetValue(lootBeam) as GameObject;
                if (Settings.HideLootBeams.Value)
                {
                    if (lootBeam.enabled)
                    {
                        lootBeam.enabled = false;
                        state.DisabledBehaviours.Add(lootBeam);
                    }
                    if (beam && beam.activeSelf)
                    {
                        beam.SetActive(false);
                        state.HiddenObjects.Add(beam);
                    }
                }
            }

            SparkleMode mode = Settings.FilteredSparkles.Value;
            if (mode == SparkleMode.Unchanged) return;
            Color color = Settings.SparkleColor.Value;

            foreach (ParticleSystem particles in drop.GetComponentsInChildren<ParticleSystem>())
            {
                // The beam has its own setting
                if (beam && particles.transform.IsChildOf(beam.transform)) continue;

                if (mode == SparkleMode.Hidden || color.a <= 0f)
                {
                    if (particles.isPlaying)
                    {
                        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        state.StoppedParticles.Add(particles);
                    }
                }
                else if (!state.RecoloredParticles.ContainsKey(particles))
                {
                    ParticleSystem.MainModule main = particles.main;
                    state.RecoloredParticles[particles] = main.startColor;
                    main.startColor = new ParticleSystem.MinMaxGradient(color);
                    // Already-emitted particles keep their old color, so start over
                    particles.Clear(false);
                }
            }

            foreach (Light light in drop.GetComponentsInChildren<Light>())
            {
                if (beam && light.transform.IsChildOf(beam.transform)) continue;

                if (mode == SparkleMode.Hidden || color.a <= 0f)
                {
                    if (light.enabled)
                    {
                        light.enabled = false;
                        state.DisabledBehaviours.Add(light);
                    }
                }
                else if (!state.RecoloredLights.ContainsKey(light))
                {
                    state.RecoloredLights[light] = (light.color, light.intensity);
                    light.color = new Color(color.r, color.g, color.b);
                    light.intensity *= color.a;
                }
            }
        }

        private static void Restore(State state)
        {
            foreach (GameObject hidden in state.HiddenObjects)
                if (hidden) hidden.SetActive(true);
            foreach (Behaviour behaviour in state.DisabledBehaviours)
                if (behaviour) behaviour.enabled = true;
            foreach (ParticleSystem particles in state.StoppedParticles)
                if (particles) particles.Play(true);
            foreach (var pair in state.RecoloredParticles)
            {
                if (!pair.Key) continue;
                ParticleSystem.MainModule main = pair.Key.main;
                main.startColor = pair.Value;
                pair.Key.Clear(false);
            }
            foreach (var pair in state.RecoloredLights)
            {
                if (!pair.Key) continue;
                pair.Key.color = pair.Value.color;
                pair.Key.intensity = pair.Value.intensity;
            }
        }
    }
}
