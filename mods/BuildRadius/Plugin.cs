using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using ValheimMods.Shared;

namespace BuildRadius
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // Versions must match when both sides have it. Vanilla clients may join a server with it (they keep the game's
    // radius), and a client with it keeps the game's radius on a server without it (ServerAuthority).
    [NetworkCompatibility(CompatibilityLevel.VersionCheckOnly, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Settings.Bind(Config);
            // All or nothing: a patch that fails after a game update turns the mod off instead of half applying it
            if (!SafePatching.PatchAll(new Harmony(MyPluginInfo.PLUGIN_GUID), Logger)) return;
            Settings.Changed += Stations.ApplyAll;
            SynchronizationManager.OnConfigurationSynchronized += (_, args) =>
            {
                ServerAuthority.OnSynchronized(args);
                Settings.Reload();
            };
            Logger.LogInfo($"{ModInfo.Name} {ModInfo.Version} loaded");
        }
    }
}
