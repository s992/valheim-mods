using System;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using LootFilter;
using Xunit;

namespace ValheimMods.Tests
{
    public class LootFilterGroupTests
    {
        private static ItemDrop.ItemData Item(ItemDrop.ItemData.ItemType type, string name = "$item_test") =>
            new ItemDrop.ItemData { m_shared = new ItemDrop.ItemData.SharedData { m_itemType = type, m_name = name } };

        // Item types that land in Misc on purpose. A game update that adds an item type fails this test, so someone
        // decides which group it belongs in instead of it quietly becoming Misc.
        private static readonly ItemDrop.ItemData.ItemType[] KnownMisc =
        {
            ItemDrop.ItemData.ItemType.None,
            ItemDrop.ItemData.ItemType.Misc,
            ItemDrop.ItemData.ItemType.Customization,
        };

        [Fact]
        public void EveryItemType_HasADeliberateGroup()
        {
            var misc = Enum.GetValues(typeof(ItemDrop.ItemData.ItemType)).Cast<ItemDrop.ItemData.ItemType>()
                .Where(type => Groups.Of(Item(type)) == ItemGroups.Misc)
                .OrderBy(type => type).ToArray();

            Assert.Equal(KnownMisc.OrderBy(type => type).ToArray(), misc);
        }

        [Theory]
        [InlineData(ItemDrop.ItemData.ItemType.Material, ItemGroups.Materials)]
        [InlineData(ItemDrop.ItemData.ItemType.Consumable, ItemGroups.FoodAndPotions)]
        [InlineData(ItemDrop.ItemData.ItemType.Bow, ItemGroups.Weapons)]
        [InlineData(ItemDrop.ItemData.ItemType.Shoulder, ItemGroups.Armor)]
        [InlineData(ItemDrop.ItemData.ItemType.AmmoNonEquipable, ItemGroups.Ammo)]
        [InlineData(ItemDrop.ItemData.ItemType.Torch, ItemGroups.ToolsAndTorches)]
        [InlineData(ItemDrop.ItemData.ItemType.Trinket, ItemGroups.UtilityAndTrinkets)]
        public void Of_GroupsLikeTheSettingsDescribe(ItemDrop.ItemData.ItemType type, ItemGroups expected) =>
            Assert.Equal(expected, Groups.Of(Item(type)));

        [Fact]
        public void EveryGroup_HasAName()
        {
            foreach (ItemGroups group in Enum.GetValues(typeof(ItemGroups)).Cast<ItemGroups>().Where(g => g != ItemGroups.None))
                Assert.False(string.IsNullOrWhiteSpace(Groups.Describe(group)), group.ToString());
        }
    }

    // LootFilter.Settings (qualified: the game has a global Settings class) and Rules are static, so these run in one class against one temporary config file
    public sealed class LootFilterRulesTests : IDisposable
    {
        private readonly string m_configPath = Path.Combine(Path.GetTempPath(), $"lootfilter-test-{Guid.NewGuid():N}.cfg");

        public LootFilterRulesTests()
        {
            LootFilter.Settings.Bind(new ConfigFile(m_configPath, true));
            Rules.Init();
            Rules.Paused = false;
        }

        public void Dispose() => File.Delete(m_configPath);

        private static ItemDrop.ItemData Item(string name, ItemDrop.ItemData.ItemType type = ItemDrop.ItemData.ItemType.Material) =>
            new ItemDrop.ItemData { m_shared = new ItemDrop.ItemData.SharedData { m_itemType = type, m_name = name } };

        [Fact]
        public void ToggleItem_FiltersThenUnfilters_AndSavesASortedList()
        {
            LootFilter.Settings.FilteredItems.Value = "Wood";

            Assert.True(Rules.ToggleItem(Item("Stone")));
            Assert.Equal("Stone, Wood", LootFilter.Settings.FilteredItems.Value);
            Assert.Equal(FilterReason.Item, Rules.Explain(Item("stone")));

            Assert.False(Rules.ToggleItem(Item("STONE")));
            Assert.Equal("Wood", LootFilter.Settings.FilteredItems.Value);
            Assert.Equal(FilterReason.NotFiltered, Rules.Explain(Item("Stone")));
        }

        [Fact]
        public void Explain_PrefersTheItemRuleOverItsGroup()
        {
            LootFilter.Settings.FilteredItems.Value = "Resin";
            LootFilter.Settings.FilteredGroups.Value = ItemGroups.Materials;

            Assert.Equal(FilterReason.Item, Rules.Explain(Item("Resin")));
            Assert.Equal(FilterReason.Group, Rules.Explain(Item("Stone")));
            Assert.Equal(FilterReason.NotFiltered, Rules.Explain(Item("Bread", ItemDrop.ItemData.ItemType.Consumable)));
        }

        [Fact]
        public void Pause_StopsBlockingPickupAndBumpsTheVersion()
        {
            LootFilter.Settings.FilteredItems.Value = "Stone";
            LootFilter.Settings.BlockAutoPickup.Value = true;
            int version = Rules.Version;

            Rules.Paused = true;

            Assert.False(Rules.BlocksAutoPickup(Item("Stone")));
            Assert.NotEqual(version, Rules.Version);
            Rules.Paused = false;
            Assert.True(Rules.BlocksAutoPickup(Item("Stone")));
        }
    }
}
