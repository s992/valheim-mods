using System.Linq;
using MarkerCommand;
using Xunit;

namespace ValheimMods.Tests
{
    public class MarkerCommandTests
    {
        private static string[] Texts(string input) => Marker.Tokenize(input).Select(t => t.Text).ToArray();

        [Fact]
        public void Tokenize_KeepsQuotedNamesTogether()
        {
            Assert.Equal(new[] { "100", "-200", "My base", "house" }, Texts("100 -200 \"My base\" house"));
            Assert.True(Marker.Tokenize("\"My base\"")[0].Quoted);
        }

        [Fact]
        public void Tokenize_IgnoresRepeatedSpaces() =>
            Assert.Equal(new[] { "me", "camp" }, Texts("  me    camp  "));

        [Fact]
        public void Tokenize_UnclosedQuoteRunsToTheEnd() =>
            Assert.Equal(new[] { "me", "my camp" }, Texts("me \"my camp"));

        [Fact]
        public void Tokenize_EmptyQuotesGiveAnEmptyName() =>
            Assert.Equal(new[] { "me", "" }, Texts("me \"\""));

        [Theory]
        [InlineData("fire", Minimap.PinType.Icon0)]
        [InlineData("house", Minimap.PinType.Icon1)]
        [InlineData("HAMMER", Minimap.PinType.Icon2)]
        [InlineData("dot", Minimap.PinType.Icon3)]
        [InlineData("rune", Minimap.PinType.Icon4)]
        [InlineData("icon4", Minimap.PinType.Icon4)]
        [InlineData("boss", Minimap.PinType.Boss)]
        public void TryParseIcon_AcceptsAliasesAndEnumNames(string text, Minimap.PinType expected)
        {
            Assert.True(Marker.TryParseIcon(new Token { Text = text }, out Minimap.PinType icon));
            Assert.Equal(expected, icon);
        }

        [Fact]
        public void TryParseIcon_QuotedTextIsAName()
        {
            Assert.False(Marker.TryParseIcon(new Token { Text = "house", Quoted = true }, out Minimap.PinType icon));
            Assert.Equal(Marker.DefaultIcon, icon);
        }

        [Fact]
        public void TryParseIcon_NoneIsNotAnIcon() =>
            Assert.False(Marker.TryParseIcon(new Token { Text = "none" }, out _));

        [Fact]
        public void IsMe_OnlyUnquoted()
        {
            Assert.True(Marker.IsMe(new Token { Text = "ME" }));
            Assert.False(Marker.IsMe(new Token { Text = "me", Quoted = true }));
        }
    }
}
