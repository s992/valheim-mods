using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimMods.Shared;

namespace LootFilter
{
    // Changes the sparkles and glow (particles and lights) of filtered drops lying on the ground:
    // hidden, recolored or left alone (FilteredSparkles). The model and hover text are kept.
    // Everything changed is remembered so it can be put back exactly.
    internal sealed class DropEffects : MonoBehaviour
    {
        private const float ScanInterval = 0.25f;

        private sealed class State
        {
            public readonly List<Behaviour> DisabledBehaviours = new List<Behaviour>();
            public readonly List<ParticleSystem> StoppedParticles = new List<ParticleSystem>();
            public readonly Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient> RecoloredParticles =
                new Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient>();
            public readonly Dictionary<Light, (Color color, float intensity)> RecoloredLights =
                new Dictionary<Light, (Color, float)>();
        }

        private readonly Dictionary<ItemDrop, State> _changed = new Dictionary<ItemDrop, State>();
        private float _nextScan;
        private int _seenVersion = -1;
        private bool _wasRevealing;
        private bool _effectSettingsChanged;
        private FrameGuard _guard;

        private void Awake()
        {
            _guard = new FrameGuard(this, "the filtered-drop effects");
            Settings.FilteredSparkles.SettingChanged += OnEffectSettingChanged;
            Settings.SparkleColor.SettingChanged += OnEffectSettingChanged;
        }

        private void OnEffectSettingChanged(object sender, EventArgs e) => _effectSettingsChanged = true;

        private void Update()
        {
            try { Tick(); }
            catch (Exception e) { _guard.Report(e); }
        }

        private void Tick()
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

        // Also runs when the guard switches this off, so no drop is left with a changed look
        private void OnDisable() => RestoreAll();

        private void OnDestroy() => RestoreAll();

        private void RestoreAll()
        {
            foreach (State state in _changed.Values) Restore(state);
            _changed.Clear();
        }

        private void Scan(bool revealing)
        {
            bool anyEffect = Settings.FilteredSparkles.Value != SparkleMode.Unchanged;

            foreach (ItemDrop drop in ItemDrop.s_instances)
            {
                if (!drop) continue;

                // Includes items you dropped yourself: vanilla won't auto-pick those up, but they should still look filtered
                bool change = anyEffect && !revealing && !Rules.Paused && Rules.IsFiltered(drop.m_itemData);
                bool isChanged = _changed.TryGetValue(drop, out State state);

                if (change)
                {
                    if (!isChanged) _changed[drop] = state = new State();
                    // Run every scan: effects can start after the drop spawns
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
            bool hide = Settings.FilteredSparkles.Value == SparkleMode.Hidden;
            Color color = Settings.SparkleColor.Value;
            if (color.a <= 0f) hide = true;

            foreach (ParticleSystem particles in drop.GetComponentsInChildren<ParticleSystem>())
            {
                if (hide)
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
                if (hide)
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
