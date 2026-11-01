using System;
using System.Linq;
using SearchEverything;
using Xunit;

namespace ValheimMods.Tests
{
    public class SearchEverythingMatcherTests
    {
        // Stands in for Localization: "$item_x" becomes "X item", skill tokens stay as they are
        private static string Localize(string token) =>
            token.StartsWith("$item_", StringComparison.Ordinal) ? string.Concat(token.AsSpan(6), " item") : token;

        private static ItemDrop.ItemData.SharedData Shared(ItemDrop.ItemData.ItemType type, string name = "$item_Test",
            Skills.SkillType skill = Skills.SkillType.None) =>
            new ItemDrop.ItemData.SharedData { m_itemType = type, m_name = name, m_skillType = skill };

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Parse_EmptyQuery_HasNoWords(string query) => Assert.Empty(Matcher.Parse(query));

        [Fact]
        public void Parse_SplitsOnWhitespace_AndLowercases() =>
            Assert.Equal(new[] { "iron", "sword" }, Matcher.Parse("  Iron\tSWORD "));

        [Theory]
        [InlineData("Iron sword", "iron", 0)]
        [InlineData("Black metal sword", "metal", 1)]
        [InlineData("Black metal sword", "weapon", 2)]
        [InlineData("Anything", null, 0)]
        public void NameRank_PrefersNamesStartingWithTheFirstWord(string name, string word, int expected) =>
            Assert.Equal(expected, Matcher.NameRank(name, word == null ? Array.Empty<string>() : new[] { word }));

        // A game update that adds an item type fails this test, so someone gives it search words
        [Fact]
        public void EveryItemType_HasTypeWords()
        {
            var without = Enum.GetValues(typeof(ItemDrop.ItemData.ItemType)).Cast<ItemDrop.ItemData.ItemType>()
                .Where(type => type != ItemDrop.ItemData.ItemType.None)
                .Where(type => Matcher.BuildItemText(Shared(type, ""), Localize).Trim().Length == 0)
                .ToArray();

            Assert.Empty(without);
        }

        [Fact]
        public void ItemText_HasNameTypeAndSkillWords()
        {
            string text = Matcher.BuildItemText(
                Shared(ItemDrop.ItemData.ItemType.OneHandedWeapon, "$item_SwordIron", Skills.SkillType.Swords), Localize);

            Assert.Contains("swordiron item", text);
            Assert.Contains("weapon", text);
            Assert.Contains("$skill_swords", text);
            Assert.Contains("sword", text);
            Assert.Equal(text.ToLowerInvariant(), text);
        }

        [Fact]
        public void Food_And_Potions_GetDifferentWords()
        {
            var food = Shared(ItemDrop.ItemData.ItemType.Consumable);
            food.m_food = 20f;
            Assert.Contains("food", Matcher.BuildItemText(food, Localize));
            Assert.Contains("potion", Matcher.BuildItemText(Shared(ItemDrop.ItemData.ItemType.Consumable), Localize));
        }

        [Fact]
        public void EveryWord_MustMatch()
        {
            string text = Matcher.BuildItemText(Shared(ItemDrop.ItemData.ItemType.Helmet, "$item_Bronze"), Localize);

            Assert.True(Matcher.ContainsAll(text, Matcher.Parse("bronze armor")));
            Assert.True(Matcher.ContainsAll(text, Matcher.Parse("HELM bro")));
            Assert.False(Matcher.ContainsAll(text, Matcher.Parse("bronze sword")));
        }
    }
}
