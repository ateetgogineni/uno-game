using System;
using System.Collections.Generic;
using System.Text;
using UnoGame.Core;
using UnityEngine;

namespace UnoGame.Net
{
    /// <summary>
    /// One match, online (server-run) or local (in-process engine + bot). The game scene only talks to
    /// this class: it sends intents and drains Inbox, so both kinds of match look identical to the UI.
    /// </summary>
    public abstract class GameSession
    {
        public readonly Queue<ServerMsg> Inbox = new Queue<ServerMsg>();
        public PlayerInfo[] Players;
        public int Seat;
        public string MatchId;
        public bool Finished;

        /// <summary>(seat, emote id) — delivered immediately, not queued behind animations.</summary>
        public event Action<int, int> EmoteReceived;
        /// <summary>Fired when the connection drops (true) or comes back (false).</summary>
        public event Action<bool> ConnectionChanged;
        /// <summary>Fired when an online match cannot be recovered.</summary>
        public event Action Lost;
        /// <summary>Fired when the opponent seat changes (a player left before the start and a bot took over).</summary>
        public event Action PlayersChanged;

        public abstract bool IsOnline { get; }
        public int OpponentSeat => 1 - Seat;
        public PlayerInfo Me => Players[Seat];
        public PlayerInfo Opponent => Players[OpponentSeat];

        public abstract void Ready();
        public abstract void Play(int card, int color, bool uno);
        public abstract void Draw();
        public abstract void Pass();
        public abstract void Emote(int e);
        public abstract void Leave();
        public abstract void RequestSync();
        public virtual void Tick() { }
        public virtual void Dispose() { }

        internal void Deliver(ServerMsg m)
        {
            m.receivedAt = Time.unscaledTime;
            if (m.t == "emote") { EmoteReceived?.Invoke(m.p, m.e); return; }
            if (m.t == "up" && m.st != null && m.st.over == 1) Finished = true;
            Inbox.Enqueue(m);
        }

        internal void RaiseConnection(bool lost) => ConnectionChanged?.Invoke(lost);
        internal void RaiseLost() => Lost?.Invoke();
        internal void RaisePlayersChanged() => PlayersChanged?.Invoke();
    }

    /// <summary>Hand-written JSON for the few client messages (keeps the payload minimal).</summary>
    public static class Wire
    {
        public static string Simple(string t) => "{\"t\":\"" + t + "\"}";

        public static string Play(int card, int color, bool uno) =>
            "{\"t\":\"play\",\"card\":" + card + ",\"color\":" + color + ",\"uno\":" + (uno ? "true" : "false") + "}";

        public static string Emote(int e) => "{\"t\":\"emote\",\"e\":" + e + "}";

        public static string Hello(string pid, string name, int avatar, string resume)
        {
            var sb = new StringBuilder("{\"t\":\"hello\",\"pid\":");
            Str(sb, pid);
            sb.Append(",\"name\":");
            Str(sb, name);
            sb.Append(",\"avatar\":").Append(avatar);
            if (!string.IsNullOrEmpty(resume)) { sb.Append(",\"resume\":"); Str(sb, resume); }
            sb.Append('}');
            return sb.ToString();
        }

        static void Str(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c >= 32 && c < 127) sb.Append(c);
            }
            sb.Append('"');
        }
    }

    public sealed class OnlineSession : GameSession
    {
        readonly Matchmaker owner;
        public bool Connected = true;

        public OnlineSession(Matchmaker owner, ServerMsg matched)
        {
            this.owner = owner;
            MatchId = matched.match;
            Seat = matched.seat;
            Players = matched.players;
        }

        public override bool IsOnline => true;

        internal void UpdatePlayers(PlayerInfo[] players)
        {
            Players = players;
            RaisePlayersChanged();
        }

        public override void Ready() => owner.Send(Wire.Simple("ready"));
        public override void Play(int card, int color, bool uno) => owner.Send(Wire.Play(card, color, uno));
        public override void Draw() => owner.Send(Wire.Simple("draw"));
        public override void Pass() => owner.Send(Wire.Simple("pass"));
        public override void Emote(int e) => owner.Send(Wire.Emote(e));
        public override void RequestSync() => owner.Send(Wire.Simple("sync"));

        public override void Leave()
        {
            if (!Finished) owner.Send(Wire.Simple("leave"));
            Finished = true;
        }
    }
}
