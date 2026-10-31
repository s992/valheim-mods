using System;
using System.IO;
using BepInEx.Logging;

namespace EverythingsANail
{
    // Sends the server's tool rules to clients so a server owner decides which tools are allowed.
    // The server pushes its rules once a client has connected and again whenever they change;
    // clients keep them in memory only and never write them to their own config file.
    internal static class ConfigSync
    {
        private const string RpcName = "s992.EverythingsANail.Rules";
        private const int Format = 1;

        private static ManualLogSource s_log;
        private static Rules s_remote;
        private static ZNet s_remoteNet;

        public static void Init(ManualLogSource log) => s_log = log;

        // Rules received from the server of the current session, or null (single player, hosting,
        // or a server without the mod). Tied to the ZNet instance so they're dropped on logout.
        public static Rules Remote => s_remote != null && ZNet.instance != null && s_remoteNet == ZNet.instance ? s_remote : null;

        private static bool IsServer => ZNet.instance != null && ZNet.instance.IsServer();

        // Both sides: every new connection can carry the rules
        public static void OnNewConnection(ZNetPeer peer) => peer.m_rpc.Register<ZPackage>(RpcName, OnReceive);

        // Server: the client has introduced itself, so its handler is registered
        public static void OnPeerInfo(ZNetPeer peer)
        {
            if (IsServer && peer != null) SendTo(peer);
        }

        public static void Broadcast()
        {
            if (!IsServer) return;
            foreach (ZNetPeer peer in ZNet.instance.GetPeers()) SendTo(peer);
        }

        private static void SendTo(ZNetPeer peer)
        {
            if (peer?.m_rpc == null) return;
            peer.m_rpc.Invoke(RpcName, Write(Settings.Local));
        }

        // The wire format. Change it only together with Format: clients reject any other format.
        public static ZPackage Write(Rules rules)
        {
            var pkg = new ZPackage();
            pkg.Write(Format);
            pkg.Write((int)rules.Categories);
            pkg.Write(Rules.FormatList(rules.OnlyAllow));
            pkg.Write(Rules.FormatList(rules.Exclude));
            return pkg;
        }

        /// <returns>The rules, or null with a reason when the package is another format or cut short.</returns>
        public static Rules TryRead(ZPackage pkg, out string problem)
        {
            problem = null;
            try
            {
                int format = pkg.ReadInt();
                if (format != Format)
                {
                    problem = $"Server sent tool rules in format {format}, expected {Format}; update the mod on both sides";
                    return null;
                }
                return new Rules
                {
                    Categories = (ToolCategories)pkg.ReadInt(),
                    OnlyAllow = Rules.ParseList(pkg.ReadString()),
                    Exclude = Rules.ParseList(pkg.ReadString()),
                };
            }
            catch (Exception e) when (e is EndOfStreamException || e is IOException)
            {
                problem = $"Server sent tool rules that couldn't be read ({e.Message})";
                return null;
            }
        }

        private static void OnReceive(ZRpc rpc, ZPackage pkg)
        {
            if (ZNet.instance == null || IsServer) return;
            Rules rules = TryRead(pkg, out string problem);
            if (rules == null)
            {
                s_log.LogWarning($"{problem}. Using local rules.");
                return;
            }
            s_remote = rules;
            s_remoteNet = ZNet.instance;
            s_log.LogInfo($"Using the server's tool rules: categories {s_remote.Categories}, " +
                          $"only allow [{Rules.FormatList(s_remote.OnlyAllow)}], exclude [{Rules.FormatList(s_remote.Exclude)}]");
            Settings.NotifyRulesChanged();
        }
    }
}
