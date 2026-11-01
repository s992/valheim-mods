using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CraftFromStorage
{
    // The only code that changes chest contents. Rules:
    // - Chests we own are edited directly (synchronous), and only once our local copy is up to date.
    // - Chests other players own are never edited: items are *pulled* into the player's inventory through MUC, using the
    //   same call QSS restock uses, or the chest is *borrowed* (ownership handed over the way vanilla does on open).
    internal static class ChestAccess
    {
        private const string BorrowRpc = "s992.CraftFromStorage.Borrow";
        private const string BorrowReplyRpc = "s992.CraftFromStorage.BorrowReply";
        private const float BorrowTimeout = 5f;
        private const float BorrowCooldown = 15f;
        private const float BorrowForInterval = 3f;
        private const int MaxBorrowsPerCall = 8;
        private const float PullTimeout = 5f;

        // ------------------------------------------------------------------ owned chests

        /// <summary>True when we may edit the chest now: we own it and our copy matches its saved contents.</summary>
        public static bool PrepareOwned(Container chest)
        {
            ZNetView nview = chest.m_nview;
            if (!nview || !nview.IsValid()) return false;
            if (!nview.IsOwner())
            {
                // Alone in the world nobody else can write it, so claiming an ownerless chest is safe
                if (nview.GetZDO().GetOwner() == 0L && StorageNetwork.IsSinglePlayer()) nview.ClaimOwnership();
                else return false;
            }
            // Saving a stale copy as owner would overwrite the real contents (e.g. right after a borrow, the
            // "granted" reply can arrive before the chest's latest data). Reload first; Load skips while the chest is
            // open locally, and then the open copy is the one being saved anyway.
            if (nview.GetZDO().DataRevision != chest.m_lastRevision && !chest.m_inUse) chest.Load();
            return true;
        }

        /// <summary>Removes up to <paramref name="amount"/> matching items from chests we own, nearest first.</summary>
        public static int TakeFromOwned(string name, int amount, int quality, bool worldLevelBased, string reason)
        {
            int remaining = amount;
            foreach (Container chest in StorageNetwork.Usable)
            {
                if (remaining <= 0) break;
                if (!StorageNetwork.IsOwnedForUse(chest) || !PrepareOwned(chest)) continue;
                int taken = TakeFrom(chest.GetInventory(), name, remaining, quality, worldLevelBased);
                if (taken == 0) continue;
                remaining -= taken;
                StorageLog.Action($"took {taken} {name} from {Describe(chest)} for {reason}");
            }
            return amount - remaining;
        }

        /// <summary>Removes up to <paramref name="amount"/> matching items from one inventory. Returns how many it took.</summary>
        public static int TakeFrom(Inventory inventory, string name, int amount, int quality, bool worldLevelBased)
        {
            int remaining = amount;
            foreach (ItemDrop.ItemData item in inventory.m_inventory.ToList())
            {
                if (remaining <= 0) break;
                if (!StorageNetwork.Matches(item, name, quality, worldLevelBased)) continue;
                int take = Mathf.Min(item.m_stack, remaining);
                if (inventory.RemoveItem(item, take)) remaining -= take;
            }
            return amount - remaining;
        }

        // ------------------------------------------------------------------ borrowing

        private static readonly Dictionary<ZDOID, float> s_borrowPending = new Dictionary<ZDOID, float>();
        private static readonly Dictionary<ZDOID, float> s_awaitingOwnership = new Dictionary<ZDOID, float>();
        private static readonly Dictionary<ZDOID, float> s_borrowCooldown = new Dictionary<ZDOID, float>();
        private static readonly Dictionary<string, float> s_nextBorrowFor = new Dictionary<string, float>();
        // Chests we borrowed recently: we don't hand these straight back, so two players crafting at the same base
        // can't pass chests back and forth (each hand-over resends the whole chest to everyone nearby)
        private static readonly Dictionary<ZDOID, float> s_borrowedAt = new Dictionary<ZDOID, float>();
        private const float KeepBorrowed = 30f;
        // Global budget across all callers: every hand-over is network traffic for everyone near the chest
        private const int MaxBorrowsInFlight = 3;
        private const float MinSecondsBetweenBorrows = 0.3f;
        private static float s_nextBorrowAllowed;

        public static int BorrowsSent { get; private set; }
        public static int BorrowsGranted { get; private set; }
        public static int HandedAway { get; private set; }

        public static void RegisterRpcs()
        {
            ZRoutedRpc.instance.Register<ZDOID>(BorrowRpc, RPC_Borrow);
            ZRoutedRpc.instance.Register<ZDOID, bool>(BorrowReplyRpc, RPC_BorrowReply);
        }

        /// <summary>Asks the owners of nearby chests holding matching items to hand them over (rate limited per key).</summary>
        public static void BorrowFor(string key, Func<ItemDrop.ItemData, bool> match)
        {
            if (!Settings.Enabled.Value || !Settings.Borrow.Value || ZRoutedRpc.instance == null) return;
            if (s_nextBorrowFor.TryGetValue(key, out float next) && Time.time < next) return;
            s_nextBorrowFor[key] = Time.time + BorrowForInterval;

            int started = 0;
            foreach (Container chest in StorageNetwork.Candidates)
            {
                if (started >= MaxBorrowsPerCall || !BudgetAllows()) break;
                ZNetView nview = chest.m_nview;
                if (!nview || !nview.IsValid() || nview.IsOwner()) continue;
                ZDO zdo = nview.GetZDO();
                long owner = zdo.GetOwner();
                // Never while someone has it open; never ownerless ones (nobody to ask)
                if (owner == 0L || zdo.GetInt(ZDOVars.s_inUse) == 1) continue;
                ZDOID id = zdo.m_uid;
                if (s_borrowPending.ContainsKey(id) || s_awaitingOwnership.ContainsKey(id)) continue;
                if (s_borrowCooldown.TryGetValue(id, out float until) && Time.time < until) continue;
                if (!chest.GetInventory().m_inventory.Any(item => StorageNetwork.IsUsableItem(item) && match(item))) continue;

                s_borrowPending[id] = Time.time + BorrowTimeout;
                s_nextBorrowAllowed = Time.time + MinSecondsBetweenBorrows;
                ZRoutedRpc.instance.InvokeRoutedRPC(owner, BorrowRpc, id);
                BorrowsSent++;
                started++;
            }
        }

        private static bool BudgetAllows() =>
            Time.time >= s_nextBorrowAllowed && s_borrowPending.Count + s_awaitingOwnership.Count < MaxBorrowsInFlight;

        public static void BorrowFor(string itemName) =>
            BorrowFor(itemName, item => item.m_shared.m_name == itemName);

        /// <summary>A hand-over to us is asked for, or granted and not yet seen in our copy of the chest.</summary>
        public static bool BorrowsInFlight => s_borrowPending.Count + s_awaitingOwnership.Count > 0;

        /// <summary>
        /// The chest is ours or on its way to us. A fetch from it that brought nothing doesn't mean its owner can't deliver:
        /// MUC sent the request to the owner we knew of, who had just handed the chest over and so answered "failed".
        /// </summary>
        private static bool BeingHandedToUs(Container chest)
        {
            if (!chest || !chest.m_nview || !chest.m_nview.IsValid()) return false;
            ZDOID id = chest.m_nview.GetZDO().m_uid;
            return chest.m_nview.IsOwner() || s_borrowPending.ContainsKey(id) || s_awaitingOwnership.ContainsKey(id) || s_borrowedAt.ContainsKey(id);
        }

        // Runs on the chest's owner. Same transfer vanilla's Container.RPC_RequestOpen does, minus opening the UI.
        private static void RPC_Borrow(long sender, ZDOID id)
        {
            bool granted = false;
            ZDO zdo = ZDOMan.instance.GetZDO(id);
            ZNetView nview = zdo != null ? ZNetScene.instance.FindInstance(zdo) : null;
            Container chest = nview ? nview.GetComponentInChildren<Container>() : null;
            bool keptForNow = s_borrowedAt.TryGetValue(id, out float borrowedAt) && Time.time - borrowedAt < KeepBorrowed;
            if (chest && nview.IsOwner() && sender != ZDOMan.GetSessionID() && !keptForNow &&
                !chest.IsInUse() && !(chest.m_wagon && chest.m_wagon.InUse()) &&
                !(InventoryGui.instance && InventoryGui.instance.m_currentContainer == chest))
            {
                ZDOMan.instance.ForceSendZDO(sender, zdo.m_uid);
                zdo.SetOwner(sender);
                granted = true;
                HandedAway++;
                StorageLog.Action($"handed {Describe(chest)} to player {sender}");
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, BorrowReplyRpc, id, granted);
        }

        private static void RPC_BorrowReply(long sender, ZDOID id, bool granted)
        {
            s_borrowPending.Remove(id);
            // Either way, leave it alone for a while so two players crafting at once don't pass chests back and forth
            s_borrowCooldown[id] = Time.time + BorrowCooldown;
            if (!granted) return;
            s_awaitingOwnership[id] = Time.time + BorrowTimeout;
            s_borrowedAt[id] = Time.time;
            BorrowsGranted++;
        }

        // ------------------------------------------------------------------ pulls

        public sealed class Pull
        {
            public Container Chest;
            public string Name;
            public int Amount;
            public Vector2i Slot;
            public int StartCount;
            public float Deadline;
            public bool Finished;
        }

        public sealed class PullPlan
        {
            public readonly List<(Container Chest, ItemDrop.ItemData Item, int Amount, Vector2i Slot)> Steps =
                new List<(Container, ItemDrop.ItemData, int, Vector2i)>();
            /// <summary>Items still missing after every reachable chest.</summary>
            public int Missing;
            /// <summary>Free inventory slots the whole plan would need on top of what's there.</summary>
            public int SlotsShort;
            public bool Ok => Missing == 0 && SlotsShort == 0;
            public int Total => Steps.Sum(s => s.Amount);
        }

        /// <summary>Target slots handed out across several plans, so pulls never share a slot (MUC blocks one request per slot).</summary>
        public sealed class SlotReservation
        {
            private readonly HashSet<Vector2i> m_reserved = new HashSet<Vector2i>(s_pending.Select(p => p.Slot));

            public bool TryReserve(Inventory inventory, ItemDrop.ItemData item, int amount, out Vector2i slot)
            {
                // A partial stack of the same kind with room for all of it (no new slot needed)
                foreach (ItemDrop.ItemData stack in inventory.m_inventory)
                {
                    if (m_reserved.Contains(stack.m_gridPos) || !stack.IsSameType(item) || stack.m_quality != item.m_quality) continue;
                    if (stack.m_shared.m_maxStackSize - stack.m_stack < amount) continue;
                    slot = stack.m_gridPos;
                    m_reserved.Add(slot);
                    return true;
                }
                // Otherwise an empty slot, leaving the hotbar row for last
                for (int row = 1; row <= inventory.GetHeight(); row++)
                {
                    int y = row % inventory.GetHeight();
                    for (int x = 0; x < inventory.GetWidth(); x++)
                    {
                        var candidate = new Vector2i(x, y);
                        if (m_reserved.Contains(candidate) || inventory.GetItemAt(x, y) != null) continue;
                        slot = candidate;
                        m_reserved.Add(slot);
                        return true;
                    }
                }
                slot = default;
                return false;
            }
        }

        private static readonly List<Pull> s_pending = new List<Pull>();

        public static int PendingPulls => s_pending.Count;

        /// <summary>Plans pulling <paramref name="amount"/> items from chests other players own, nearest first. Changes nothing.</summary>
        public static PullPlan PlanPulls(string name, int quality, int amount, SlotReservation slots)
        {
            Player player = Player.m_localPlayer;
            if (!player || amount <= 0 || !MucHealth.CanUseNonOwnedChests) return new PullPlan { Missing = Mathf.Max(0, amount) };
            IEnumerable<(Container, Inventory)> chests = StorageNetwork.Usable
                .Where(chest => !StorageNetwork.IsOwnedForUse(chest))
                .Select(chest => (chest, chest.GetInventory()));
            return Plan(chests, name, quality, amount, player.GetInventory(), slots);
        }

        /// <summary>
        /// The planning itself: takes from <paramref name="chests"/> in order, at most a stack per step, each step into
        /// its own reserved slot of <paramref name="target"/>. Items with no slot to go to count as <see cref="PullPlan.SlotsShort"/>.
        /// </summary>
        public static PullPlan Plan(IEnumerable<(Container Chest, Inventory Inventory)> chests, string name, int quality, int amount,
            Inventory target, SlotReservation slots)
        {
            var plan = new PullPlan();
            int remaining = amount;
            foreach (var (chest, inventory) in chests)
            {
                if (remaining <= 0) break;
                foreach (ItemDrop.ItemData item in inventory.m_inventory)
                {
                    if (remaining <= 0) break;
                    if (!StorageNetwork.Matches(item, name, quality, true)) continue;
                    int take = Mathf.Min(item.m_stack, remaining, item.m_shared.m_maxStackSize);
                    remaining -= take;
                    if (slots.TryReserve(target, item, take, out Vector2i slot)) plan.Steps.Add((chest, item, take, slot));
                    else plan.SlotsShort++;
                }
            }
            plan.Missing = Mathf.Max(0, remaining);
            return plan;
        }

        /// <summary>Starts every step of a plan. Returns the pulls that are now on their way.</summary>
        public static List<Pull> Start(PullPlan plan, string reason)
        {
            var started = new List<Pull>();
            Player player = Player.m_localPlayer;
            if (!player) return started;
            Inventory inventory = player.GetInventory();
            foreach (var (chest, item, amount, slot) in plan.Steps)
            {
                if (!chest || !chest.GetInventory().ContainsItem(item)) continue;
                string name = item.m_shared.m_name;
                var pull = new Pull
                {
                    Chest = chest, Name = name, Amount = amount, Slot = slot,
                    StartCount = inventory.CountItems(name, -1, false), Deadline = Time.time + PullTimeout,
                };

                if (StorageNetwork.IsOwnedForUse(chest))
                {
                    // Became ours since planning (a borrow finished): a plain local move
                    if (!PrepareOwned(chest) || !chest.GetInventory().ContainsItem(item)) continue;
                    inventory.MoveItemToThis(chest.GetInventory(), item, amount, slot.x, slot.y);
                    pull.Finished = true;
                    StorageLog.Action($"moved {amount} {name} from {Describe(chest)} for {reason}");
                    started.Add(pull);
                    continue;
                }

                // MUC turns this into a remove request to the chest's owner; the item arrives with the reply
                bool sent = inventory.MoveItemToThis(chest.GetInventory(), item, amount, slot.x, slot.y);
                if (MucInternals.Available ? !MucInternals.IsSlotBlocked(inventory, slot) : !sent)
                {
                    StorageLog.Warning($"Couldn't fetch {amount} {name} from {Describe(chest)}: MUC didn't send the request");
                    continue;
                }
                s_pending.Add(pull);
                started.Add(pull);
                StorageLog.Action($"fetching {amount} {name} from {Describe(chest)} for {reason}");
            }
            return started;
        }

        // ------------------------------------------------------------------ per frame

        public static void Tick()
        {
            float now = Time.time;
            ExpireAll(s_borrowPending, now, id => StorageNetwork.MarkUnresponsive(FindChest(id)));
            ExpireAll(s_borrowCooldown, now, null);
            ExpireAll(s_borrowedAt, now - KeepBorrowed, null);

            if (s_awaitingOwnership.Count > 0)
            {
                foreach (ZDOID id in s_awaitingOwnership.Keys.ToList())
                {
                    Container chest = FindChest(id);
                    if (chest && chest.m_nview.IsOwner())
                    {
                        // Ownership and the chest's latest contents arrive together in one ZDO update
                        PrepareOwned(chest);
                        s_awaitingOwnership.Remove(id);
                        StorageNetwork.Invalidate();
                        StorageLog.Action($"borrowed {Describe(chest)}");
                    }
                    else if (now > s_awaitingOwnership[id]) s_awaitingOwnership.Remove(id);
                }
            }

            Player player = Player.m_localPlayer;
            if (!player || s_pending.Count == 0) return;
            Inventory inventory = player.GetInventory();
            foreach (Pull pull in s_pending)
            {
                bool arrived = MucInternals.Available
                    ? !MucInternals.IsSlotBlocked(inventory, pull.Slot)
                    : inventory.CountItems(pull.Name, -1, false) >= pull.StartCount + pull.Amount;
                if (arrived)
                {
                    pull.Finished = true;
                    // MUC also unblocks the slot when the owner answers "failed" (e.g. they portalled away and no longer
                    // have the chest loaded). Leave such a chest out for a while instead of promising its items again.
                    if (inventory.CountItems(pull.Name, -1, false) <= pull.StartCount)
                    {
                        if (BeingHandedToUs(pull.Chest))
                            StorageLog.Action($"fetch of {pull.Amount} {pull.Name} from {Describe(pull.Chest)} came back empty: the chest is being handed to us");
                        else
                        {
                            if (pull.Chest) StorageNetwork.MarkUnresponsive(pull.Chest);
                            StorageLog.Action($"fetch of {pull.Amount} {pull.Name} from {Describe(pull.Chest)} came back empty");
                        }
                    }
                    else StorageLog.Action($"fetch of {pull.Amount} {pull.Name} from {Describe(pull.Chest)} answered");
                }
                else if (now > pull.Deadline)
                {
                    // No reply (typically the owner disconnected). Unblock the slot so MUC lets auto-pickup work again;
                    // a late reply still lands in the inventory or at the player's feet.
                    MucInternals.ReleaseSlot(inventory, pull.Slot);
                    pull.Finished = true;
                    if (pull.Chest && !BeingHandedToUs(pull.Chest)) StorageNetwork.MarkUnresponsive(pull.Chest);
                    StorageLog.Warning($"Fetch of {pull.Amount} {pull.Name} from {Describe(pull.Chest)} got no reply in {PullTimeout:0} s");
                    StorageLog.Action($"fetch of {pull.Amount} {pull.Name} from {Describe(pull.Chest)} timed out");
                }
            }
            if (s_pending.RemoveAll(p => p.Finished) > 0) StorageNetwork.Invalidate();
        }

        private static readonly List<ZDOID> s_expired = new List<ZDOID>();

        // Runs every frame: no allocations unless something actually expired
        private static void ExpireAll(Dictionary<ZDOID, float> deadlines, float now, Action<ZDOID> onExpired)
        {
            if (deadlines.Count == 0) return;
            s_expired.Clear();
            foreach (KeyValuePair<ZDOID, float> pair in deadlines)
                if (now > pair.Value) s_expired.Add(pair.Key);
            foreach (ZDOID id in s_expired)
            {
                deadlines.Remove(id);
                onExpired?.Invoke(id);
            }
        }

        private static Container FindChest(ZDOID id)
        {
            ZDO zdo = ZDOMan.instance?.GetZDO(id);
            ZNetView nview = zdo != null ? ZNetScene.instance.FindInstance(zdo) : null;
            return nview ? nview.GetComponentInChildren<Container>() : null;
        }

        public static string Describe(Container chest)
        {
            if (!chest) return "a chest that's gone";
            Vector3 p = chest.transform.position;
            string name = chest.m_nview ? StorageNetwork.PrefabName(chest.m_nview.gameObject) : chest.name;
            return $"{name} at ({p.x:0}, {p.y:0}, {p.z:0})";
        }
    }
}
