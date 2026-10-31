using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace SearchEverything
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Settings.Bind(Config);
            new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll();
            gameObject.AddComponent<Finder>();
            gameObject.AddComponent<Markers>();
            Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded");
        }
    }
}
