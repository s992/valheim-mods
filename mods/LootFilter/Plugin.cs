using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using ValheimMods.Shared;

namespace LootFilter
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(BuildCameraPickupPatch.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Settings.Bind(Config);
            Rules.Init();

            var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            if (!SafePatching.PatchAll(harmony, Logger)) return;
            if (Chainloader.PluginInfos.ContainsKey(BuildCameraPickupPatch.Guid))
                BuildCameraPickupPatch.Apply(harmony, Logger);

            gameObject.AddComponent<Marker>();
            gameObject.AddComponent<DropEffects>();
            Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded");
        }
    }
}
