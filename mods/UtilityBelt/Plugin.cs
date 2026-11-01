using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using ValheimMods.Shared;

namespace UtilityBelt
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // The belt is a new item, so everyone in a game needs the mod; Jotunn turns away anyone without it
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Settings.Bind(Config);
            // All or nothing: a patch that fails after a game update turns the mod off instead of half applying it
            if (!SafePatching.PatchAll(new Harmony(MyPluginInfo.PLUGIN_GUID), Logger)) return;
            Belt.Register();
            Settings.Changed += RefreshEffects;
            SynchronizationManager.OnConfigurationSynchronized += (_, __) => Settings.Reload();
            gameObject.AddComponent<Toggles>();
            Logger.LogInfo($"{ModInfo.Name} {ModInfo.Version} loaded");
        }

        // Effects follow the settings (a tier turned off, a personal toggle) without re-equipping the belt
        private static void RefreshEffects()
        {
            if (Player.m_localPlayer) Player.m_localPlayer.UpdateEquipmentStatusEffects();
        }
    }
}
