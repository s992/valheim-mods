using System;
using System.Globalization;
using System.IO;
using System.Threading;
using BepInEx.Configuration;
using StabilityView;
using UnityEngine;
using Xunit;

namespace ValheimMods.Tests
{
    public class StabilityViewStabilityTests
    {
        private static readonly MaterialInfo s_wood = new MaterialInfo { Max = 100f, Min = 10f };

        [Theory]
        [InlineData(10f, 0f)]
        [InlineData(55f, 0.5f)]
        [InlineData(100f, 1f)]
        [InlineData(5f, 0f)]
        [InlineData(150f, 1f)]
        public void Ratio_RunsFromCollapseToMax(float support, float expected) =>
            Assert.Equal(expected, Stability.Ratio(support, s_wood), 3);

        [Fact]
        public void Ratio_MaterialWithNoRange_IsFull() =>
            Assert.Equal(1f, Stability.Ratio(0f, new MaterialInfo { Max = 5f, Min = 5f }));

        [Fact]
        public void ColorFor_AtMax_IsTheGamesBlue() =>
            Assert.Equal(Stability.MaxColor, Stability.ColorFor(100f, s_wood));

        [Fact]
        public void ColorFor_GoesFromRedToGreen()
        {
            Color low = Stability.ColorFor(10f, s_wood), high = Stability.ColorFor(99f, s_wood);
            Assert.True(low.r > low.g, $"near collapse should be red, was {low}");
            Assert.True(high.g > high.r, $"near max should be green, was {high}");
        }
    }

    // StabilityView.Settings is static, so these run in one class against one temporary config file
    public sealed class StabilityViewLabelTests : IDisposable
    {
        private static readonly MaterialInfo s_stone = new MaterialInfo { Max = 1000f, Min = 100f };
        private readonly string m_configPath = Path.Combine(Path.GetTempPath(), $"stabilityview-test-{Guid.NewGuid():N}.cfg");
        private readonly CultureInfo m_culture = Thread.CurrentThread.CurrentCulture;

        public StabilityViewLabelTests()
        {
            StabilityView.Settings.Bind(new ConfigFile(m_configPath, true));
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        }

        public void Dispose()
        {
            Thread.CurrentThread.CurrentCulture = m_culture;
            File.Delete(m_configPath);
        }

        [Fact]
        public void Piece_ShowsSupportAndMax_ByDefault()
        {
            var (_, plain) = Labels.Piece("Stone", 452.37f, s_stone, null, false);
            Assert.Equal("452.4/1000", plain);
        }

        [Fact]
        public void Piece_OptionalParts_FollowTheSettings()
        {
            StabilityView.Settings.ShowMaterial.Value = true;
            StabilityView.Settings.ShowMaxSupport.Value = false;
            StabilityView.Settings.ShowBreakThreshold.Value = true;

            var (_, plain) = Labels.Piece("Stone", 452f, s_stone, null, false);

            Assert.Equal("Stone 452 (min 100)", plain);
        }

        [Fact]
        public void Piece_Change_GoesOnASecondLine_WithASign()
        {
            Assert.EndsWith("\n+12.5", Labels.Piece("Stone", 452f, s_stone, 12.5f, false).Plain);
            Assert.EndsWith("\n-3", Labels.Piece("Stone", 452f, s_stone, -3f, false).Plain);
            Assert.EndsWith("\nnew", Labels.Piece("Stone", 452f, s_stone, null, true).Plain);
        }

        [Fact]
        public void Piece_RichText_IsThePlainTextColoured()
        {
            var (text, plain) = Labels.Piece("Stone", 452f, s_stone, -3f, false);

            string stripped = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
            Assert.Equal(plain, stripped);
            Assert.Contains(ColorUtility.ToHtmlStringRGB(Labels.DecreaseColor), text);
        }

        [Fact]
        public void Piece_UsesThePlayersNumberFormat()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Assert.StartsWith("452,4/", Labels.Piece("Stone", 452.4f, s_stone, null, false).Plain);
        }

        [Fact]
        public void Status_SaysWhyPiecesAreMissing()
        {
            string status = Labels.Status(shown: 40, hiddenByLimit: 7, hiddenByRadius: 3, snapshotActive: false, manualSnapshot: false);

            Assert.Contains("40 shown", status);
            Assert.Contains($"7 hidden by MaxPieces ({StabilityView.Settings.MaxPieces.Value})", status);
            Assert.Contains("3 beyond", status);
        }

        [Fact]
        public void Status_NothingHidden_IsJustTheCount() =>
            Assert.Equal("StabilityView: 5 shown", Labels.Status(5, 0, 0, false, false));

        [Fact]
        public void Status_ManualSnapshot_NamesTheClearKey() =>
            Assert.Contains(StabilityView.Settings.ClearSnapshot.Value.ToString(), Labels.Status(5, 0, 0, true, true));

        [Theory]
        [InlineData(5f, false)]
        [InlineData(-5f, false)]
        [InlineData(0f, true)]
        public void ChangeColor_MatchesTheDirection(float delta, bool isNew)
        {
            Color expected = isNew ? Labels.NewColor : delta > 0 ? Labels.IncreaseColor : Labels.DecreaseColor;
            Assert.Equal(expected, Labels.ChangeColor(delta, isNew));
        }
    }
}
