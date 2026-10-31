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
}
