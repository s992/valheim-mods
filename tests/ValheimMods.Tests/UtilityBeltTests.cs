using System.Collections.Generic;
using System.Linq;
using UtilityBelt;
using Xunit;

namespace ValheimMods.Tests
{
    public class UtilityBeltTests
    {
        private static TierRules Rules(params Tier[] disabled) => new TierRules(
            Tiers.All.Except(disabled), Tiers.All.ToDictionary(t => t, TierRules.DefaultGold));

        private static HashSet<Tier> Set(params Tier[] tiers) => new HashSet<Tier>(tiers);

        [Fact]
        public void MaxQuality_IsTheBaseBeltPlusEveryTier() => Assert.Equal(7, Tiers.MaxQuality);

        [Fact]
        public void Next_GoesInOrder()
        {
            TierRules rules = Rules();
            Assert.Equal(Tier.SwampKey, rules.Next(Set()));
            Assert.Equal(Tier.Wishbone, rules.Next(Set(Tier.SwampKey)));
            Assert.Equal(Tier.ExtraCarry, rules.Next(Set(Tier.SwampKey, Tier.Wishbone, Tier.FrostResist,
                Tier.Wisplight, Tier.FeatherFall)));
            Assert.Null(rules.Next(Set(Tiers.All)));
        }

        [Fact]
        public void Next_SkipsDisabledTiers()
        {
            TierRules rules = Rules(Tier.Wishbone, Tier.FrostResist);
            Assert.Equal(Tier.Wisplight, rules.Next(Set(Tier.SwampKey)));
        }

        [Fact]
        public void Next_OffersATierTurnedBackOnThatTheBeltSkipped()
        {
            // Upgraded while the wishbone tier was off, so it went from the swamp key straight to frost resistance
            HashSet<Tier> owned = Set(Tier.SwampKey, Tier.FrostResist);
            Assert.Equal(Tier.Wishbone, Rules().Next(owned));
        }

        [Fact]
        public void Next_IsNullWhenEveryRemainingTierIsOff() =>
            Assert.Null(Rules(Tier.FeatherFall, Tier.ExtraCarry).Next(
                Set(Tier.SwampKey, Tier.Wishbone, Tier.FrostResist, Tier.Wisplight)));

        [Fact]
        public void IsActive_NeedsTheTierBoughtAndOn()
        {
            HashSet<Tier> owned = Set(Tier.SwampKey, Tier.Wishbone);
            TierRules rules = Rules(Tier.Wishbone);
            Assert.True(rules.IsActive(owned, Tier.SwampKey));
            Assert.False(rules.IsActive(owned, Tier.Wishbone));
            Assert.False(rules.IsActive(owned, Tier.FrostResist));
        }

        [Theory]
        [InlineData(1, "CryptKey", 1)]
        [InlineData(1, "Coins", 0)]
        [InlineData(1, "Wishbone", 0)]
        [InlineData(3, "CapeWolf", 1)]
        [InlineData(4, "Demister", 1)]
        [InlineData(5, "CapeFeather", 1)]
        [InlineData(6, "Coins", 1500)]
        [InlineData(6, "CapeFeather", 0)]
        // Tier numbers: the enum is internal, and a public test method can't take it
        public void Amount_DefaultCosts(int tier, string prefab, int expected) =>
            Assert.Equal(expected, Rules().Amount((Tier)tier, prefab));

        [Fact]
        public void Amount_UsesTheConfiguredGold()
        {
            var rules = new TierRules(Tiers.All, new Dictionary<Tier, int> { [Tier.Wishbone] = 250, [Tier.SwampKey] = -5 });
            Assert.Equal(250, rules.Amount(Tier.Wishbone, Tiers.Coins));
            Assert.Equal(0, rules.Amount(Tier.SwampKey, Tiers.Coins));
            Assert.Equal(0, rules.Amount(Tier.ExtraCarry, Tiers.Coins));
        }

        [Fact]
        public void Owned_ReadsTheRecord()
        {
            var data = new Dictionary<string, string> { [Tiers.DataKey] = "1,3" };
            Assert.Equal(Set(Tier.SwampKey, Tier.FrostResist), Tiers.Owned(data, 3));
        }

        [Fact]
        public void Owned_WithoutARecordComesFromQuality()
        {
            Assert.Empty(Tiers.Owned(new Dictionary<string, string>(), 1));
            Assert.Equal(Set(Tier.SwampKey, Tier.Wishbone), Tiers.Owned(null, 3));
            Assert.Equal(Set(Tiers.All), Tiers.Owned(null, 7));
        }

        [Fact]
        public void Parse_IgnoresJunkAndUnknownTiers() =>
            Assert.Equal(Set(Tier.SwampKey, Tier.ExtraCarry), Tiers.Parse(" 1, x, 9, 6,,0"));

        [Fact]
        public void Format_RoundTripsInOrder()
        {
            string text = Tiers.Format(new[] { Tier.ExtraCarry, Tier.SwampKey, Tier.SwampKey });
            Assert.Equal("1,6", text);
            Assert.Equal(Set(Tier.SwampKey, Tier.ExtraCarry), Tiers.Parse(text));
        }

        [Fact]
        public void Cost_NamesItemAndGold()
        {
            TierRules rules = Rules();
            Assert.Equal("1 swamp key", rules.Cost(Tier.SwampKey));
            Assert.Equal("1500 gold", rules.Cost(Tier.ExtraCarry));
            var priced = new TierRules(Tiers.All, new Dictionary<Tier, int> { [Tier.Wisplight] = 100 });
            Assert.Equal("1 wisplight and 100 gold", priced.Cost(Tier.Wisplight));
        }

        [Fact]
        public void Describe_ShowsOffTiersAndTheNextUpgrade()
        {
            string text = Rules(Tier.Wishbone).Describe(Set(Tier.SwampKey, Tier.Wishbone, Tier.FrostResist),
                tier => tier != Tier.FrostResist);
            Assert.Contains("Swamp key: opens sunken crypts", text);
            Assert.Contains("Wishbone: turned off on this server", text);
            Assert.Contains("Frost resistance: off in your settings", text);
            Assert.Contains("Next at a workbench: <color=orange>Wisplight</color> (1 wisplight)", text);
        }

        [Fact]
        public void Describe_FullyUpgraded()
        {
            string text = Rules().Describe(Set(Tiers.All), _ => true);
            Assert.Contains("Fully upgraded", text);
            Assert.DoesNotContain("none yet", text);
        }
    }
}
