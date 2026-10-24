using BepInEx;
using HarmonyLib;

namespace Storage
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    // Load after MUC (its patches must exist when we check them) and Epic Loot (its magic-item check)
    [BepInDependency(MucHealth.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(EpicLootCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    // Patches the same crafting/smelting seams and edits chests it doesn't own
    [BepInIncompatibility("toxo.craftfromchests")]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony m_harmony;

        private void Awake()
        {
            Settings.Bind(Config);
            StorageLog.Init(Logger);
            EpicLootCompat.Init(Logger);
            m_harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            m_harmony.PatchAll();
            gameObject.AddComponent<Runner>();
            Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded");
        }

        // Start runs after every plugin's Awake, which is where MUC applies its patches
        private void Start()
        {
            MucHealth.Check(Logger);
            MucInternals.Init(Logger);
        }
    }
}
