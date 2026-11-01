using System;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using ValheimMods.Shared;

namespace MarkerCommand
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(Hints.ServerDevcommandsGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            if (!SafePatching.PatchAll(new Harmony(MyPluginInfo.PLUGIN_GUID), Logger)) return;

            // Parameter hints only exist when Server Devcommands is installed
            if (Chainloader.PluginInfos.ContainsKey(Hints.ServerDevcommandsGuid))
            {
                try
                {
                    if (Hints.Register())
                        Logger.LogInfo("Registered parameter hints with Server Devcommands");
                    else
                        Logger.LogWarning("Server Devcommands has no compatible AutoComplete.Register; hints disabled");
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"Could not register parameter hints: {e.Message}");
                }
            }

            Logger.LogInfo($"{ModInfo.Name} {ModInfo.Version} loaded");
        }
    }
}
