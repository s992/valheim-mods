using System;
using System.Reflection;
using Xunit;

namespace ValheimMods.Tests
{
    // shared/ is compiled into every mod, so the same internal type exists once per mod assembly; these use
    // LootFilter's copy through reflection. Paths that show the player a message need Unity and aren't tested here.
    public class PatchGuardTests
    {
        private static readonly Type s_guard =
            typeof(LootFilter.Rules).Assembly.GetType("ValheimMods.Shared.PatchGuard", throwOnError: true);

        private static bool FromThisMod(Exception e) =>
            (bool)s_guard.GetMethod("FromThisMod", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { e });

        private static Exception Contain(Exception e) =>
            (Exception)s_guard.GetMethod("Contain").Invoke(null, new object[] { e, "test" });

        private static Exception Caught(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                return e;
            }
            throw new InvalidOperationException("expected an exception");
        }

        [Fact]
        public void ExceptionFromTheModsCode_IsRecognised()
        {
            // Groups.Of reads item.m_shared, so a null item throws inside LootFilter
            Exception e = Caught(() => LootFilter.Groups.Of(null));
            Assert.True(FromThisMod(e));
        }

        [Fact]
        public void ExceptionFromElsewhere_IsNotTheMods()
        {
            Exception e = Caught(() => throw new InvalidOperationException("the game's own"));
            Assert.False(FromThisMod(e));
        }

        [Fact]
        public void ExceptionFromElsewhere_PassesThroughUnchanged()
        {
            Exception e = Caught(() => throw new InvalidOperationException("the game's own"));
            Assert.Same(e, Contain(e));
        }

        [Fact]
        public void NoException_StaysNone() => Assert.Null(Contain(null));
    }
}
