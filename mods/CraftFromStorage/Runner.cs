using System.Collections.Generic;
using UnityEngine;

namespace Storage
{
    internal static class Status
    {
        public static string Line()
        {
            string key = Settings.ToggleKey.Value.ToString();
            if (!Settings.Enabled.Value) return $"Storage: off [{key}]";
            int chests = StorageNetwork.Usable.Count;
            var parts = new List<string> { $"Storage [{key}]: {chests} chest{(chests == 1 ? "" : "s")} within {StorageNetwork.EffectiveRange:0} m" };
            if (StorageNetwork.RangeCapped) parts.Add("range capped by loaded area");
            if (StorageNetwork.SkippedNoOwner > 0) parts.Add($"{StorageNetwork.SkippedNoOwner} skipped (no owner)");
            if (StorageNetwork.InUseByOthers > 0) parts.Add($"{StorageNetwork.InUseByOthers} in use");
            if (StorageNetwork.Unresponsive > 0) parts.Add($"{StorageNetwork.Unresponsive} not answering");
            if (ChestAccess.PendingPulls > 0) parts.Add($"fetching {ChestAccess.PendingPulls}");
            if (!MucHealth.CanUseNonOwnedChests && !StorageNetwork.IsSinglePlayer()) parts.Add("only your chests (MultiUserChest missing or broken)");
            return string.Join(" · ", parts);
        }
    }

    // Per-frame work: pull/borrow bookkeeping, hotkeys, borrowing ahead of need, and the status line
    internal sealed class Runner : MonoBehaviour
    {
        private const float BorrowAheadInterval = 1f;
        private float m_nextBorrowAhead;
        private GUIStyle m_style;
        private GUIStyle m_shadow;

        private void Update()
        {
            ChestAccess.Tick();
            Player player = Player.m_localPlayer;
            if (!player || Hotkeys.IsTyping()) return;

            if (Hotkeys.Pressed(Settings.ToggleKey))
            {
                Settings.Enabled.Value = !Settings.Enabled.Value;
                StorageNetwork.Invalidate();
                player.Message(MessageHud.MessageType.TopLeft, $"Use nearby storage: {(Settings.Enabled.Value ? "on" : "off")}");
            }
            if (Hotkeys.Pressed(Settings.BlocklistKey) && Hooks.Active(Settings.Smelting.Value))
                SmeltFlow.ToggleBlocklistOnHover(player);

            if (Time.time >= m_nextBorrowAhead)
            {
                m_nextBorrowAhead = Time.time + BorrowAheadInterval;
                BorrowAhead(player);
                SmeltFlow.Prune();
                StorageNetwork.Prune();
            }
        }

        // Ask for the chests holding what the selected recipe or piece needs, so by the time the player clicks they're
        // ours (synchronous, no inventory space needed). Pulls only cover what couldn't be borrowed.
        private static void BorrowAhead(Player player)
        {
            if (!Settings.Enabled.Value) return;
            Inventory inventory = player.GetInventory();
            List<Need> needs = null;
            if (player.InPlaceMode())
            {
                if (Settings.Building.Value) needs = Needs.ForPiece(player.GetSelectedPiece());
            }
            else if (InventoryGui.IsVisible() && Settings.Crafting.Value && InventoryGui.instance.m_selectedRecipe.Recipe)
            {
                InventoryGui gui = InventoryGui.instance;
                ItemDrop.ItemData upgrade = gui.m_selectedRecipe.ItemData;
                needs = Needs.ForRecipe(player, gui.m_selectedRecipe.Recipe, upgrade == null ? 1 : upgrade.m_quality + 1, 1);
            }
            if (needs == null) return;
            foreach (Need need in needs)
                if (need.PayableNow(inventory) < need.Amount) ChestAccess.BorrowFor(need.Name);
        }

        private static bool StatusContext(Player player) =>
            player.InPlaceMode() ||
            (InventoryGui.IsVisible() && InventoryGui.instance.m_currentContainer == null) ||
            SmeltFlow.HoveredOreSwitchSmelter(player);

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !Settings.ShowStatus.Value) return;
            Player player = Player.m_localPlayer;
            if (!player || !StatusContext(player)) return;
            if (m_style == null)
            {
                m_style = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.UpperCenter };
                m_style.normal.textColor = new Color(1f, 0.9f, 0.7f);
                m_shadow = new GUIStyle(m_style);
                m_shadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            }
            string text = Status.Line();
            var rect = new Rect(0f, 8f, Screen.width, 24f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, m_shadow);
            GUI.Label(rect, text, m_style);
        }
    }
}
