using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;

namespace CraftFromStorage
{
    // Action journal: every item taken from or fetched out of a chest, so a test session can be audited afterwards
    internal static class StorageLog
    {
        private static ManualLogSource s_log;
        private static string s_path;

        public static void Init(ManualLogSource log)
        {
            s_log = log;
            s_path = Path.Combine(Paths.BepInExRootPath, "CraftFromStorage.log");
        }

        public static void Info(string message) => s_log?.LogInfo(message);
        public static void Warning(string message) => s_log?.LogWarning(message);

        public static void Action(string message)
        {
            if (Settings.LogActions.Value) Write(message);
        }

        /// <summary>Always written (e.g. `storage audit`, which the player asked for explicitly).</summary>
        public static void Write(string message)
        {
            if (s_path == null) return;
            try
            {
                File.AppendAllText(s_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch (Exception e)
            {
                s_log?.LogWarning($"Couldn't write {s_path}: {e.Message}");
            }
        }
    }
}
