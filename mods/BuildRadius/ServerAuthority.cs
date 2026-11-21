using Jotunn.Utils;
using ValheimMods.Shared;

namespace BuildRadius
{
    // Whether this game's settings come from somewhere with the authority to set them.
    //
    // The build check runs on each client, so the radius has to be the server's. Jotunn resets admin-only settings to
    // their defaults when a client connects and then applies whatever the server sends, but a server without the mod
    // sends nothing, which would leave a client on this mod's defaults (twice the radius) on a vanilla server. So a
    // client keeps the game's radius until the server's values for this mod have arrived.
    internal static class ServerAuthority
    {
        // The connection the server's values came over; a new ZNet (a new connection) starts unconfirmed
        private static ZNet s_syncedFrom;

        /// <summary>
        /// True in single player, when hosting, on a dedicated server, and on a client once its server sent this mod's
        /// settings.
        /// </summary>
        public static bool Active
        {
            get
            {
                ZNet znet = ZNet.instance;
                if (!znet || znet.IsServer()) return true;
                return s_syncedFrom == znet;
            }
        }

        public static void OnSynchronized(ConfigurationSynchronizationEventArgs args)
        {
            if (args?.UpdatedPluginGUIDs == null || !args.UpdatedPluginGUIDs.Contains(MyPluginInfo.PLUGIN_GUID)) return;
            ZNet znet = ZNet.instance;
            if (!znet || znet.IsServer() || s_syncedFrom == znet) return;
            s_syncedFrom = znet;
            ModInfo.Log.LogInfo("Using the server's build radius settings");
        }
    }
}
