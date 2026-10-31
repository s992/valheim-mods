using System;
using System.Collections.Generic;
using System.Text;

namespace SearchEverything
{
    /// <summary>
    /// Text matching shared by every search. A query is split into words; something matches when every word appears
    /// somewhere in its search text (case-insensitive): its name, its type words and, for recipes, its ingredients.
    /// </summary>
    internal static class Matcher
    {
        private static readonly Dictionary<ItemDrop.ItemData.SharedData, string> s_itemText =
            new Dictionary<ItemDrop.ItemData.SharedData, string>();
        private static readonly Dictionary<Recipe, string> s_recipeText = new Dictionary<Recipe, string>();
        private static string s_language;

        public static string[] Parse(string query) =>
            string.IsNullOrWhiteSpace(query)
                ? Array.Empty<string>()
                : query.ToLowerInvariant().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

        public static bool Matches(ItemDrop.ItemData item, string[] words) =>
            words.Length == 0 || item?.m_shared == null || ContainsAll(ItemText(item.m_shared), words);

        public static bool Matches(Recipe recipe, string[] words) =>
            words.Length == 0 || !recipe || ContainsAll(RecipeText(recipe), words);

        /// <summary>How well a name matches, for sorting: 0 starts with the first word, 1 contains it, 2 matched elsewhere.</summary>
        public static int NameRank(string localizedName, string[] words)
        {
            if (words.Length == 0) return 0;
            string name = localizedName.ToLowerInvariant();
            if (name.StartsWith(words[0], StringComparison.Ordinal)) return 0;
            return name.Contains(words[0]) ? 1 : 2;
        }

        public static string Localize(string token) =>
            Localization.instance != null ? Localization.instance.Localize(token) : token;

        private static bool ContainsAll(string text, string[] words)
        {
            foreach (string word in words)
                if (text.IndexOf(word, StringComparison.Ordinal) < 0) return false;
            return true;
        }

        private static void CheckLanguage()
        {
            string language = Localization.instance != null ? Localization.instance.GetSelectedLanguage() : "";
            if (language == s_language) return;
            s_language = language;
            s_itemText.Clear();
            s_recipeText.Clear();
        }

        private static string ItemText(ItemDrop.ItemData.SharedData shared)
        {
            CheckLanguage();
            if (s_itemText.TryGetValue(shared, out string text)) return text;
            var builder = new StringBuilder();
            builder.Append(Localize(shared.m_name)).Append('\n');
            AppendTypeWords(builder, shared);
            text = builder.ToString().ToLowerInvariant();
            s_itemText[shared] = text;
            return text;
        }

        private static string RecipeText(Recipe recipe)
        {
            CheckLanguage();
            if (s_recipeText.TryGetValue(recipe, out string text)) return text;
            var builder = new StringBuilder();
            if (recipe.m_item) builder.Append(ItemText(recipe.m_item.m_itemData.m_shared));
            foreach (Piece.Requirement requirement in recipe.m_resources)
                if (requirement.m_resItem) builder.Append(Localize(requirement.m_resItem.m_itemData.m_shared.m_name).ToLowerInvariant()).Append('\n');
            text = builder.ToString();
            s_recipeText[recipe] = text;
            return text;
        }

        // English words, plus the game's own (localized) skill name for weapons and tools
        private static void AppendTypeWords(StringBuilder builder, ItemDrop.ItemData.SharedData shared)
        {
            void Add(string words) => builder.Append(words).Append('\n');
            switch (shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Material: Add("material"); break;
                case ItemDrop.ItemData.ItemType.Consumable:
                    Add(shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f ? "food" : "potion mead consumable");
                    break;
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Attach_Atgeir:
                    Add("weapon"); break;
                case ItemDrop.ItemData.ItemType.Shield: Add("shield"); break;
                case ItemDrop.ItemData.ItemType.Helmet: Add("helmet head armor armour"); break;
                case ItemDrop.ItemData.ItemType.Chest: Add("chest body armor armour"); break;
                case ItemDrop.ItemData.ItemType.Legs: Add("legs armor armour"); break;
                case ItemDrop.ItemData.ItemType.Hands: Add("hands gloves armor armour"); break;
                case ItemDrop.ItemData.ItemType.Shoulder: Add("cape shoulder armor armour"); break;
                case ItemDrop.ItemData.ItemType.Utility: Add("utility belt"); break;
                case ItemDrop.ItemData.ItemType.Trinket: Add("trinket"); break;
                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                    Add("ammo ammunition arrow bolt"); break;
                case ItemDrop.ItemData.ItemType.Torch: Add("torch"); break;
                case ItemDrop.ItemData.ItemType.Tool: Add("tool"); break;
                case ItemDrop.ItemData.ItemType.Trophy: Add("trophy"); break;
                case ItemDrop.ItemData.ItemType.Fish: Add("fish"); break;
                case ItemDrop.ItemData.ItemType.Customization: Add("customization"); break;
                case ItemDrop.ItemData.ItemType.Misc: Add("misc"); break;
            }

            bool usesSkill = shared.m_itemType != ItemDrop.ItemData.ItemType.Material &&
                             shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable &&
                             shared.m_skillType != Skills.SkillType.None;
            if (!usesSkill) return;
            Add(Localize("$skill_" + shared.m_skillType.ToString().ToLower()));
            switch (shared.m_skillType)
            {
                case Skills.SkillType.Swords: Add("sword"); break;
                case Skills.SkillType.Knives: Add("knife dagger"); break;
                case Skills.SkillType.Clubs: Add("club mace"); break;
                case Skills.SkillType.Polearms: Add("polearm atgeir"); break;
                case Skills.SkillType.Spears: Add("spear"); break;
                case Skills.SkillType.Axes: Add("axe"); break;
                case Skills.SkillType.Bows: Add("bow"); break;
                case Skills.SkillType.Crossbows: Add("crossbow"); break;
                case Skills.SkillType.ElementalMagic:
                case Skills.SkillType.BloodMagic:
                    Add("staff magic"); break;
                case Skills.SkillType.Pickaxes: Add("pickaxe"); break;
                case Skills.SkillType.Unarmed: Add("fists unarmed"); break;
            }
        }
    }
}
