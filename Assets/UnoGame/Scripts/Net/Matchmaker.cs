using System;
using UnoGame.Core;
using UnityEngine;

namespace UnoGame.Net
{
    /// <summary>
    /// Owns the server connection and finds matches. Search flow:
    ///   connect (if needed) -> "find" -> server seats the players waiting for the same mode and table
    ///   size, and fills the empty seats with bots after a few seconds.
    ///   If the server cannot be reached (or does not answer in time) a local bot match is made instead,
    ///   so pressing Play always ends in a game.
    /// </summary>
    public sealed class Matchmaker : MonoBehaviour
    {
        public enum Phase { Idle, Searching, Matched }

        public Phase Current { get; private set; } = Phase.Idle;
        public GameSession Session { get; private set; }
        public int OnlineCount { get; private set; } = -1;
        public bool ServerOnline => net.IsOpen && welcomed;
        public bool SearchingOnline { get; private set; }
        /// <summary>True until the first connection attempt has finished (home shows "Connecting...").</summary>
        public bool Connecting => connecting || (!attempted && !cfg.forceOffline);
        bool attempted;

        public event Action<GameSession> Matched;
        public event Action StatusChanged;

        readonly NetClient net = new NetClient();
        GameConfig cfg;
        Profile profile;
        bool welcomed;
        bool connecting;
        float searchStart;
        float onlineDeadline = -1;
        float localMatchAt = -1;
        float nextPing;
        float reconnectAt = -1;
        float reconnectGiveUp = -1;
        float nextProbe;

        public void Init(GameConfig config, Profile prof)
        {
            cfg = config;
            profile = prof;
            net.Message += OnNetMessage;
            net.Closed += OnNetClosed;
        }

        float Now => Time.unscaledTime;

        public void Send(string json) => net.Send(json);

        /// <summary>Connect in the background so the home screen can show who is online.</summary>
        public void Probe()
        {
            if (cfg.forceOffline || net.IsOpen || connecting) return;
            Connect(null, null);
        }

        async void Connect(Action<bool> done, string resume)
        {
            if (connecting) { done?.Invoke(false); return; }
            connecting = true;
            welcomed = false;
            bool ok = await net.ConnectAsync(cfg.ServerUrl, cfg.connectTimeoutMs);
            connecting = false;
            attempted = true;
            if (ok) SendHello(resume);
            StatusChanged?.Invoke();
            done?.Invoke(ok);
        }

        public void SendHello(string resume = null)
        {
            if (net.IsOpen) net.Send(Wire.Hello(profile.pid, profile.name, profile.avatar, resume));
        }

        public string SearchMode { get; private set; } = "classic";
        /// <summary>Table size being searched for (2 to 10 players).</summary>
        public int SearchSize { get; private set; } = 2;

        public const int MinPlayers = 2, MaxPlayers = 10;

        public void StartSearch(string mode, int size = 2)
        {
            if (Current == Phase.Searching) return;
            SearchMode = Core.Decks.Normalize(mode);
            SearchSize = Mathf.Clamp(size, MinPlayers, MaxPlayers);
            EndSession();
            Current = Phase.Searching;
            searchStart = Now;
            localMatchAt = -1;
            onlineDeadline = -1;
            SearchingOnline = false;
            if (cfg.forceOffline) { ScheduleLocal(); return; }
            if (ServerOnline) { SendFind(); return; }
            Connect(ok =>
            {
                if (Current != Phase.Searching) return;
                if (ok) SendFind();
                else ScheduleLocal();
            }, null);
        }

        void SendFind()
        {
            SearchingOnline = true;
            onlineDeadline = Now + cfg.onlineMatchTimeoutSec;
            net.Send(Wire.Find(profile.BotLevel, SearchMode, SearchSize));
            Debug.Log("[Match] searching online for " + SearchMode + ", " + SearchSize + " players, bot level " + profile.BotLevel);
            StatusChanged?.Invoke();
        }

        void ScheduleLocal()
        {
            SearchingOnline = false;
            onlineDeadline = -1;
            localMatchAt = Mathf.Max(Now + 0.6f, searchStart + UnityEngine.Random.Range(cfg.localWaitMin, cfg.localWaitMax));
            StatusChanged?.Invoke();
        }

        public void CancelSearch()
        {
            if (Current != Phase.Searching) return;
            if (SearchingOnline && net.IsOpen) net.Send(Wire.Simple("cancel"));
            SearchingOnline = false;
            localMatchAt = -1;
            onlineDeadline = -1;
            Current = Phase.Idle;
            StatusChanged?.Invoke();
        }

        /// <summary>Forget the current match (after it ended or the player left).</summary>
        public void EndSession()
        {
            if (Session != null)
            {
                if (!Session.Finished) Session.Leave();
                Session.Dispose();
            }
            Session = null;
            reconnectAt = -1;
            if (Current == Phase.Matched) Current = Phase.Idle;
        }

        void CompleteMatch(GameSession s)
        {
            Session = s;
            Current = Phase.Matched;
            SearchingOnline = false;
            localMatchAt = -1;
            onlineDeadline = -1;
            var others = new System.Collections.Generic.List<string>();
            for (int i = 0; i < s.Size; i++) if (i != s.Seat) others.Add(s.Players[i].name);
            Debug.Log("[Match] matched (" + (s.IsOnline ? "online" : "offline") + ", " + s.Size + " players) vs " + string.Join(", ", others));
            Matched?.Invoke(s);
            StatusChanged?.Invoke();
        }

        void OnNetMessage(string text)
        {
            ServerMsg m;
            try { m = JsonUtility.FromJson<ServerMsg>(text); }
            catch (Exception) { return; }
            if (m == null || string.IsNullOrEmpty(m.t)) return;

            switch (m.t)
            {
                case "welcome":
                    welcomed = true;
                    OnlineCount = m.online;
                    if (Session is OnlineSession os && reconnectAt >= 0)
                    {
                        reconnectAt = -1;
                        if (m.resumed == 1) { os.Connected = true; os.RaiseConnection(false); }
                        else os.RaiseLost();
                    }
                    StatusChanged?.Invoke();
                    break;
                case "queued":
                case "pong":
                    OnlineCount = m.online;
                    StatusChanged?.Invoke();
                    break;
                case "matched":
                    if (Current == Phase.Searching && SearchingOnline)
                        CompleteMatch(new OnlineSession(this, m));
                    else if (Session is OnlineSession cur && cur.MatchId == m.match && m.players != null)
                        cur.UpdatePlayers(m.players);
                    break;
                default:
                    if (Session is OnlineSession online) online.Deliver(m);
                    break;
            }
        }

        void OnNetClosed()
        {
            welcomed = false;
            Debug.Log("[Net] connection closed");
            if (Current == Phase.Searching && SearchingOnline) ScheduleLocal();
            if (Session is OnlineSession os && !os.Finished)
            {
                os.Connected = false;
                os.RaiseConnection(true);
                reconnectAt = Now + 1f;
                reconnectGiveUp = Now + 25f;
            }
            StatusChanged?.Invoke();
        }

        void Update()
        {
            float now = Now;
            if (Current == Phase.Searching)
            {
                if (localMatchAt > 0 && now >= localMatchAt) CompleteMatch(MakeLocal());
                else if (SearchingOnline && onlineDeadline > 0 && now >= onlineDeadline)
                {
                    Debug.Log("[Match] server did not match in time, playing offline");
                    if (net.IsOpen) net.Send(Wire.Simple("cancel"));
                    ScheduleLocal();
                }
            }

            Session?.Tick();

            if (reconnectAt > 0 && Session is OnlineSession os && !os.Finished)
            {
                if (now > reconnectGiveUp) { reconnectAt = -1; os.RaiseLost(); }
                else if (now >= reconnectAt && !connecting)
                {
                    reconnectAt = now + 3f;
                    Connect(null, os.MatchId);
                }
            }

            if (net.IsOpen && now >= nextPing)
            {
                nextPing = now + 20f;
                net.Send(Wire.Simple("ping"));
            }
            if (!net.IsOpen && Current == Phase.Idle && !cfg.forceOffline && now >= nextProbe)
            {
                nextProbe = now + 15f;
                Probe();
            }
        }

        LocalSession MakeLocal()
        {
            var players = new PlayerInfo[SearchSize];
            players[0] = new PlayerInfo { name = profile.name, avatar = profile.avatar };
            var avatars = new System.Collections.Generic.List<int> { profile.avatar };
            var names = new System.Collections.Generic.List<string> { profile.name };
            for (int i = 1; i < players.Length; i++)
            {
                int avatar;
                do { avatar = UnityEngine.Random.Range(0, Avatars.Count); } while (avatars.Contains(avatar) && avatars.Count < Avatars.Count);
                avatars.Add(avatar);
                string name = BotNames.Pick(names);
                names.Add(name);
                players[i] = new PlayerInfo { name = name, avatar = avatar };
            }
            Debug.Log("[Match] offline " + SearchMode + ", " + SearchSize + " players, bot level " + profile.BotLevel);
            return new LocalSession(cfg, players, profile.BotLevel, SearchMode);
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused && !net.IsOpen && Session is OnlineSession os && !os.Finished && reconnectAt < 0)
            {
                os.Connected = false;
                os.RaiseConnection(true);
                reconnectAt = Now;
                reconnectGiveUp = Now + 25f;
            }
        }

        void OnDestroy() => net.Dispose();
    }

    public static class Avatars
    {
        public const int Count = 12;
    }

    public static class BotNames
    {
        static readonly string[] Names =
        {
            "Maya_21", "Rohan77", "LunaPlays", "Arjun.K", "Zoe99", "Kabir_xo", "NinjaCat", "Priya.S", "LeoWins",
            "Aisha23", "Dev_Rocks", "Sana.M", "Max_Ace", "Ishaan07", "Nora_UNO", "Vikram_V", "Ella.B", "Kian88",
            "Riya_Star", "Omar.Q", "Tara_Lee", "Aryan_9", "Mila_Moon", "Neil.P", "Jade_Fox", "Sid_Smash",
        };

        public static string Pick() => Names[UnityEngine.Random.Range(0, Names.Length)];

        /// <summary>A name nobody at the table has yet.</summary>
        public static string Pick(System.Collections.Generic.ICollection<string> taken)
        {
            var free = new System.Collections.Generic.List<string>();
            foreach (var n in Names) if (!taken.Contains(n)) free.Add(n);
            return free.Count > 0 ? free[UnityEngine.Random.Range(0, free.Count)] : Pick();
        }
    }
}
