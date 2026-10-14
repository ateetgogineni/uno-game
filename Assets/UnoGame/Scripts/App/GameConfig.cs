using System;
using UnityEngine;

namespace UnoGame
{
    /// <summary>Tunable settings. Edit Assets/UnoGame/Resources/GameConfig.asset in the editor.</summary>
    [CreateAssetMenu(menuName = "UNO Game/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
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
                if (args[i] == "-server" && i + 1 < args.Length) cfg.serverOverride = args[i + 1];
            }
            return cfg;
        }
    }
}
