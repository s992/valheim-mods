using BepInEx;
using HarmonyLib;
using ValheimMods.Shared;

namespace __MODNAME__
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            // All or nothing: a patch that fails after a game update turns the mod off instead of half applying it
            if (!SafePatching.PatchAll(new Harmony(MyPluginInfo.PLUGIN_GUID), Logger)) return;
            Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded");
        }
    }
}
