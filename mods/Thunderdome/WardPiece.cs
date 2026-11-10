using System;
using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimMods.Shared;

namespace Thunderdome
{
    // The piece itself: a recoloured copy of the guard stone without its building protection, registered through Jotunn.
    internal static class WardPiece
    {
        private const string PrefabName = "Thunderdome";
        private const string GuardStone = "guard_stone";
        // Has the traders' dome material in a form that's always loaded (the real shield generator's dome is an image effect)
        private const string DomeSource = "charred_shieldgenerator";
        private const string DomeMaterial = "ForceField";

        // Blue, so a ward isn't mistaken for a guard stone's orange
        private static readonly Color s_tint = new Color(0.35f, 0.7f, 1f);

        private static Piece s_piece;

        // The incinerator's lightning strike, copied without its damage (that would hit the player's own buildings)
        private const string LightningName = "Thunderdome_Lightning";
        private const string LightningSource = "lightningAOE";

        // Null when the game no longer has it; wards then only show the ring while placing
        public static Material Dome { get; private set; }

        // Null when the game no longer has it; wards then don't strike
        public static GameObject Lightning { get; private set; }

        public static void Register()
        {
            LocalizationManager.Instance.GetLocalization().AddTranslation("English", new Dictionary<string, string>
            {
                ["piece_thunderdome"] = "Thunderdome",
                ["piece_thunderdome_description"] =
                    "Raises a force field like the traders' camps have. Hostile creatures that enter are struck by lightning.",
                ["piece_thunderdome_strike"] = "Strikes down monsters within",
                ["piece_thunderdome_repel"] = "Keeps monsters out within",
                ["piece_thunderdome_both"] = "Keeps out and strikes down monsters within",
            });
            PrefabManager.OnVanillaPrefabsAvailable += Create;
            PieceManager.OnPiecesRegistered += ApplyCost;
        }

        private static void Create()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            try
            {
                var config = new PieceConfig
                {
                    Name = "$piece_thunderdome",
                    Description = "$piece_thunderdome_description",
                    PieceTable = PieceTables.Hammer,
                    Category = PieceCategories.Misc,
                    CraftingStation = CraftingStations.Workbench,
                };
                // Jotunn wants these up front; ApplyCost replaces them with the configured cost once items are loaded
                foreach (KeyValuePair<string, int> entry in CostList.Parse(CostList.Default, new List<string>()))
                    config.AddRequirement(new RequirementConfig(entry.Key, entry.Value, 0, true));

                var custom = new CustomPiece(PrefabName, GuardStone, config);
                GameObject prefab = custom.PiecePrefab;
                s_piece = custom.Piece;

                // Keeps the guard stone's model, health, ring and its small PlayerBase area (no spawns right next to
                // it, like any base piece), but none of its permissions or its tutorial raven
                UnityEngine.Object.DestroyImmediate(prefab.GetComponent<PrivateArea>());
                foreach (GuidePoint guide in prefab.GetComponentsInChildren<GuidePoint>(true))
                    UnityEngine.Object.DestroyImmediate(guide.gameObject);
                prefab.AddComponent<Ward>();

                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) Tint(prefab);
                Dome = FindDomeMaterial();

                PieceManager.Instance.AddPiece(custom);
                Lightning = CreateLightning();
            }
            catch (Exception e)
            {
                ModInfo.Log.LogError($"Couldn't create the Thunderdome piece: {e}");
                PlayerNotice.Show($"{ModInfo.Name}: couldn't create the ward piece. Details are in BepInEx/LogOutput.log.");
            }
        }

        // Registered as a network prefab, so a strike one client spawns shows for everyone (every client has the mod,
        // so every client's copy is harmless too)
        private static GameObject CreateLightning()
        {
            if (!PrefabManager.Instance.GetPrefab(LightningSource))
            {
                ModInfo.Log.LogWarning($"{LightningSource} not found; wards won't strike");
                return null;
            }
            GameObject bolt = PrefabManager.Instance.CreateClonedPrefab(LightningName, LightningSource);
            foreach (Aoe aoe in bolt.GetComponentsInChildren<Aoe>(true))
            {
                if (aoe.gameObject == bolt) UnityEngine.Object.DestroyImmediate(aoe);
                else UnityEngine.Object.DestroyImmediate(aoe.gameObject);
            }
            PrefabManager.Instance.AddPrefab(bolt);
            return bolt;
        }

        private static Material FindDomeMaterial()
        {
            GameObject source = PrefabManager.Instance.GetPrefab(DomeSource);
            if (source)
            {
                foreach (MeshRenderer renderer in source.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.sharedMaterial && renderer.sharedMaterial.name == DomeMaterial) return renderer.sharedMaterial;
                }
            }
            ModInfo.Log.LogWarning($"Dome material not found on {DomeSource}; wards will show no dome");
            return null;
        }

        private static void Tint(GameObject prefab)
        {
            foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!materials[i] || !materials[i].name.Contains("Glow")) continue;
                    materials[i] = new Material(materials[i]);
                    materials[i].EnableKeyword("_EMISSION");
                    materials[i].SetColor("_EmissionColor", s_tint * 2f);
                }
                renderer.sharedMaterials = materials;
            }
            foreach (ParticleSystem particles in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                main.startColor = s_tint;
            }
            foreach (Light light in prefab.GetComponentsInChildren<Light>(true)) light.color = s_tint;
        }

        // Runs when items are loaded and whenever the setting changes. Keeps the current cost when the setting has no
        // usable entry, so a typo can't make wards free.
        public static void ApplyCost()
        {
            if (!s_piece || !ObjectDB.instance) return;
            var problems = new List<string>();
            var requirements = new List<Piece.Requirement>();
            foreach (KeyValuePair<string, int> entry in CostList.Parse(Settings.Cost.Value, problems))
            {
                GameObject item = ObjectDB.instance.GetItemPrefab(entry.Key);
                ItemDrop drop = item ? item.GetComponent<ItemDrop>() : null;
                if (!drop)
                {
                    problems.Add(entry.Key);
                    continue;
                }
                requirements.Add(new Piece.Requirement { m_resItem = drop, m_amount = entry.Value, m_recover = true });
            }
            if (problems.Count > 0) ModInfo.Log.LogWarning($"Cost setting: ignored {string.Join(", ", problems)}");
            if (requirements.Count > 0) s_piece.m_resources = requirements.ToArray();
        }
    }
}
