using UnityEngine;

namespace LootFilter
{
    // Toggles rules for whatever item is under the cursor (inventory/chest) or crosshair (ground)
    internal sealed class Marker : MonoBehaviour
    {
        private void Update()
        {
            if (!Player.m_localPlayer || Input.IsTyping()) return;

            if (Input.Pressed(Settings.TogglePause))
            {
                Rules.Paused = !Rules.Paused;
                Message(Rules.Paused
                    ? "Loot filter paused: filtered items will be picked up"
                    : "Loot filter active");
            }

            bool item = Input.Pressed(Settings.ToggleItem);
            bool group = Input.Pressed(Settings.ToggleGroup);
            if (!item && !group) return;

            ItemDrop.ItemData target = HoveredItem();
            if (target == null) return;

            if (item) MarkItem(target);
            if (group) MarkGroup(target);
        }

        private static void MarkItem(ItemDrop.ItemData target)
        {
            bool filtered = Rules.ToggleItem(target);
            string name = Localization.instance.Localize(target.m_shared.m_name);
            Message($"{name}: {(filtered ? "filtered" : "not filtered")}");
        }

        private static void MarkGroup(ItemDrop.ItemData target)
        {
            ItemGroups group = Groups.Of(target);
            bool filtered = Rules.ToggleGroup(group);
            Message($"All {Groups.Describe(group).ToLowerInvariant()}: {(filtered ? "filtered" : "not filtered")}");
        }

        private static ItemDrop.ItemData HoveredItem()
        {
            if (InventoryGui.IsVisible())
            {
                InventoryGui gui = InventoryGui.instance;
                return HoveredIn(gui.m_playerGrid) ?? HoveredIn(gui.m_containerGrid);
            }

            GameObject hovering = Player.m_localPlayer.GetHoverObject();
            ItemDrop drop = hovering ? hovering.GetComponentInParent<ItemDrop>() : null;
            return drop ? drop.m_itemData : null;
        }

        private static ItemDrop.ItemData HoveredIn(InventoryGrid grid)
        {
            if (!grid || !grid.isActiveAndEnabled) return null;
            InventoryElement element = grid.GetHoveredElement();
            if (element == null) return null;
            Vector2i pos = grid.GetElementPos(element);
            return grid.GetInventory()?.GetItemAt(pos.x, pos.y);
        }

        private static void Message(string text) =>
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, text);
    }
}
