using BepInEx;
using HarmonyLib;

namespace LootFilter
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Settings.Bind(Config);
            Rules.Init();
            new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll();
            gameObject.AddComponent<Marker>();
            gameObject.AddComponent<DropEffects>();
            Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded");
        }
    }
}
