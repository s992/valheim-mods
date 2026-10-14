using UnityEngine;

namespace LootFilter
{
    // Toggles rules for whatever item is under the cursor (inventory/chest) or crosshair (ground)
    internal sealed class Marker : MonoBehaviour
    {
        private void Update()
        {
            if (!Player.m_localPlayer || Input.IsTyping()) return;

            bool item = Input.Pressed(Settings.ToggleItem);
            bool group = Input.Pressed(Settings.ToggleGroup);
            bool rarity = Input.Pressed(Settings.ToggleRarity);
            bool outdated = Input.Pressed(Settings.ToggleOutdatedRarity);
            bool biome = Input.Pressed(Settings.ToggleBiomeRarity);
            if (!item && !group && !rarity && !outdated && !biome) return;

            ItemDrop.ItemData target = HoveredItem();
            if (target == null) return;

            if (item) MarkItem(target);
            if (group) MarkGroup(target);
            if (rarity) MarkRarity(target, RarityRule.Always);
            if (outdated) MarkRarity(target, RarityRule.Outdated);
            if (biome) MarkRarity(target, RarityRule.ItemBiome);
        }

        private enum RarityRule
        {
            Always,
            Outdated,
            ItemBiome,
        }

        private static void MarkItem(ItemDrop.ItemData target)
        {
            bool filtered = Rules.ToggleItem(target);
            string name = Localization.instance.Localize(target.m_shared.m_name);
            string message = $"{name}: {(filtered ? "filtered" : "not filtered")}";

            // An item rule doesn't affect magic items, so say so instead of looking broken
            if (EpicLootCompat.TryGetRarity(target, out _, out bool isMagicItem) && isMagicItem)
                message += " (magic items use rarity rules)";
            Message(message);
        }

        private static void MarkGroup(ItemDrop.ItemData target)
        {
            ItemGroups group = Groups.Of(target);
            bool filtered = Rules.ToggleGroup(group);
            Message($"All {Groups.Describe(group).ToLowerInvariant()}: {(filtered ? "filtered" : "not filtered")}");
        }

        private static void MarkRarity(ItemDrop.ItemData target, RarityRule rule)
        {
            if (!EpicLootCompat.Available)
            {
                Message("Rarity rules need Epic Loot");
                return;
            }
            if (rule != RarityRule.Always && !EpicLootCompat.BiomeTiersAvailable)
            {
                Message("Biome-based rules aren't supported by this Epic Loot version");
                return;
            }
            if (!EpicLootCompat.TryGetRarity(target, out Rarities rarity, out bool isMagicItem))
            {
                Message("This item has no Epic Loot rarity");
                return;
            }

            bool filtered;
            string what;
            switch (rule)
            {
                case RarityRule.Outdated:
                    filtered = Rules.ToggleOutdatedRarity(rarity);
                    what = $"Outdated {rarity} items";
                    break;
                case RarityRule.ItemBiome:
                    if (!EpicLootCompat.TryGetItemBiome(Rules.ItemName(target), out Heightmap.Biome itemBiome, out _)
                        || !Settings.RaritiesByItemBiome.ContainsKey(itemBiome))
                    {
                        Message("Epic Loot doesn't assign this item to a biome tier");
                        return;
                    }
                    filtered = Rules.ToggleBiomeRarity(itemBiome, rarity);
                    what = $"{rarity} items from {itemBiome}";
                    break;
                default:
                    filtered = Rules.ToggleRarity(rarity);
                    what = $"{rarity} items";
                    break;
            }

            string message = $"{what}: {(filtered ? "filtered" : "not filtered")}";
            if (filtered && rule == RarityRule.Always)
                message += " (from every biome)";
            if (!isMagicItem && !Settings.IncludeMagicMaterials.Value)
                message += " (materials and runestones need IncludeMagicMaterials)";
            Message(message);
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
