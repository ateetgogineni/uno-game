using System.Collections.Generic;
using UnoGame.Core;
using UnityEngine;

namespace UnoGame.Net
{
    /// <summary>
    /// Offline match against 1 to 9 bots, used when the server is unreachable. Runs the C# port of the
    /// server engine and produces exactly the messages the server would send (see Server/src/match.js).
    /// </summary>
    public sealed class LocalSession : GameSession
    {
        const int MeSeat = 0;

        readonly GameConfig cfg;
        UnoEngine game;
        bool started;
        float deadline;        // unscaled time when the current turn runs out
        float botActAt = -1;
        float turnLen;
        int timeouts;
        readonly int botLevel;
        readonly List<(float at, int seat, int emote)> botEmotes = new List<(float, int, int)>();

        /// <param name="players">You first, then the bots.</param>
        public LocalSession(GameConfig cfg, PlayerInfo[] players, int level, string mode)
        {
            this.cfg = cfg;
            Mode = Decks.Normalize(mode);
            botLevel = UnoBot.ClampLevel(level);
            MatchId = "local";
            Seat = MeSeat;
            Players = players;
        }

        bool IsBot(int seat) => seat != MeSeat;

        int RandomBot()
        {
            var live = new List<int>();
            for (int i = 0; i < Size; i++) if (IsBot(i) && (game == null || !game.Out[i])) live.Add(i);
            return live.Count > 0 ? live[Random.Range(0, live.Count)] : -1;
        }

        public override bool IsOnline => false;

        public override void Ready()
        {
            if (started) return;
            started = true;
            game = new UnoEngine(Size, Random.Range(1, int.MaxValue), cfg.stacking, Mode);
            game.Start();
            Flush();
        }

        float Now => Time.unscaledTime;

        static float AnimSeconds(List<Ev> events)
        {
            float ms = 0;
            foreach (var ev in events)
            {
                if (ev.k == "deal") ms += 160 * ev.n;
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

        void Flush()
        {
            var events = game.TakeEvents();
            float anim = AnimSeconds(events);
            botActAt = -1;
            if (!game.Over)
            {
                turnLen = cfg.turnSeconds;
                deadline = Now + anim + turnLen;
                // Big tables move a bit quicker so the wait for your next turn stays short.
                float pace = Size > 4 ? 0.75f : 1f;
                if (IsBot(game.Turn)) botActAt = Now + anim + Random.Range(0.9f, 1.8f) * pace;
            }
            var st = game.View(MeSeat);
            st.ms = game.Over ? 0 : Mathf.RoundToInt((deadline - Now) * 1000);
            st.turnMs = Mathf.RoundToInt(turnLen * 1000);
            Deliver(new ServerMsg { t = "up", ev = UnoEngine.EventsFor(MeSeat, events).ToArray(), st = st });
            BotReactions(events);
        }

        // Bots occasionally react with an emote, like a person would.
        void BotReactions(List<Ev> events)
        {
            for (int b = 0; b < Size; b++)
            {
                if (!IsBot(b)) continue;
                int e = -1;
                foreach (var ev in events)
                {
                    if (ev.k == "win") e = ev.p == b ? (Random.value < 0.5f ? Emotes.Happy : Emotes.Cool) : Emotes.Sad;
                    else if (ev.k == "stack" && game.Turn != b) e = Emotes.Laugh;
                    else if (ev.k == "draw" && ev.p == b && ev.n >= 4) e = Random.value < 0.5f ? Emotes.Angry : Emotes.Wow;
                }
                // fewer reactions per bot at a big table, so it doesn't turn into a wall of emotes
                if (e >= 0 && Random.value < 0.35f * Mathf.Min(1f, 3f / Size)) botEmotes.Add((Now + Random.Range(0.7f, 1.3f), b, e));
            }
        }

        public override void Tick()
        {
            for (int i = botEmotes.Count - 1; i >= 0; i--)
            {
                if (Now < botEmotes[i].at) continue;
                Deliver(new ServerMsg { t = "emote", p = botEmotes[i].seat, e = botEmotes[i].emote });
                botEmotes.RemoveAt(i);
            }
            if (!started || game.Over) return;
            if (IsBot(game.Turn))
            {
                if (botActAt > 0 && Now >= botActAt) BotAct();
                return;
            }
            if (Now >= deadline + 0.6f)
            {
                timeouts++;
                if (timeouts >= cfg.maxTimeouts) game.Forfeit(MeSeat, "idle");
                else game.AutoMove(MeSeat);
                Flush();
            }
        }

        void BotAct()
        {
            botActAt = -1;
            int p = game.Turn;
            var err = UnoBot.Apply(game, p, UnoBot.Choose(game, p, botLevel));
            if (err != null) game.AutoMove(p);
            Flush();
        }

        void Act(string err)
        {
            if (err != null)
            {
                Deliver(new ServerMsg { t = "err", msg = err });
                SendSync();
                return;
            }
            timeouts = 0;
            Flush();
        }

        public override void Play(int card, int color, bool uno, int target = -1)
        {
            if (!started || game.Over) return;
            // Forgot UNO? Each bot only notices some of the time (official rule: no catch, no penalty).
            bool forgive = !uno;
            for (int i = 0; i < Size && forgive; i++)
                if (IsBot(i) && !game.Out[i] && Random.value < UnoBot.CatchChance[botLevel]) forgive = false;
            Act(game.Play(MeSeat, card, color, uno, forgive, target));
        }

        public override void Draw()
        {
            if (!started || game.Over) return;
            Act(game.Draw(MeSeat));
        }

        public override void Pass()
        {
            if (!started || game.Over) return;
            Act(game.Pass(MeSeat));
        }

        public override void Roulette(int color)
        {
            if (!started || game.Over) return;
            Act(game.Roulette(MeSeat, color));
        }

        public override void Dare(bool ok)
        {
            if (!started || game.Over) return;
            Act(game.Dare(MeSeat, ok));
        }

        public override void Emote(int e)
        {
            // A bot sometimes answers.
            int b = RandomBot();
            if (b >= 0 && Random.value < 0.4f)
            {
                int[] replies = { Emotes.Happy, Emotes.Laugh, Emotes.Cool, Emotes.Wow };
                botEmotes.Add((Now + Random.Range(1.0f, 1.8f), b, replies[Random.Range(0, replies.Length)]));
            }
        }

        public override void Leave()
        {
            if (started && !game.Over)
            {
                game.Forfeit(MeSeat, "left");
                Flush();
            }
            Finished = true;
        }

        public override void RequestSync() => SendSync();

        void SendSync()
        {
            if (!started) return;
            var st = game.View(MeSeat);
            st.ms = Mathf.RoundToInt(Mathf.Max(0, deadline - Now) * 1000);
            st.turnMs = Mathf.RoundToInt(turnLen * 1000);
            Deliver(new ServerMsg { t = "sync", match = MatchId, seat = MeSeat, players = Players, st = st });
        }
    }
}
