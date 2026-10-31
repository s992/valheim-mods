using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimMods.Shared
{
    // Shows a message to the player once they're in game. For problems that would otherwise only be in the log, like a
    // mod that turned itself off: missing behaviour with no explanation looks like a bug.
    internal sealed class PlayerNotice : MonoBehaviour
    {
        private static PlayerNotice s_instance;
        private readonly List<string> m_pending = new List<string>();

        public static void Show(string message)
        {
            if (!s_instance)
            {
                var host = new GameObject($"{ModInfo.Name}.PlayerNotice");
                DontDestroyOnLoad(host);
                s_instance = host.AddComponent<PlayerNotice>();
            }
            s_instance.m_pending.Add(message);
        }

        private void Update()
        {
            if (m_pending.Count == 0 || !Player.m_localPlayer || !MessageHud.instance) return;
            try
            {
                foreach (string message in m_pending) Send(message);
            }
            catch (Exception e)
            {
                // The game API this uses may itself have changed; the message is already in the log
                Debug.LogWarning($"[{ModInfo.Name}] Couldn't show a message in game: {e.Message}");
            }
            m_pending.Clear();
        }

        // Separate method, so a changed game API fails inside Update's try instead of when Update is compiled
        private static void Send(string message)
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, message);
            if (global::Console.instance) global::Console.instance.Print(message);
        }
    }
}
