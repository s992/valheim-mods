using System;

namespace LootFilter
{
    // Friendlier groupings of Valheim's item types; [Flags] so Configuration Manager shows checkboxes
    [Flags]
    public enum ItemGroups
    {
        None = 0,
        Materials = 1 << 0,
        FoodAndPotions = 1 << 1,
        Weapons = 1 << 2,
        Shields = 1 << 3,
        Armor = 1 << 4,
        Ammo = 1 << 5,
        Trophies = 1 << 6,
        Fish = 1 << 7,
        ToolsAndTorches = 1 << 8,
        UtilityAndTrinkets = 1 << 9,
        Misc = 1 << 10,
    }

    internal static class Groups
    {
        public static ItemGroups Of(ItemDrop.ItemData item)
        {
            switch (item.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Material:
                    return ItemGroups.Materials;
                case ItemDrop.ItemData.ItemType.Consumable:
                    return ItemGroups.FoodAndPotions;
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Attach_Atgeir:
                    return ItemGroups.Weapons;
                case ItemDrop.ItemData.ItemType.Shield:
                    return ItemGroups.Shields;
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Hands:
                case ItemDrop.ItemData.ItemType.Shoulder:
                    return ItemGroups.Armor;
                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                    return ItemGroups.Ammo;
                case ItemDrop.ItemData.ItemType.Trophy:
                    return ItemGroups.Trophies;
                case ItemDrop.ItemData.ItemType.Fish:
                    return ItemGroups.Fish;
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Torch:
                    return ItemGroups.ToolsAndTorches;
                case ItemDrop.ItemData.ItemType.Utility:
                case ItemDrop.ItemData.ItemType.Trinket:
                    return ItemGroups.UtilityAndTrinkets;
                default:
                    return ItemGroups.Misc;
            }
        }

        public static string Describe(ItemGroups group)
        {
            switch (group)
            {
                case ItemGroups.FoodAndPotions: return "Food & potions";
                case ItemGroups.ToolsAndTorches: return "Tools & torches";
                case ItemGroups.UtilityAndTrinkets: return "Utility & trinkets";
                default: return group.ToString();
            }
        }
    }
}
