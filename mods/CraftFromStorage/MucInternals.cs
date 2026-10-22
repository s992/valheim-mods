using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace Storage
{
    // MUC internals (not an API): its per-inventory slot block. A pull blocks its target slot until MUC's reply arrives,
    // and while any slot is blocked MUC refuses all auto-pickup. MUC never releases a slot whose reply was lost, so we
    // release it on timeout. If MUC changes these, pull completion falls back to watching the inventory and timeouts
    // can't unblock pickup (logged once).
    internal static class MucInternals
    {
        private static MethodInfo s_get;
        private static MethodInfo s_isSlotBlocked;
        private static MethodInfo s_releaseSlot;

        public static bool Available { get; private set; }

        public static void Init(ManualLogSource log)
        {
            if (MucHealth.State != MucState.Healthy) return;
            try
            {
                Type block = AccessTools.TypeByName("MultiUserChest.InventoryBlock");
                s_get = block == null ? null : AccessTools.Method(block, "Get", new[] { typeof(Inventory) });
                s_isSlotBlocked = block == null ? null : AccessTools.Method(block, "IsSlotBlocked", new[] { typeof(Vector2i) });
                s_releaseSlot = block == null ? null : AccessTools.Method(block, "ReleaseSlot", new[] { typeof(Vector2i) });
                Available = s_get != null && s_isSlotBlocked != null && s_releaseSlot != null;
            }
            catch (Exception e)
            {
                log.LogWarning($"MUC slot-block lookup failed: {e.Message}");
            }
            if (!Available)
                log.LogWarning("Couldn't find MUC's slot blocking; a fetch that never gets a reply may stop auto-pickup until you relog");
        }

        public static bool IsSlotBlocked(Inventory inventory, Vector2i slot)
        {
            if (!Available) return false;
            object block = s_get.Invoke(null, new object[] { inventory });
            return (bool)s_isSlotBlocked.Invoke(block, new object[] { slot });
        }

        public static void ReleaseSlot(Inventory inventory, Vector2i slot)
        {
            if (!Available) return;
            object block = s_get.Invoke(null, new object[] { inventory });
            s_releaseSlot.Invoke(block, new object[] { slot });
        }
    }
}
