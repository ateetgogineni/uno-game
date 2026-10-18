using System;
using System.Collections.Generic;
using UnoGame.Core;
using UnityEngine;

namespace UnoGame.Net
{
    /// <summary>
    /// Runs one table on this device: the rules engine, the bots, turn timers and what each seat may see.
    /// A C# port of Server/src/match.js speaking the same messages, so the game screen can't tell who hosts.
    /// Used for offline games (you + bots) and by the Photon host (people on other phones + bots).
    /// </summary>
    public sealed class TableHost
    {
        public sealed class Seat
        {
            public PlayerInfo info;
            public bool bot;
            public int level = UnoBot.Normal;
            public bool connected = true;
            public int timeouts;
            public float lastEmote = -10f;
        }

        public readonly Seat[] Seats;
        public readonly string Mode;
        public UnoEngine Game { get; private set; }
        public bool Started => Game != null;
        public string MatchId { get; }

        readonly GameConfig cfg;
        readonly Action<int, ServerMsg> deliver;    // a message for one person's seat
        float deadline;
        float turnLen;
        float botActAt = -1f;
        bool finishedNotified;
        readonly List<(float at, int seat, int emote)> botEmotes = new List<(float, int, int)>();

        /// <summary>Raised once when the game is over (the host can close its room).</summary>
        public event Action Finished;
        /// <summary>Raised after every change to the game (the Photon host shares a snapshot for handover).</summary>
        public event Action Changed;

        public TableHost(GameConfig cfg, Seat[] seats, string mode, string matchId, Action<int, ServerMsg> deliver)
        {
            this.cfg = cfg;
            Seats = seats;
            Mode = Decks.Normalize(mode);
            MatchId = matchId;
            this.deliver = deliver;
        }

        public int Size => Seats.Length;
        public PlayerInfo[] Players
        {
            get
            {
                var p = new PlayerInfo[Size];
                for (int i = 0; i < Size; i++) p[i] = Seats[i].info;
                return p;
            }
        }

        static float Now => Time.unscaledTime;

        public void Start(int seed)
        {
            if (Started) return;
            Game = new UnoEngine(Size, seed, cfg.stacking, Mode);
            Game.Start();
            Flush();
        }

        /// <summary>Host handover: carry on a game another phone was running.</summary>
        public void Resume(EngineSnapshot snapshot)
        {
            Game = UnoEngine.Restore(snapshot);
            ArmTimers(0f);
            for (int s = 0; s < Size; s++) SendSync(s);
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- intents

        /// <summary>A move or message from a person at the table (same JSON as the Node server takes).</summary>
        public void Handle(int seat, string json)
        {
            var m = new ClientMsg();
            try { JsonUtility.FromJsonOverwrite(json, m); }
            catch (Exception) { return; }
            switch (m.t)
            {
                case "play": Play(seat, m.card, m.color, m.uno, m.target); break;
                case "draw": Act(seat, () => Game.Draw(seat)); break;
                case "pass": Act(seat, () => Game.Pass(seat)); break;
                case "roulette": Act(seat, () => Game.Roulette(seat, m.color)); break;
                case "dare": Act(seat, () => Game.Dare(seat, m.ok)); break;
                case "emote": Emote(seat, m.e); break;
                case "sync": SendSync(seat); break;
                case "leave": Leave(seat, "left"); break;
            }
        }

        public void Play(int seat, int card, int color, bool uno, int target)
        {
            if (!Started || Game.Over) return;
            // A forgotten UNO goes unpunished only if nobody notices: every opponent is a bot and each one misses it.
            bool forgive = !uno;
            for (int i = 0; i < Size && forgive; i++)
            {
                if (i == seat || Game.Out[i]) continue;
                if (!Seats[i].bot || UnityEngine.Random.value < UnoBot.CatchChance[UnoBot.ClampLevel(Seats[i].level)]) forgive = false;
            }
            Act(seat, () => Game.Play(seat, card, color, uno, forgive, target));
        }

        public void Act(int seat, Func<string> move)
        {
            if (!Started || Game.Over) return;
            string err = move();
            if (err != null)
            {
                Send(seat, new ServerMsg { t = "err", msg = err });
                SendSync(seat);
                return;
            }
            Seats[seat].timeouts = 0;
            Flush();
        }

        public void Emote(int seat, int e)
        {
            var s = Seats[seat];
            if (Now - s.lastEmote < 1.5f || e < 0 || e >= Emotes.Count) return;
            s.lastEmote = Now;
            for (int i = 0; i < Size; i++) if (i != seat) Send(i, new ServerMsg { t = "emote", p = seat, e = e });
            // a bot sometimes answers
            var bots = new List<int>();
            for (int i = 0; i < Size; i++) if (Seats[i].bot && (Game == null || !Game.Out[i])) bots.Add(i);
            if (bots.Count > 0 && UnityEngine.Random.value < 0.4f)
            {
                int[] replies = { Emotes.Happy, Emotes.Laugh, Emotes.Cool, Emotes.Wow };
                botEmotes.Add((Now + UnityEngine.Random.Range(1.0f, 1.8f), bots[UnityEngine.Random.Range(0, bots.Count)], replies[UnityEngine.Random.Range(0, replies.Length)]));
            }
        }

        /// <summary>A person left (or their phone dropped out for good): out of the game, or the other player wins a 1v1.</summary>
        public void Leave(int seat, string reason)
        {
            Seats[seat].connected = false;
            if (!Started || Game.Over || Game.Out[seat]) return;
            Game.Forfeit(seat, reason);
            Flush();
        }

        /// <summary>A person's connection dropped but may come back: their turns get shorter meanwhile.</summary>
        public void SetConnected(int seat, bool on)
        {
            Seats[seat].connected = on;
            if (on) SendSync(seat);
        }

        // ---------------------------------------------------------------- output

        void Send(int seat, ServerMsg m)
        {
            if (seat < 0 || seat >= Size || Seats[seat].bot) return;
            deliver(seat, m);
        }

        St StateFor(int seat)
        {
            var st = Game.View(seat);
            st.ms = Game.Over ? 0 : Mathf.RoundToInt(Mathf.Max(0f, deadline - Now) * 1000f);
            st.turnMs = Mathf.RoundToInt(turnLen * 1000f);
            return st;
        }

        public void SendSync(int seat)
        {
            if (!Started) return;
            Send(seat, new ServerMsg { t = "sync", match = MatchId, seat = seat, mode = Mode, players = Players, st = StateFor(seat) });
        }

        void Flush()
        {
            var events = Game.TakeEvents();
            ArmTimers(AnimSeconds(events, Size));
            for (int i = 0; i < Size; i++)
            {
                if (Seats[i].bot) continue;
                Send(i, new ServerMsg { t = "up", ev = UnoEngine.EventsFor(i, events).ToArray(), st = StateFor(i) });
            }
            BotReactions(events);
            Changed?.Invoke();
            if (Game.Over && !finishedNotified)
            {
                finishedNotified = true;
                Finished?.Invoke();
            }
        }

        void ArmTimers(float anim)
        {
            botActAt = -1f;
            if (Game.Over) { deadline = 0f; return; }
            var s = Seats[Game.Turn];
            turnLen = s.bot || s.connected ? cfg.turnSeconds : Mathf.Min(cfg.turnSeconds, 5f);
            deadline = Now + anim + turnLen;
            // Big tables move a bit quicker so the wait for your next turn stays short.
            float pace = Size > 4 ? 0.75f : 1f;
            if (s.bot) botActAt = Now + anim + UnityEngine.Random.Range(0.9f, 1.8f) * pace;
        }

        /// <summary>
        /// Rough time the screens need to animate these events, so bots don't act before the cards land
        /// (the same table as animMs in Server/src/match.js).
        /// </summary>
        public static float AnimSeconds(List<Ev> events, int players)
        {
            // dealing: one card each per round, a little faster per card at big tables
            float deal = Mathf.Min(1f, (2f + 0.24f * (players - 2)) / players);
            float ms = 0;
            foreach (var ev in events)
            {
                if (ev.k == "deal") ms += 160 * ev.n * deal;
                else if (ev.k == "draw" || ev.k == "unoPen") ms += 220 * Mathf.Max(1, ev.n);
                else if (ev.k == "play") ms += 450;
                else if (ev.k == "reshuffle") ms += 600;
                else if (ev.k == "launch") ms += 500 + 200 * ev.n;
                else if (ev.k == "roulette") ms += 400 + 200 * ev.n;
                else if (ev.k == "discardAll") ms += 250 * ev.n;
                else if (ev.k == "flip") ms += 1000;
                else if (ev.k == "swap" || ev.k == "rotate") ms += 900;
                else if (ev.k == "out") ms += 700;
                else if (ev.k == "dare" || ev.k == "darePending" || ev.k == "roulettePending") ms += 400;
            }
            return ms / 1000f;
        }

        // Bots occasionally react with an emote, like a person would.
        void BotReactions(List<Ev> events)
        {
            for (int b = 0; b < Size; b++)
            {
                if (!Seats[b].bot) continue;
                int e = -1;
                foreach (var ev in events)
                {
                    if (ev.k == "win") e = ev.p == b ? (UnityEngine.Random.value < 0.5f ? Emotes.Happy : Emotes.Cool) : Emotes.Sad;
                    else if (ev.k == "stack" && Game.Turn != b) e = Emotes.Laugh;
                    else if (ev.k == "draw" && ev.p == b && ev.n >= 4) e = UnityEngine.Random.value < 0.5f ? Emotes.Angry : Emotes.Wow;
                }
                // fewer reactions per bot at a big table, so it doesn't turn into a wall of emotes
                if (e >= 0 && UnityEngine.Random.value < 0.35f * Mathf.Min(1f, 3f / Size))
                    botEmotes.Add((Now + UnityEngine.Random.Range(0.7f, 1.3f), b, e));
            }
        }

        // ---------------------------------------------------------------- clock

        public void Tick()
        {
            for (int i = botEmotes.Count - 1; i >= 0; i--)
            {
                if (Now < botEmotes[i].at) continue;
                var (_, seat, emote) = botEmotes[i];
                botEmotes.RemoveAt(i);
                for (int j = 0; j < Size; j++) if (j != seat) Send(j, new ServerMsg { t = "emote", p = seat, e = emote });
            }
            if (!Started || Game.Over) return;
            int p = Game.Turn;
            if (Seats[p].bot)
            {
                if (botActAt > 0f && Now >= botActAt) BotAct();
                return;
            }
            if (Now >= deadline + 0.6f)
            {
                var s = Seats[p];
                s.timeouts++;
                if (s.timeouts >= cfg.maxTimeouts) Game.Forfeit(p, "idle");
                else Game.AutoMove(p);
                Flush();
            }
        }

        void BotAct()
        {
            botActAt = -1f;
            int p = Game.Turn;
            var err = UnoBot.Apply(Game, p, UnoBot.Choose(Game, p, Seats[p].level));
            if (err != null) Game.AutoMove(p);
            Flush();
        }
    }

    /// <summary>A move from a player's screen (Wire.* JSON). Unset fields keep these defaults.</summary>
    [Serializable]
    public sealed class ClientMsg
    {
        public string t;
        public int card = -1;
        public int color = -1;
        public bool uno;
        public int target = -1;
        public bool ok;
        public int e = -1;
    }
}
