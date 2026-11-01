using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using ValheimMods.Shared;

namespace EverythingsANail
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private FileSystemWatcher m_watcher;
        // Set from the watcher's thread; the reload happens on the main thread
        private volatile bool m_configFileChanged;
        private float m_reloadAt = -1f;

        private void Awake()
        {
            Log = Logger;
            Settings.Bind(Config);
            ConfigSync.Init(Logger);
            Settings.RulesChanged += ToolSwitcher.Revalidate;
            if (!SafePatching.PatchAll(new Harmony(MyPluginInfo.PLUGIN_GUID), Logger)) return;
            WatchConfigFile();
            Logger.LogInfo($"{ModInfo.Name} {ModInfo.Version} loaded");
        }

        // Dedicated servers have no Configuration Manager: pick up edits to the config file so a
        // server owner can change the rules without a restart (they're sent to everyone connected)
        private void WatchConfigFile()
        {
            string path = Config.ConfigFilePath;
            m_watcher = new FileSystemWatcher(Path.GetDirectoryName(path), Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            };
            m_watcher.Changed += (_, __) => m_configFileChanged = true;
            m_watcher.Created += (_, __) => m_configFileChanged = true;
            m_watcher.Renamed += (_, __) => m_configFileChanged = true;
            m_watcher.EnableRaisingEvents = true;
        }

        private void Update()
        {
            if (m_configFileChanged)
            {
                m_configFileChanged = false;
                // Editors often write a file in several steps
                m_reloadAt = Time.unscaledTime + 1f;
            }
            if (m_reloadAt >= 0f && Time.unscaledTime >= m_reloadAt)
            {
                m_reloadAt = -1f;
                if (File.Exists(Config.ConfigFilePath)) Config.Reload();
            }
        }

        private void OnDestroy()
        {
            m_watcher?.Dispose();
        }
    }
}
