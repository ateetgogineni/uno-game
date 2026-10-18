using System;
using UnityEngine;

namespace UnoGame
{
    /// <summary>Tunable settings. Edit Assets/UnoGame/Resources/GameConfig.asset in the editor.</summary>
    [CreateAssetMenu(menuName = "UNO Game/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        public enum Network { Photon, Server }

        [Tooltip("Photon: players meet through Photon Cloud and one phone runs each table (no server to host). Server: the Node server in Server/.")]
        public Network network = Network.Photon;
        [Tooltip("Photon Realtime App ID (dashboard.photonengine.com).")]
        public string photonAppId = "b9d13c06-8aa1-4f19-9fc0-fd7267f92b85";
        [Tooltip("Photon region code (e.g. in, eu, us); empty = the closest region.")]
        public string photonRegion = "";
        [Tooltip("Photon: wait this long for other people before bots fill the empty seats.")]
        public float botAfterSec = 6f;

        [Tooltip("WebSocket address of the matchmaking server, e.g. wss://uno.example.com/")]
        public string serverUrl = "ws://192.168.29.30:8090/";
        public int connectTimeoutMs = 3500;
        [Tooltip("If the server has not matched us after this long, play an offline bot match instead.")]
        public float onlineMatchTimeoutSec = 16f;
        [Tooltip("Offline fallback: how long the matchmaking animation runs before the bot 'joins'.")]
        public float localWaitMin = 3.2f;
        public float localWaitMax = 5.5f;
        public float turnSeconds = 15f;
        public int maxTimeouts = 3;
        [Tooltip("+2/+4 stacking, as in the reference game. Must match the server's STACKING setting.")]
        public bool stacking = true;

        [NonSerialized] public bool forceOffline;
        [NonSerialized] public string serverOverride;

        public string ServerUrl => string.IsNullOrEmpty(serverOverride) ? serverUrl : serverOverride;

        public static GameConfig Load()
        {
            var asset = Resources.Load<GameConfig>("GameConfig");
            var cfg = asset != null ? Instantiate(asset) : CreateInstance<GameConfig>();
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-offline") cfg.forceOffline = true;
                if (args[i] == "-server" && i + 1 < args.Length) { cfg.serverOverride = args[i + 1]; cfg.network = Network.Server; }
                if (args[i] == "-photon") cfg.network = Network.Photon;
                if (args[i] == "-region" && i + 1 < args.Length) cfg.photonRegion = args[i + 1];
            }
            return cfg;
        }
    }
}
