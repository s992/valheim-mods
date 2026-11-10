using System.Collections.Generic;
using System.Linq;
using Thunderdome;
using Xunit;

namespace ValheimMods.Tests
{
    public class ThunderdomeTests
    {
        [Fact]
        public void DefaultCostParsesWithNoProblems()
        {
            var problems = new List<string>();
            var cost = CostList.Parse(CostList.Default, problems);
            Assert.Empty(problems);
            Assert.Equal(new[] { "SurtlingCore", "FineWood", "ElderBark", "GreydwarfEye" }, cost.Select(c => c.Key));
            Assert.Equal(new[] { 5, 10, 10, 10 }, cost.Select(c => c.Value));
        }

        [Fact]
        public void SpacesAndEmptyEntriesAreIgnored()
        {
            var problems = new List<string>();
            var cost = CostList.Parse(" Stone : 3 ,, Wood:2, ", problems);
            Assert.Empty(problems);
            Assert.Equal(new[] { "Stone", "Wood" }, cost.Select(c => c.Key));
            Assert.Equal(new[] { 3, 2 }, cost.Select(c => c.Value));
        }

        [Theory]
        [InlineData("Stone")]
        [InlineData("Stone:")]
        [InlineData(":5")]
        [InlineData("Stone:0")]
        [InlineData("Stone:-1")]
        [InlineData("Stone:many")]
        [InlineData("Stone:1:2")]
        public void BadEntriesAreReportedAndSkipped(string bad)
        {
            var problems = new List<string>();
            var cost = CostList.Parse($"Wood:2,{bad}", problems);
            Assert.Equal(new[] { bad }, problems);
            Assert.Equal(new[] { "Wood" }, cost.Select(c => c.Key));
        }

        [Theory]
        // WardMode is internal, so the cases pass its name (an internal type can't be a public method's parameter)
        [InlineData("Thunderdome", true, false)]
        [InlineData("Repel", false, true)]
        [InlineData("Both", true, true)]
        public void ModesDecideStrikingAndRepelling(string name, bool strikes, bool repels)
        {
            var mode = (WardMode)System.Enum.Parse(typeof(WardMode), name);
            Assert.Equal(strikes, Targeting.Strikes(mode));
            Assert.Equal(repels, Targeting.Repels(mode));
        }

        [Theory]
        [InlineData(Character.Faction.ForestMonsters)]
        [InlineData(Character.Faction.Undead)]
        [InlineData(Character.Faction.Boss)]
        [InlineData(Character.Faction.SeaMonsters)]
        [InlineData(Character.Faction.MistlandsMonsters)]
        [InlineData(Character.Faction.DeepNorth)]
        public void MonstersAndBossesAreStruck(Character.Faction faction)
        {
            Assert.True(Targeting.ShouldStrike(faction, isPlayer: false, tamed: false, aggravated: false));
        }

        [Theory]
        [InlineData(Character.Faction.Players)]
        [InlineData(Character.Faction.AnimalsVeg)]
        [InlineData(Character.Faction.PlayerSpawned)]
        [InlineData(Character.Faction.TrainingDummy)]
        public void FriendlyAndPassiveFactionsAreSpared(Character.Faction faction)
        {
            Assert.False(Targeting.ShouldStrike(faction, isPlayer: false, tamed: false, aggravated: false));
        }

        [Fact]
        public void PlayersAndTamedCreaturesAreSparedWhateverTheirFaction()
        {
            Assert.False(Targeting.ShouldStrike(Character.Faction.ForestMonsters, isPlayer: true, tamed: false, aggravated: false));
            Assert.False(Targeting.ShouldStrike(Character.Faction.ForestMonsters, isPlayer: false, tamed: true, aggravated: true));
        }

        [Fact]
        public void DvergrAreStruckOnlyWhenAngered()
        {
            Assert.False(Targeting.ShouldStrike(Character.Faction.Dverger, isPlayer: false, tamed: false, aggravated: false));
            Assert.True(Targeting.ShouldStrike(Character.Faction.Dverger, isPlayer: false, tamed: false, aggravated: true));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void NothingConfiguredGivesNoEntries(string text)
        {
            var problems = new List<string>();
            Assert.Empty(CostList.Parse(text, problems));
            Assert.Empty(problems);
        }
    }
}
