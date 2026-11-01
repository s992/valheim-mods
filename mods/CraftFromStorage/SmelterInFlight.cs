using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CraftFromStorage
{
    // A smelter's ore queue and fuel live in its ZDO, written by its owner (often another player). Each insert is an RPC
    // to that owner, and our copy of the count only catches up when the owner's next update arrives. Holding E repeats
    // faster than that, so checks against our copy saw room that was already used, and the owner doesn't re-check the
    // limit, which overfilled smelters. This counts our own inserts that the smelter's count doesn't show yet.
    internal static class SmelterInFlight
    {
        // Backstop for lost updates: by then our copy has long caught up
        private const float Expiry = 3f;

        internal sealed class Channel
        {
            private int m_pending;
            private float m_lastSeen = float.NaN;
            private float m_expires;

            public float Effective(float observed, float now)
            {
                Catch(observed, now);
                return observed + m_pending;
            }

            public void Sent(float observed, float now)
            {
                Catch(observed, now);
                m_pending++;
                m_expires = now + Expiry;
            }

            // Each confirmed insert raises the count by one. Fuel also burns down slowly meanwhile, so a rise of half a
            // unit or more counts as arrivals (rounded).
            private void Catch(float observed, float now)
            {
                if (!float.IsNaN(m_lastSeen))
                {
                    float rise = observed - m_lastSeen;
                    if (rise >= 0.5f) m_pending = Mathf.Max(0, m_pending - Mathf.RoundToInt(rise));
                }
                m_lastSeen = observed;
                if (m_pending > 0 && now > m_expires) m_pending = 0;
            }
        }

        private static readonly Dictionary<Smelter, (Channel Ore, Channel Fuel)> s_channels = new Dictionary<Smelter, (Channel, Channel)>();

        private static (Channel Ore, Channel Fuel) For(Smelter smelter)
        {
            if (!s_channels.TryGetValue(smelter, out var channels))
                s_channels[smelter] = channels = (new Channel(), new Channel());
            return channels;
        }

        public static int Queue(Smelter smelter) => Mathf.RoundToInt(For(smelter).Ore.Effective(smelter.GetQueueSize(), Time.time));
        public static float Fuel(Smelter smelter) => For(smelter).Fuel.Effective(smelter.GetFuel(), Time.time);

        // When we own the smelter the RPC is handled on the spot, so the count already includes the insert
        public static void OreSent(Smelter smelter)
        {
            if (!smelter.m_nview.IsOwner()) For(smelter).Ore.Sent(smelter.GetQueueSize(), Time.time);
        }

        public static void FuelSent(Smelter smelter)
        {
            if (!smelter.m_nview.IsOwner()) For(smelter).Fuel.Sent(smelter.GetFuel(), Time.time);
        }

        public static void Prune()
        {
            foreach (Smelter dead in s_channels.Keys.Where(s => !s).ToList()) s_channels.Remove(dead);
        }
    }
}
