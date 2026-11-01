using System.Linq;
using CraftFromStorage;
using Xunit;

namespace ValheimMods.Tests
{
    internal static class Items
    {
        public static ItemDrop.ItemData Item(string name, int stack, int maxStack = 50, int quality = 1, int x = 0, int y = 0) =>
            new ItemDrop.ItemData
            {
                m_shared = new ItemDrop.ItemData.SharedData { m_name = name, m_maxStackSize = maxStack },
                m_stack = stack,
                m_quality = quality,
                m_gridPos = new Vector2i(x, y),
            };

        public static Inventory Inventory(int width, int height, params ItemDrop.ItemData[] items)
        {
            var inventory = new Inventory("test", null, width, height);
            inventory.m_inventory.AddRange(items);
            return inventory;
        }
    }

    // Taking from a chest we own is the step that actually removes players' items
    public class CraftFromStorageTakeTests
    {
        [Fact]
        public void TakeFrom_TakesAcrossStacks_AndNoMore()
        {
            Inventory chest = Items.Inventory(5, 2, Items.Item("$wood", 30), Items.Item("$stone", 10, x: 1), Items.Item("$wood", 30, x: 2));

            int taken = ChestAccess.TakeFrom(chest, "$wood", 45, -1, false);

            Assert.Equal(45, taken);
            Assert.Equal(15, chest.CountItems("$wood", -1, false));
            Assert.Equal(10, chest.CountItems("$stone", -1, false));
        }

        [Fact]
        public void TakeFrom_ReportsWhatWasThere_WhenShort()
        {
            Inventory chest = Items.Inventory(5, 2, Items.Item("$wood", 7));

            Assert.Equal(7, ChestAccess.TakeFrom(chest, "$wood", 20, -1, false));
            Assert.Empty(chest.m_inventory);
        }

        [Fact]
        public void TakeFrom_OnlyTakesTheAskedQuality()
        {
            Inventory chest = Items.Inventory(5, 2, Items.Item("$sword", 1, quality: 1), Items.Item("$sword", 1, quality: 3, x: 1));

            Assert.Equal(1, ChestAccess.TakeFrom(chest, "$sword", 2, 3, false));
            Assert.Equal(1, chest.m_inventory.Single().m_quality);
        }

        [Fact]
        public void TakeFrom_NothingMatching_TakesNothing()
        {
            Inventory chest = Items.Inventory(5, 2, Items.Item("$stone", 10));

            Assert.Equal(0, ChestAccess.TakeFrom(chest, "$wood", 5, -1, false));
            Assert.Equal(10, chest.CountItems("$stone", -1, false));
        }
    }

    public class CraftFromStoragePlanTests
    {
        private static ChestAccess.PullPlan Plan(Inventory target, int amount, params Inventory[] chests) =>
            ChestAccess.Plan(chests.Select(c => ((Container)null, c)), "$wood", -1, amount, target, new ChestAccess.SlotReservation());

        [Fact]
        public void Plan_TakesNearestChestsFirst_AndStopsWhenCovered()
        {
            Inventory near = Items.Inventory(5, 2, Items.Item("$wood", 20));
            Inventory far = Items.Inventory(5, 2, Items.Item("$wood", 20));

            ChestAccess.PullPlan plan = Plan(Items.Inventory(8, 4), 25, near, far);

            Assert.True(plan.Ok);
            Assert.Equal(new[] { 20, 5 }, plan.Steps.Select(s => s.Amount));
            Assert.Same(near.m_inventory[0], plan.Steps[0].Item);
            Assert.Same(far.m_inventory[0], plan.Steps[1].Item);
        }

        [Fact]
        public void Plan_NotEnoughAnywhere_ReportsWhatsMissing()
        {
            ChestAccess.PullPlan plan = Plan(Items.Inventory(8, 4), 30, Items.Inventory(5, 2, Items.Item("$wood", 12)));

            Assert.False(plan.Ok);
            Assert.Equal(18, plan.Missing);
        }

        [Fact]
        public void Plan_GivesEveryStepItsOwnSlot()
        {
            Inventory chest = Items.Inventory(5, 2, Items.Item("$wood", 50), Items.Item("$wood", 50, x: 1), Items.Item("$wood", 50, x: 2));

            ChestAccess.PullPlan plan = Plan(Items.Inventory(8, 4), 150, chest);

            Assert.True(plan.Ok);
            Assert.Equal(3, plan.Steps.Select(s => s.Slot).Distinct().Count());
        }

        [Fact]
        public void Plan_LeavesTheHotbarForLast()
        {
            ChestAccess.PullPlan plan = Plan(Items.Inventory(8, 4), 10, Items.Inventory(5, 2, Items.Item("$wood", 10)));

            Assert.Equal(1, plan.Steps.Single().Slot.y);
        }

        [Fact]
        public void Plan_TopsUpAPartialStackInsteadOfANewSlot()
        {
            Inventory target = Items.Inventory(8, 4, Items.Item("$wood", 40, x: 3, y: 2));

            ChestAccess.PullPlan plan = Plan(target, 10, Items.Inventory(5, 2, Items.Item("$wood", 10)));

            Assert.Equal(new Vector2i(3, 2), plan.Steps.Single().Slot);
        }

        [Fact]
        public void Plan_FullInventory_CountsTheSlotsItsShort()
        {
            var full = Enumerable.Range(0, 4).Select(x => Items.Item("$stone", 50, x: x)).ToArray();
            ChestAccess.PullPlan plan = Plan(Items.Inventory(4, 1, full), 60, Items.Inventory(5, 2, Items.Item("$wood", 50), Items.Item("$wood", 10, x: 1)));

            Assert.False(plan.Ok);
            Assert.Equal(2, plan.SlotsShort);
            Assert.Empty(plan.Steps);
        }

        [Fact]
        public void SlotReservation_SharedAcrossPlans_NeverHandsOutASlotTwice()
        {
            Inventory target = Items.Inventory(2, 1);
            var slots = new ChestAccess.SlotReservation();
            Inventory chest = Items.Inventory(5, 2, Items.Item("$wood", 10), Items.Item("$stone", 10, x: 1), Items.Item("$resin", 10, x: 2));
            (Container, Inventory)[] chests = { (null, chest) };

            ChestAccess.PullPlan wood = ChestAccess.Plan(chests, "$wood", -1, 10, target, slots);
            ChestAccess.PullPlan stone = ChestAccess.Plan(chests, "$stone", -1, 10, target, slots);
            ChestAccess.PullPlan resin = ChestAccess.Plan(chests, "$resin", -1, 10, target, slots);

            Assert.NotEqual(wood.Steps.Single().Slot, stone.Steps.Single().Slot);
            Assert.Equal(1, resin.SlotsShort);
        }
    }

    // The smelter's count lags behind our own inserts; this is what stopped holding E from overfilling smelters
    public class CraftFromStorageSmelterInFlightTests
    {
        [Fact]
        public void Sent_CountsUntilTheSmelterShowsIt()
        {
            var channel = new SmelterInFlight.Channel();
            channel.Sent(observed: 5, now: 0f);
            channel.Sent(observed: 5, now: 0.1f);

            Assert.Equal(7, channel.Effective(observed: 5, now: 0.2f));
            Assert.Equal(7, channel.Effective(observed: 6, now: 0.3f));
            Assert.Equal(7, channel.Effective(observed: 7, now: 0.4f));
        }

        [Fact]
        public void FuelBurningDown_DoesntCountAsArrivals()
        {
            var channel = new SmelterInFlight.Channel();
            channel.Sent(observed: 5f, now: 0f);

            // 0.7 burned since: still one insert on its way, not two
            Assert.Equal(5.3f, channel.Effective(observed: 4.3f, now: 0.5f), 3);
        }

        [Fact]
        public void LostUpdates_ExpireAfterAFewSeconds()
        {
            var channel = new SmelterInFlight.Channel();
            channel.Sent(observed: 5, now: 0f);

            Assert.Equal(6, channel.Effective(observed: 5, now: 2f));
            Assert.Equal(5, channel.Effective(observed: 5, now: 4f));
        }
    }

    public class CraftFromStorageSettingsTests
    {
        [Fact]
        public void SplitNames_TrimsAndIgnoresCaseAndBlanks()
        {
            var names = CraftFromStorage.Settings.SplitNames(" FineWood, ,roundlog ,");

            Assert.Equal(2, names.Count);
            Assert.Contains("RoundLog", names);
        }

        [Fact]
        public void Toggled_AddsThenRemoves()
        {
            string added = CraftFromStorage.Settings.Toggled("FineWood", "Coal");
            Assert.True(CraftFromStorage.Settings.SplitNames(added).SetEquals(new[] { "FineWood", "Coal" }));
            Assert.Equal("FineWood", CraftFromStorage.Settings.Toggled(added, "coal"));
        }

        [Fact]
        public void Selections_RoundTrip_PerSmelter()
        {
            string stored = CraftFromStorage.Settings.WithSelection("", "smelter", "CopperOre");
            stored = CraftFromStorage.Settings.WithSelection(stored, "blastfurnace", "BlackMetalScrap");
            stored = CraftFromStorage.Settings.WithSelection(stored, "smelter", "TinOre");

            Assert.Equal("TinOre", CraftFromStorage.Settings.SelectionIn(stored, "smelter"));
            Assert.Equal("BlackMetalScrap", CraftFromStorage.Settings.SelectionIn(stored, "blastfurnace"));
            Assert.Null(CraftFromStorage.Settings.SelectionIn(stored, "kiln"));
        }

        [Fact]
        public void Selections_SmelterNamesThatSharePrefixes_StayApart()
        {
            string stored = CraftFromStorage.Settings.WithSelection("smelter2=Coal", "smelter", "CopperOre");

            Assert.Equal("Coal", CraftFromStorage.Settings.SelectionIn(stored, "smelter2"));
            Assert.Equal("CopperOre", CraftFromStorage.Settings.SelectionIn(stored, "smelter"));
        }
    }

    public class CraftFromStorageRangeTests
    {
        [Theory]
        [InlineData(2, true, 128f)]
        [InlineData(1, true, 64f)]
        [InlineData(4, true, 256f)]
        public void GuaranteedLoadedRadius_Classic_IsWholeRings(int near, bool classic, float expected) =>
            Assert.Equal(expected, StorageNetwork.GuaranteedLoadedRadius(new SimulationDistance(near, 2, classic)), 3);

        [Fact]
        public void GuaranteedLoadedRadius_NonClassic_SkipsCorners()
        {
            float radius = StorageNetwork.GuaranteedLoadedRadius(new SimulationDistance(2, 2));
            Assert.InRange(radius, 64f, 128f);
        }
    }
}
