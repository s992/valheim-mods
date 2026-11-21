using System.Collections.Generic;
using BuildRadius;
using Xunit;

namespace ValheimMods.Tests
{
    public class BuildRadiusTests
    {
        [Fact]
        public void MultiplierScalesBaseAndExtensionBonus()
        {
            var (baseRange, extra) = Radius.Compute(20f, 4f, 2f, null);
            Assert.Equal(40f, baseRange);
            Assert.Equal(8f, extra);
        }

        [Fact]
        public void OverrideReplacesBaseButExtensionsFollowTheMultiplier()
        {
            var (baseRange, extra) = Radius.Compute(20f, 4f, 2f, 35f);
            Assert.Equal(35f, baseRange);
            Assert.Equal(8f, extra);
        }

        [Fact]
        public void OverridesParseWithSpacesCaseAndDollarSigns()
        {
            var problems = new List<string>();
            var overrides = Radius.ParseOverrides(" Piece_Workbench : 40 ,, $piece_forge:30.5, ", problems);
            Assert.Empty(problems);
            Assert.Equal(40f, overrides["piece_workbench"]);
            Assert.Equal(30.5f, overrides["piece_forge"]);
        }

        [Fact]
        public void EmptyOverridesAreFine()
        {
            var problems = new List<string>();
            Assert.Empty(Radius.ParseOverrides("", problems));
            Assert.Empty(Radius.ParseOverrides(null, problems));
            Assert.Empty(problems);
        }

        [Fact]
        public void LaterEntryForTheSameStationWins()
        {
            var overrides = Radius.ParseOverrides("forge:30, FORGE:50", new List<string>());
            Assert.Equal(50f, overrides["forge"]);
        }

        [Theory]
        [InlineData("forge")]
        [InlineData("forge:")]
        [InlineData(":30")]
        [InlineData("$:30")]
        [InlineData("forge:0")]
        [InlineData("forge:-5")]
        [InlineData("forge:501")]
        [InlineData("forge:NaN")]
        [InlineData("forge:far")]
        [InlineData("forge:1:2")]
        [InlineData("forge:30,5")]
        public void BadEntriesAreReportedAndSkipped(string bad)
        {
            var problems = new List<string>();
            var overrides = Radius.ParseOverrides("piece_workbench:40," + bad, problems);
            Assert.NotEmpty(problems);
            Assert.Equal(40f, overrides["piece_workbench"]);
        }

        [Fact]
        public void DecimalCommaIsNotReadAsADecimal()
        {
            // "30,5" splits into "forge:30" and a stray "5", whatever the PC's locale
            var problems = new List<string>();
            var overrides = Radius.ParseOverrides("forge:30,5", problems);
            Assert.Equal(30f, overrides["forge"]);
            Assert.Equal(new[] { "5" }, problems);
        }

        [Fact]
        public void FindMatchesPrefabNameFirstThenStationName()
        {
            var overrides = Radius.ParseOverrides("forge:30, piece_forge:50, piece_stonecutter:25", new List<string>());
            Assert.Equal(30f, Radius.Find(overrides, "forge", "$piece_forge"));
            Assert.Equal(25f, Radius.Find(overrides, "piece_stonecutter", "$piece_stonecutter"));
            Assert.Null(Radius.Find(overrides, "piece_workbench", "$piece_workbench"));
        }

        [Fact]
        public void StationNameAloneMatches()
        {
            var overrides = Radius.ParseOverrides("piece_forge:50", new List<string>());
            Assert.Equal(50f, Radius.Find(overrides, "forge", "$piece_forge"));
        }
    }
}
