using BepInEx;
using HarmonyLib;
using ValheimMods.Shared;

namespace StabilityView
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Settings.Bind(Config);
            if (!SafePatching.PatchAll(new Harmony(MyPluginInfo.PLUGIN_GUID), Logger)) return;
            gameObject.AddComponent<Overlay>();
            Logger.LogInfo($"{ModInfo.Name} {ModInfo.Version} loaded");
        }
    }
}
