namespace EverythingsANail
{
    // Switching the equipped hammer between tools. Switching re-equips the hammer, so the game sets
    // up the hand slot, build mode, animations and visuals exactly as for the real tool.
    internal static class ToolSwitcher
    {
        public static ItemDrop.ItemData EquippedHammer(Player player)
        {
            if (!player) return null;
            if (Impersonation.IsHammer(player.m_rightItem)) return player.m_rightItem;
            if (Impersonation.IsHammer(player.m_leftItem)) return player.m_leftItem;
            return null;
        }

        public static void Switch(Player player, ToolInfo tool)
        {
            ItemDrop.ItemData hammer = EquippedHammer(player);
            if (hammer == null || tool == null || Impersonation.CurrentTool(hammer) == tool) return;
            // The same conditions under which the game refuses to equip; checked first so the hammer
            // isn't left unequipped
            if (player.InAttack() || player.InDodge() || (player.IsSwimming() && !player.IsOnGround()))
            {
                player.Message(MessageHud.MessageType.Center, "Can't switch tools right now");
                return;
            }
            Impersonation.SetStoredTool(hammer, tool);
            Reequip(player, hammer);
        }

        // Crafting stations match recipes by name, so a disguised hammer would be offered the tool's
        // upgrade. Turn it back into a hammer while the station is in use.
        public static void RevertToHammer(Player player)
        {
            ItemDrop.ItemData hammer = EquippedHammer(player);
            if (hammer == null || !Impersonation.IsDisguised(hammer)) return;
            Impersonation.SetStoredTool(hammer, null);
            Reequip(player, hammer);
        }

        // The rules changed (config edit or server values): drop a tool that's no longer allowed.
        // The hammer keeps it stored, so it comes back if it's allowed again.
        public static void Revalidate()
        {
            Player player = Player.m_localPlayer;
            ItemDrop.ItemData hammer = EquippedHammer(player);
            if (hammer == null || !Impersonation.IsDisguised(hammer)) return;
            ToolInfo tool = Impersonation.CurrentTool(hammer);
            if (ToolCatalog.IsAvailable(player, tool)) return;
            player.Message(MessageHud.MessageType.Center, $"{tool.DisplayName} isn't allowed here; back to the hammer");
            Reequip(player, hammer);
        }

        private static void Reequip(Player player, ItemDrop.ItemData hammer)
        {
            player.UnequipItem(hammer, triggerEquipEffects: false);
            if (!player.EquipItem(hammer))
            {
                Plugin.Log.LogWarning($"Couldn't re-equip the hammer as {Impersonation.StoredTool(hammer) ?? ToolCatalog.HammerPrefab}");
            }
        }
    }
}
