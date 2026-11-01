using System;
using UnityEngine;

namespace ValheimMods.Shared
{
    // Unity logs an exception thrown from Update or OnGUI and calls the method again next frame, so one bug can write
    // to the log every frame for the rest of the session. Per-frame bodies catch and pass errors here: the first is
    // logged in full; if they keep coming, the component is switched off (its OnDisable undoes its changes) and the
    // player is told.
    //
    //     private void Update()
    //     {
    //         try { Tick(); }
    //         catch (Exception e) { m_guard.Report(e); }
    //     }
    internal sealed class FrameGuard
    {
        // A few errors can be a one-off (a scene loading); this many means the feature is broken
        private const int MaxErrors = 10;

        private readonly Behaviour m_owner;
        private readonly string m_feature;
        private int m_errors;

        /// <param name="feature">What the player would call it, e.g. "the stability overlay".</param>
        public FrameGuard(Behaviour owner, string feature)
        {
            m_owner = owner;
            m_feature = feature;
        }

        public void Report(Exception e)
        {
            m_errors++;
            if (m_errors == 1) ModInfo.Log.LogError($"Error in {m_feature}: {e}");
            if (m_errors < MaxErrors) return;

            m_owner.enabled = false;
            string message = $"{ModInfo.Name}: {m_feature} kept failing and is off until the game restarts. " +
                             "Details are in BepInEx/LogOutput.log.";
            ModInfo.Log.LogError(message);
            PlayerNotice.Show(message);
        }
    }
}
