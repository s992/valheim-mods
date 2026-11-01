using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimMods.Shared;

namespace UtilityBelt
{
    // The item itself: a recoloured copy of Megingjord, registered through Jotunn, with an upgrade recipe at the
    // workbench whose costs Patches.cs works out per belt.
    internal static class Belt
    {
        public const string PrefabName = "UtilityBelt";
        private const string Megingjord = "BeltStrength";
        private const string SharedName = "$item_utilitybelt";
        public const float CarryWeight = 150f;

        // A cool blue, so it reads as a belt but isn't mistaken for Megingjord's brown and gold
        private static readonly Color s_tint = new Color(0.55f, 0.78f, 1f);

        // +150 carry weight from the base belt, with the belt's icon in the status bar
        private static StatusEffect s_baseEffect;
        // Tier 6's +150 more; no icon, so the status bar still shows one belt
        private static StatusEffect s_extraCarryEffect;
        private static Recipe s_recipe;

        public static ItemDrop Prefab { get; private set; }

        public static void Register()
        {
            LocalizationManager.Instance.GetLocalization().AddTranslation("English", new Dictionary<string, string>
            {
                ["item_utilitybelt"] = "Utility Belt",
                ["item_utilitybelt_description"] =
                    "A sturdy belt with a pouch for everything. Upgrade it at a workbench to carry the gear you'd " +
                    "otherwise have to swap between.",
                ["se_utilitybelt"] = "Utility Belt",
            });
            PrefabManager.OnVanillaPrefabsAvailable += Create;
        }

        private static void Create()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            try
            {
                var config = new ItemConfig
                {
                    Name = SharedName,
                    Description = SharedName + "_description",
                    CraftingStation = CraftingStations.Workbench,
                    MinStationLevel = 1,
                };
                // Every amount is 0 here; Patches.RequirementAmount gives the real cost for the belt being upgraded
                config.AddRequirement(new RequirementConfig(Tiers.Coins, 0, 0, false));
                foreach (string item in Tiers.All.Select(Tiers.UpgradeItem).Where(i => i != null))
                    config.AddRequirement(new RequirementConfig(item, 0, 0, false));

                var belt = new CustomItem(PrefabName, Megingjord, config);
                ItemDrop.ItemData.SharedData shared = belt.ItemDrop.m_itemData.m_shared;
                shared.m_maxQuality = Tiers.MaxQuality;
                // Bought from Haldor, never crafted: the recipe is only for upgrades
                s_recipe = belt.Recipe.Recipe;
                s_recipe.m_noCraftOnlyUpgrade = true;

                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) Tint(belt.ItemPrefab, shared);
                s_baseEffect = CarryEffect(shared.m_equipStatusEffect, "SE_UtilityBelt", shared.m_icons.FirstOrDefault());
                s_extraCarryEffect = CarryEffect(shared.m_equipStatusEffect, "SE_UtilityBeltReinforced", null);
                shared.m_equipStatusEffect = s_baseEffect;

                ItemManager.Instance.AddItem(belt);
                Prefab = belt.ItemDrop;
            }
            catch (Exception e)
            {
                ModInfo.Log.LogError($"Couldn't create the utility belt: {e}");
                PlayerNotice.Show($"{ModInfo.Name}: couldn't create the belt item. Details are in BepInEx/LogOutput.log.");
            }
        }

        // A copy of Megingjord's effect (whose only stat is the carry weight) under the belt's own name
        private static SE_Stats CarryEffect(StatusEffect megingjord, string name, Sprite icon)
        {
            SE_Stats effect = megingjord is SE_Stats stats
                ? UnityEngine.Object.Instantiate(stats)
                : ScriptableObject.CreateInstance<SE_Stats>();
            effect.name = name;
            effect.m_name = "$se_utilitybelt";
            effect.m_tooltip = "";
            effect.m_icon = icon;
            effect.m_addMaxCarryWeight = CarryWeight;
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(effect, false));
            return effect;
        }

        private static void Tint(GameObject prefab, ItemDrop.ItemData.SharedData shared)
        {
            // Copies of the materials: the originals are shared with Megingjord
            var copies = new Dictionary<Material, Material>();
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material original = materials[i];
                    if (!original || !original.HasProperty("_Color")) continue;
                    if (!copies.TryGetValue(original, out Material copy))
                    {
                        copy = new Material(original) { name = original.name + "_UtilityBelt" };
                        copy.color = original.color * s_tint;
                        copies[original] = copy;
                    }
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
            shared.m_icons = shared.m_icons.Select(TintSprite).ToArray();
        }

        // Icons sit in a texture atlas that can't be read directly: draw it to a render texture and read that back
        private static Sprite TintSprite(Sprite sprite)
        {
            if (!sprite) return sprite;
            Rect rect = sprite.textureRect;
            Texture source = sprite.texture;
            RenderTexture target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                var texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false)
                {
                    name = "UtilityBeltIcon",
                };
                texture.ReadPixels(rect, 0, 0);
                Color[] pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color p = pixels[i];
                    pixels[i] = new Color(p.r * s_tint.r, p.g * s_tint.g, p.b * s_tint.b, p.a);
                }
                texture.SetPixels(pixels);
                texture.Apply();
                return Sprite.Create(texture, new Rect(0, 0, rect.width, rect.height), new Vector2(0.5f, 0.5f),
                    sprite.pixelsPerUnit);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        public static bool IsBelt(ItemDrop.ItemData item) => item != null && item.m_shared.m_name == SharedName;

        public static bool IsBeltRecipe(Recipe recipe) => recipe && recipe == s_recipe;

        public static bool IsBeltRequirement(Piece.Requirement requirement) =>
            s_recipe && Array.IndexOf(s_recipe.m_resources, requirement) >= 0;

        // Read every frame (armour, carry weight), so the last result is kept
        private static string s_ownedText;
        private static int s_ownedQuality;
        private static HashSet<Tier> s_owned;

        public static HashSet<Tier> Owned(ItemDrop.ItemData item)
        {
            item.m_customData.TryGetValue(Tiers.DataKey, out string text);
            if (s_owned == null || text != s_ownedText || item.m_quality != s_ownedQuality)
            {
                s_owned = Tiers.Owned(item.m_customData, item.m_quality);
                s_ownedText = text;
                s_ownedQuality = item.m_quality;
            }
            return s_owned;
        }

        /// <summary>Whether a character wears a belt with this upgrade working (bought and turned on by the server).</summary>
        public static bool HasActive(Humanoid character, Tier tier)
        {
            ItemDrop.ItemData item = character ? character.m_utilityItem : null;
            return IsBelt(item) && Settings.Rules.IsActive(Owned(item), tier);
        }

        /// <summary>The status effect an upgrade adds while worn, or null for upgrades that work another way.</summary>
        public static StatusEffect EffectFor(Tier tier) => tier switch
        {
            Tier.Wishbone => EquipEffectOf("Wishbone"),
            Tier.Wisplight => EquipEffectOf("Demister"),
            Tier.FeatherFall => EquipEffectOf("CapeFeather"),
            Tier.ExtraCarry => s_extraCarryEffect,
            _ => null,
        };

        public static ItemDrop.ItemData.SharedData SharedOf(string prefabName)
        {
            GameObject prefab = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
            ItemDrop drop = prefab ? prefab.GetComponent<ItemDrop>() : null;
            return drop ? drop.m_itemData.m_shared : null;
        }

        private static StatusEffect EquipEffectOf(string prefabName) => SharedOf(prefabName)?.m_equipStatusEffect;
    }
}
