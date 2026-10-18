using System;
using System.Collections.Generic;
using Photon.Realtime;
using UnoGame.Core;
using UnityEngine;

namespace UnoGame.Net
{
    /// <summary>Who sits where, sent by the host when a Photon table starts (actor 0 = a bot seat).</summary>
    [Serializable]
    public sealed class TableInfo
    {
        public string match;
        public string mode;
        public PlayerInfo[] players;
        public int[] actors;
        public int[] levels;
    }

    /// <summary>What another phone needs to take over the table if the host leaves.</summary>
    [Serializable]
    public sealed class HostState
    {
        public TableInfo table;
        public bool started;
        public EngineSnapshot game;
        public int hostActor;
    }

    /// <summary>
    /// A match over Photon Cloud. The room's master client runs the table (TableHost: rules, bots, timers)
    /// and sends each player only what their seat may see; everyone else sends moves to it. The host shares
    /// a snapshot after every move, so if it leaves, the next master client carries on the same game.
    /// </summary>
    public sealed class PhotonSession : GameSession
    {
        readonly PhotonLink link;
        readonly GameConfig cfg;
        readonly TableInfo table;
        TableHost host;
        HostState lastState;
        readonly HashSet<int> ready = new HashSet<int>();
        float startBy = -1f;

        public bool IsHost => host != null;
        public override bool IsOnline => true;
        public int MyActor => table.actors[Seat];

        public PhotonSession(PhotonLink link, GameConfig cfg, TableInfo table, int myActor, bool isHost)
        {
            this.link = link;
            this.cfg = cfg;
            this.table = table;
            MatchId = table.match;
            Mode = Decks.Normalize(table.mode);
            Players = table.players;
            Seat = Array.IndexOf(table.actors, myActor);
            if (isHost) CreateHost();
        }

        static float Now => Time.unscaledTime;

        int SeatOfActor(int actor) => actor > 0 ? Array.IndexOf(table.actors, actor) : -1;

        void CreateHost()
        {
            var seats = new TableHost.Seat[table.players.Length];
            for (int i = 0; i < seats.Length; i++)
                seats[i] = new TableHost.Seat { info = table.players[i], bot = table.actors[i] == 0, level = table.levels[i] };
            host = new TableHost(cfg, seats, Mode, MatchId, SendToSeat);
            host.Changed += ShareSnapshot;
            startBy = Now + 6f;
            ready.Clear();
        }

        // ---------------------------------------------------------------- host side

        void SendToSeat(int seat, ServerMsg m)
        {
            if (seat == Seat) { Deliver(m); return; }
            int actor = table.actors[seat];
            if (actor > 0) link.SendTo(actor, PhotonLink.EvToSeat, JsonUtility.ToJson(m));
        }

        void ShareSnapshot()
        {
            if (host == null) return;
            var state = new HostState { table = table, started = host.Started, game = host.Started ? host.Game.Snapshot() : null, hostActor = MyActor };
            link.SendToOthers(PhotonLink.EvSnapshot, JsonUtility.ToJson(state));
        }

        void MarkReady(int seat)
        {
            if (seat < 0) return;
            ready.Add(seat);
            if (host.Started) { host.SendSync(seat); return; }
            for (int i = 0; i < table.actors.Length; i++)
                if (table.actors[i] > 0 && !ready.Contains(i) && link.IsInRoom(table.actors[i])) return;
            StartGame();
        }

        void StartGame()
        {
            if (host == null || host.Started) return;
            host.Start(UnityEngine.Random.Range(1, int.MaxValue));
        }

        /// <summary>Photon event for this match (moves to the host, updates from it, snapshots).</summary>
        public void OnEvent(byte code, int sender, string json)
        {
            switch (code)
            {
                case PhotonLink.EvToHost:
                    if (host == null) return;
                    int seat = SeatOfActor(sender);
                    if (seat < 0) return;
                    if (json.Contains("\"t\":\"ready\"")) MarkReady(seat);
                    else host.Handle(seat, json);
                    break;
                case PhotonLink.EvToSeat:
                    if (host != null) return;
                    var m = JsonUtility.FromJson<ServerMsg>(json);
                    if (m != null) Deliver(m);
                    break;
                case PhotonLink.EvSnapshot:
                    if (host != null) return;
                    lastState = JsonUtility.FromJson<HostState>(json);
                    break;
            }
        }

        public void PlayerLeft(Player p)
        {
            if (host == null) return;
            int seat = SeatOfActor(p.ActorNumber);
            if (seat < 0 || seat == Seat) return;
            if (p.IsInactive) host.SetConnected(seat, false);      // may come back (phone locked, network blip)
            else if (host.Started) host.Leave(seat, "left");
            else MarkReadyCheck();
        }

        void MarkReadyCheck()
        {
            if (host == null || host.Started) return;
            for (int i = 0; i < table.actors.Length; i++)
                if (table.actors[i] > 0 && !ready.Contains(i) && link.IsInRoom(table.actors[i])) return;
            StartGame();
        }

        public void PlayerReturned(Player p)
        {
            if (host == null) return;
            int seat = SeatOfActor(p.ActorNumber);
            if (seat >= 0 && host.Started) host.SetConnected(seat, true);
        }

        /// <summary>The host left: if this phone is the new master client it carries on the game.</summary>
        public void MasterSwitched(Player newMaster)
        {
            if (!newMaster.IsLocal)
            {
                // somebody else hosts now; if this phone was hosting (it dropped out and came back), stop
                if (host != null) { host = null; RequestSync(); }
                return;
            }
            if (host != null || Finished) return;
            if (lastState != null && lastState.game != null && lastState.game.over) return;   // nothing left to run
            Debug.Log("[Photon] taking over as host of " + MatchId);
            CreateHost();
            if (lastState != null && lastState.started && lastState.game != null)
            {
                host.Resume(lastState.game);
                int old = SeatOfActor(lastState.hostActor);
                if (old >= 0 && old != Seat && !link.IsInRoom(lastState.hostActor)) host.Leave(old, "left");
            }
            else
            {
                ready.Add(Seat);
                StartGame();
            }
        }

        // ---------------------------------------------------------------- moves from this phone

        bool Playing => host != null && host.Started && !host.Game.Over;

        void ToHost(string json) => link.SendToHost(PhotonLink.EvToHost, json);

        public override void Ready()
        {
            if (host != null) MarkReady(Seat);
            else ToHost(Wire.Simple("ready"));
        }

        public override void Play(int card, int color, bool uno, int target = -1)
        {
            if (host != null) { if (Playing) host.Play(Seat, card, color, uno, target); }
            else ToHost(Wire.Play(card, color, uno, target));
        }

        public override void Draw()
        {
            if (host != null) { if (Playing) host.Act(Seat, () => host.Game.Draw(Seat)); }
            else ToHost(Wire.Simple("draw"));
        }

        public override void Pass()
        {
            if (host != null) { if (Playing) host.Act(Seat, () => host.Game.Pass(Seat)); }
            else ToHost(Wire.Simple("pass"));
        }

        public override void Roulette(int color)
        {
            if (host != null) { if (Playing) host.Act(Seat, () => host.Game.Roulette(Seat, color)); }
            else ToHost(Wire.Roulette(color));
        }

        public override void Dare(bool ok)
        {
            if (host != null) { if (Playing) host.Act(Seat, () => host.Game.Dare(Seat, ok)); }
            else ToHost(Wire.Dare(ok));
        }

        public override void Emote(int e)
        {
            if (host != null) host.Emote(Seat, e);
            else ToHost(Wire.Emote(e));
        }

        public override void RequestSync()
        {
            if (host != null) host.SendSync(Seat);
            else ToHost(Wire.Simple("sync"));
        }

        public override void Leave()
        {
            if (!Finished)
            {
                if (host != null) { if (Playing) host.Leave(Seat, "left"); }
                else ToHost(Wire.Simple("leave"));
            }
            Finished = true;
        }

        public override void Tick()
        {
            if (host == null) return;
            if (!host.Started && startBy > 0f && Now >= startBy) StartGame();
            host.Tick();
        }

        public override void Dispose() => link.EndSession(this);

        internal void ConnectionDropped() => RaiseConnection(true);

        internal void ConnectionBack()
        {
            RaiseConnection(false);
            RequestSync();
        }

        internal void ConnectionLost() => RaiseLost();
    }
}
