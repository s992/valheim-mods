using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EverythingsANail;
using Xunit;

namespace ValheimMods.Tests
{
    public class EverythingsANailRulesTests
    {
        [Fact]
        public void ParseList_TrimsSplitsOnCommaAndSemicolon_IgnoresCaseAndEmptyEntries()
        {
            var names = Rules.ParseList(" AxeBronze, ;FishingRod;; axebronze ,");

            Assert.Equal(2, names.Count);
            Assert.Contains("AXEBRONZE", names);
            Assert.Contains("fishingrod", names);
        }

        [Fact]
        public void ParseList_HandlesNull() => Assert.Empty(Rules.ParseList(null));

        private static ToolInfo Tool(string prefab, ToolCategories category) =>
            new ToolInfo { Prefab = prefab, Category = category };

        private static Rules RulesWith(ToolCategories categories, string onlyAllow = "", string exclude = "") =>
            new Rules { Categories = categories, OnlyAllow = Rules.ParseList(onlyAllow), Exclude = Rules.ParseList(exclude) };

        [Fact]
        public void Hammer_IsAlwaysAllowed()
        {
            var rules = RulesWith(0, onlyAllow: "Hoe", exclude: ToolCatalog.HammerPrefab);
            Assert.True(ToolCatalog.IsAllowed(Tool(ToolCatalog.HammerPrefab, ToolCategories.Building), rules));
        }

        [Fact]
        public void Categories_DecideWhenOnlyAllowIsEmpty()
        {
            var rules = RulesWith(ToolCategories.Building | ToolCategories.Fishing);
            Assert.True(ToolCatalog.IsAllowed(Tool("Hoe", ToolCategories.Building), rules));
            Assert.True(ToolCatalog.IsAllowed(Tool("FishingRod", ToolCategories.Fishing), rules));
            Assert.False(ToolCatalog.IsAllowed(Tool("PickaxeIron", ToolCategories.Mining), rules));
        }

        [Fact]
        public void OnlyAllow_OverridesCategories()
        {
            var rules = RulesWith(ToolCategories.Building, onlyAllow: "pickaxeiron");
            Assert.True(ToolCatalog.IsAllowed(Tool("PickaxeIron", ToolCategories.Mining), rules));
            Assert.False(ToolCatalog.IsAllowed(Tool("Hoe", ToolCategories.Building), rules));
        }

        [Fact]
        public void Exclude_WinsOverOnlyAllowAndCategories()
        {
            var rules = RulesWith(ToolCategories.Building, onlyAllow: "Hoe, Cultivator", exclude: "Hoe");
            Assert.False(ToolCatalog.IsAllowed(Tool("Hoe", ToolCategories.Building), rules));
            Assert.True(ToolCatalog.IsAllowed(Tool("Cultivator", ToolCategories.Building), rules));
        }
    }

    // The server sends its rules to every client; both sides must agree on the format byte for byte
    public class EverythingsANailConfigSyncTests
    {
        private static ZPackage Received(ZPackage sent) => new ZPackage(sent.GetArray());

        [Fact]
        public void Rules_RoundTrip()
        {
            var rules = new Rules
            {
                Categories = ToolCategories.Building | ToolCategories.Mining,
                OnlyAllow = Rules.ParseList("Hoe, PickaxeIron"),
                Exclude = Rules.ParseList("AxeBronze"),
            };

            Rules read = ConfigSync.TryRead(Received(ConfigSync.Write(rules)), out string problem);

            Assert.Null(problem);
            Assert.NotNull(read);
            Assert.Equal(rules.Categories, read.Categories);
            Assert.Equal(rules.OnlyAllow, read.OnlyAllow);
            Assert.Equal(rules.Exclude, read.Exclude);
        }

        [Fact]
        public void OtherFormat_IsRejectedWithAReason()
        {
            var pkg = new ZPackage();
            pkg.Write(99);
            pkg.Write(0);

            Assert.Null(ConfigSync.TryRead(Received(pkg), out string problem));
            Assert.Contains("format 99", problem);
        }

        [Fact]
        public void CutShortPackage_IsRejectedInsteadOfThrowing()
        {
            var full = ConfigSync.Write(new Rules { Categories = ToolCategories.Building });
            byte[] bytes = full.GetArray();
            var cut = new ZPackage(bytes[..6]);

            Assert.Null(ConfigSync.TryRead(cut, out string problem));
            Assert.NotNull(problem);
        }
    }

    // Tools are recognised by what they do, so other mods' tools join the menu without a list to maintain. Build tools
    // and harvesting attacks are recognised through Unity objects (PieceTable, Attack) that can't be made outside the
    // game, so the hoe, cultivator and scythe are left to the in-game test list.
    public class EverythingsANailClassifyTests
    {
        private static ItemDrop.ItemData.SharedData Item(Skills.SkillType skill = Skills.SkillType.None,
            ItemDrop.ItemData.ItemType type = ItemDrop.ItemData.ItemType.OneHandedWeapon, HitData.DamageTypes damage = default,
            int tier = 0) =>
            new ItemDrop.ItemData.SharedData { m_name = "$item", m_skillType = skill, m_itemType = type, m_damages = damage, m_toolTier = tier };

        [Fact]
        public void OneHandedChoppingAxe_IsLogging_BattleAxeIsNot()
        {
            var chop = new HitData.DamageTypes { m_chop = 30f };

            Assert.Equal(ToolCategories.Logging, ToolCatalog.Classify("AxeBronze", Item(Skills.SkillType.Axes, damage: chop)).Category);
            Assert.Null(ToolCatalog.Classify("Battleaxe",
                Item(Skills.SkillType.Axes, ItemDrop.ItemData.ItemType.TwoHandedWeapon, chop)));
        }

        [Fact]
        public void BetterTier_RanksHigherThanMoreDamage()
        {
            ToolInfo bronze = ToolCatalog.Classify("PickaxeBronze",
                Item(Skills.SkillType.Pickaxes, damage: new HitData.DamageTypes { m_pickaxe = 90f }, tier: 2));
            ToolInfo iron = ToolCatalog.Classify("PickaxeIron",
                Item(Skills.SkillType.Pickaxes, damage: new HitData.DamageTypes { m_pickaxe = 40f }, tier: 3));

            Assert.Equal(ToolCategories.Mining, iron.Category);
            Assert.True(iron.Rank > bronze.Rank);
        }

        [Fact]
        public void ButcherKnife_AndTamedOnlyItems_AreButchering()
        {
            Assert.Equal(ToolCategories.Butchering, ToolCatalog.Classify("KnifeButcher", Item(Skills.SkillType.Knives)).Category);
            ItemDrop.ItemData.SharedData modded = Item(Skills.SkillType.Knives);
            modded.m_tamedOnly = true;
            Assert.Equal(ToolCategories.Butchering, ToolCatalog.Classify("ModdedCleaver", modded).Category);
        }

        [Fact]
        public void FishingRod_IsRecognisedByItsAnimation()
        {
            ItemDrop.ItemData.SharedData rod = Item(type: ItemDrop.ItemData.ItemType.TwoHandedWeapon);
            rod.m_animationState = ItemDrop.ItemData.AnimationState.FishingRod;

            Assert.Equal(ToolCategories.Fishing, ToolCatalog.Classify("FishingRod", rod).Category);
        }

        [Fact]
        public void ServingTray_AndPlainWeapons_AreNotTools()
        {
            ItemDrop.ItemData.SharedData tray = Item();
            tray.m_animationState = ItemDrop.ItemData.AnimationState.Feaster;

            Assert.Null(ToolCatalog.Classify("ServingTray", tray));
            Assert.Null(ToolCatalog.Classify("SwordIron", Item(Skills.SkillType.Swords, damage: new HitData.DamageTypes { m_slash = 50f })));
        }

        // Attack's constructor calls into Unity (AnimationCurve), so build one without running it
        private static Attack SnowAttack(bool strong)
        {
            var attack = (Attack)RuntimeHelpers.GetUninitializedObject(typeof(Attack));
            attack.m_snowShovel = true;
            attack.m_snowShovelStrong = strong;
            attack.m_snowShovelStrength = 1f;
            return attack;
        }

        [Fact]
        public void SnowShovel_IsRecognisedByItsStrongSnowAttack_EvenWithPickaxeDamage()
        {
            ItemDrop.ItemData.SharedData shovel = Item(Skills.SkillType.Pickaxes, damage: new HitData.DamageTypes { m_pickaxe = 10f });
            shovel.m_attack = SnowAttack(strong: true);

            ToolInfo tool = ToolCatalog.Classify("SnowShovel", shovel);

            Assert.Equal(ToolCategories.Snow, tool.Category);
            Assert.Equal("Snow", tool.Group);
        }

        [Fact]
        public void OrdinarySnowClearingAttacks_DontMakeATool()
        {
            // Every attack clears a little snow by default; only the strong (deep snow) one is the shovel's
            ItemDrop.ItemData.SharedData pickaxe = Item(Skills.SkillType.Pickaxes, damage: new HitData.DamageTypes { m_pickaxe = 40f });
            pickaxe.m_attack = SnowAttack(strong: false);
            ItemDrop.ItemData.SharedData sword = Item(Skills.SkillType.Swords, damage: new HitData.DamageTypes { m_slash = 50f });
            sword.m_attack = SnowAttack(strong: false);

            Assert.Equal(ToolCategories.Mining, ToolCatalog.Classify("PickaxeIron", pickaxe).Category);
            Assert.Null(ToolCatalog.Classify("SwordIron", sword));
        }
    }

    public class EverythingsANailToolLevelsTests
    {
        [Fact]
        public void Remember_KeepsTheBestLevel()
        {
            var data = new Dictionary<string, string>();

            Assert.True(ToolLevels.Remember(data, "Hoe", 4));
            Assert.False(ToolLevels.Remember(data, "Hoe", 3));
            Assert.True(ToolLevels.Remember(data, "Hoe", 6));

            Assert.Equal(6, ToolLevels.Get(data, "Hoe"));
            Assert.Equal(0, ToolLevels.Get(data, "Cultivator"));
        }

        [Fact]
        public void LevelOne_IsNotStored()
        {
            var data = new Dictionary<string, string>();

            Assert.False(ToolLevels.Remember(data, "Hoe", 1));
            Assert.Empty(data);
        }

        [Fact]
        public void LevelFor_IsAtLeastOne_AndCappedByTheToolsCurrentMaximum()
        {
            var data = new Dictionary<string, string>();
            Assert.Equal(1, ToolLevels.LevelFor(data, "Hoe", 6));

            ToolLevels.Remember(data, "Hoe", 6);
            Assert.Equal(6, ToolLevels.LevelFor(data, "Hoe", 6));
            // Voxheim's level count lowered after the hoe was upgraded
            Assert.Equal(4, ToolLevels.LevelFor(data, "Hoe", 4));
        }

        [Fact]
        public void Get_IgnoresDataThatIsntALevel()
        {
            var data = new Dictionary<string, string> { [ToolLevels.KeyPrefix + "Hoe"] = "six" };

            Assert.Equal(0, ToolLevels.Get(data, "Hoe"));
            Assert.True(ToolLevels.Remember(data, "Hoe", 2));
            Assert.Equal(2, ToolLevels.Get(data, "Hoe"));
        }
    }
}
