using BepInEx;
using HarmonyLib;

namespace Storage
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    // Load after MUC so its patches exist by the time we check them
    [BepInDependency(MucHealth.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    // Patches the same crafting/smelting seams and edits chests it doesn't own
    [BepInIncompatibility("toxo.craftfromchests")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll();
            Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded");
        }

        // Start runs after every plugin's Awake, which is where MUC applies its patches
        private void Start()
        {
            MucHealth.Check(Logger);
        }
    }
}
