using BepInEx;
using Jotunn.Managers;
using Jotunn.Utils;
using ValheimMods.Shared;

namespace Thunderdome
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // The ward is a new piece, and monsters obey it on whichever client owns them, so everyone in a game needs the mod
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Settings.Bind(Config);
            WardPiece.Register();
            Settings.Changed += WardPiece.ApplyCost;
            Settings.Changed += Ward.RefreshAll;
            SynchronizationManager.OnConfigurationSynchronized += (_, __) => Settings.Reload();
            Logger.LogInfo($"{ModInfo.Name} {ModInfo.Version} loaded");
        }
    }
}
