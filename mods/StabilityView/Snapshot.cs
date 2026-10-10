using System.Collections.Generic;
using UnityEngine;

namespace StabilityView
{
    // Support values recorded at a point in time, to show how later changes affected them
    internal static class Snapshot
    {
        private static Dictionary<ZDOID, float> s_values;
        private static float s_takenAt;

        public static bool Active =>
            s_values != null && (Settings.ChangeDuration.Value <= 0f || Time.time - s_takenAt <= Settings.ChangeDuration.Value);

        // A manual snapshot stays put (automatic ones are skipped) until it's cleared,
        // so changes can be compared across several builds/removals
        public static bool IsManual { get; private set; }

        public static void Capture(bool manual)
        {
            s_values = new Dictionary<ZDOID, float>();
            foreach (WearNTear wnt in WearNTear.GetAllInstances())
            {
                if (Stability.UsesSupport(wnt))
                    s_values[wnt.m_nview.GetZDO().m_uid] = Stability.Support(wnt);
            }
            s_takenAt = Time.time;
            IsManual = manual;
        }

        public static void Clear()
        {
            s_values = null;
            IsManual = false;
        }

        // False for pieces that didn't exist when the snapshot was taken
        public static bool TryGet(WearNTear wnt, out float support)
        {
            support = 0f;
            return s_values != null && s_values.TryGetValue(wnt.m_nview.GetZDO().m_uid, out support);
        }
    }
}
