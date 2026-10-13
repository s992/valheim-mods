using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LootFilter
{
    // Turns off particles, lights and Epic Loot's loot beam on filtered drops, keeping the model
    // and hover text. Everything it turns off is remembered so it can be turned back on exactly.
    internal sealed class DropEffects : MonoBehaviour
    {
        private const float ScanInterval = 0.25f;

        private sealed class State
        {
            public readonly List<Behaviour> DisabledBehaviours = new List<Behaviour>();
            public readonly List<ParticleSystem> StoppedParticles = new List<ParticleSystem>();
            public readonly List<GameObject> HiddenObjects = new List<GameObject>();
        }

        // Epic Loot adds a LootBeam component to magic drops, which spawns its beam object (_beam) in Update
        private static readonly Type LootBeamType = AccessTools.TypeByName("EpicLoot.LootBeams.LootBeam");
        private static readonly FieldInfo LootBeamObject = LootBeamType != null ? AccessTools.Field(LootBeamType, "_beam") : null;

        private readonly Dictionary<ItemDrop, State> _hidden = new Dictionary<ItemDrop, State>();
        private float _nextScan;
        private int _seenVersion = -1;
        private bool _wasRevealing;

        private void Update()
        {
            bool revealing = Input.Held(Settings.Reveal);
            bool dirty = revealing != _wasRevealing || _seenVersion != Rules.Version;
            if (!dirty && Time.time < _nextScan) return;

            _wasRevealing = revealing;
            _seenVersion = Rules.Version;
            _nextScan = Time.time + ScanInterval;
            Scan(revealing);
        }

        private void OnDestroy()
        {
            foreach (State state in _hidden.Values) Restore(state);
            _hidden.Clear();
        }

        private void Scan(bool revealing)
        {
            foreach (ItemDrop drop in ItemDrop.s_instances)
            {
                if (!drop) continue;

                // Items you dropped yourself (m_autoPickup off) are left alone
                bool hide = Settings.HideEffects.Value && !revealing && drop.m_autoPickup && Rules.IsFiltered(drop.m_itemData);
                bool isHidden = _hidden.TryGetValue(drop, out State state);

                if (hide)
                {
                    if (!isHidden) _hidden[drop] = state = new State();
                    // Run every scan: effects such as Epic Loot's beam can appear after the drop spawns
                    HideEffects(drop, state);
                }
                else if (isHidden)
                {
                    Restore(state);
                    _hidden.Remove(drop);
                }
            }

            // Forget drops that were picked up or despawned
            foreach (ItemDrop gone in _hidden.Keys.Where(d => !d).ToList())
                _hidden.Remove(gone);
        }

        private static void HideEffects(ItemDrop drop, State state)
        {
            // The beam first: once its object is inactive, its own particles and lights aren't found below
            if (LootBeamType != null && drop.GetComponent(LootBeamType) is Behaviour lootBeam)
            {
                if (lootBeam.enabled)
                {
                    lootBeam.enabled = false;
                    state.DisabledBehaviours.Add(lootBeam);
                }
                if (LootBeamObject?.GetValue(lootBeam) is GameObject beam && beam && beam.activeSelf)
                {
                    beam.SetActive(false);
                    state.HiddenObjects.Add(beam);
                }
            }

            foreach (ParticleSystem particles in drop.GetComponentsInChildren<ParticleSystem>())
            {
                if (particles.isPlaying)
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    state.StoppedParticles.Add(particles);
                }
            }

            foreach (Light light in drop.GetComponentsInChildren<Light>())
            {
                if (light.enabled)
                {
                    light.enabled = false;
                    state.DisabledBehaviours.Add(light);
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
        }
    }
}
