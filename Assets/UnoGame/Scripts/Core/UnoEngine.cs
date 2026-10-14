using System.Collections.Generic;

namespace UnoGame.Core
{
    /// <summary>
    /// UNO rules engine, a line-by-line port of Server/src/engine.js (the online authority).
    /// Used for offline games against a bot. Keep both in sync: Checks.ParityDump compares them.
    /// Rules: official 108-card deck; Wild Draw Four only without a card of the current colour;
    /// two players: Skip and Reverse give you another turn; forgetting UNO costs 2 cards;
    /// optional +2/+4 stacking (on by default, as in the reference game).
    /// </summary>
    public sealed class UnoEngine
    {
        public readonly int N;
        public readonly UnoRng Rng;
        public readonly bool Stacking;
        public readonly List<int>[] Hands;
        public List<int> DrawPile = new List<int>();   // top = last element
        public List<int> Discard = new List<int>();    // top = last element
        public int Color;
        public int Dir = 1;
        public int Turn;
        public int Pending;
        public bool HasDrawn;
        public int DrawnCard = -1;
        public bool Over;
        public int Winner = -1;
        public int Points;

        readonly List<Ev> events = new List<Ev>();

        public UnoEngine(int players, int seed, bool stacking)
        {
            N = players;
            Rng = new UnoRng(seed);
            Stacking = stacking;
            Hands = new List<int>[players];
            for (int i = 0; i < players; i++) Hands[i] = new List<int>();
        }

        public int RandInt(int n) => (int)System.Math.Floor(Rng.Next() * n);

        void Shuffle(List<int> arr)
        {
            for (int i = arr.Count - 1; i > 0; i--)
            {
                int j = RandInt(i + 1);
                int t = arr[i]; arr[i] = arr[j]; arr[j] = t;
            }
        }

        void Emit(Ev ev) => events.Add(ev);

        public List<Ev> TakeEvents()
        {
            var e = new List<Ev>(events);
            events.Clear();
            return e;
        }

        public int Top => Discard[Discard.Count - 1];
        public int Next(int p) => (p + Dir + N) % N;

        public void Start(int firstPlayer = -1)
        {
            var ids = new List<int>(Cards.DeckSize);
            for (int i = 0; i < Cards.DeckSize; i++) ids.Add(i);
            Shuffle(ids);
            DrawPile = ids;
            for (int r = 0; r < Cards.HandSize; r++)
                for (int p = 0; p < N; p++) Hands[p].Add(Pop(DrawPile));
            for (int p = 0; p < N; p++)
            {
                var ev = Ev.Of("deal", p);
                ev.n = Cards.HandSize;
                ev.cards = Hands[p].ToArray();
                Emit(ev);
            }

            // The first discard is always a number card: action and wild cards go back into the pile.
            int first = Pop(DrawPile);
            while (Cards.ValueOf(first) > 9)
            {
                DrawPile.Insert(RandInt(DrawPile.Count), first);
                first = Pop(DrawPile);
            }
            Discard.Add(first);
            Color = Cards.ColorOf(first);
            var start = Ev.Of("start");
            start.card = first;
            start.color = Color;
            Emit(start);

            Turn = firstPlayer < 0 ? RandInt(N) : firstPlayer;
            Emit(Ev.Of("turn", Turn));
        }

        static int Pop(List<int> list)
        {
            int v = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return v;
        }

        public bool IsPlayable(int p, int id)
        {
            int v = Cards.ValueOf(id), c = Cards.ColorOf(id);
            if (Pending > 0) return v == Cards.Draw2 || v == Cards.Wild4;
            if (v == Cards.Wild) return true;
            if (v == Cards.Wild4)
            {
                var hand = Hands[p];
                for (int i = 0; i < hand.Count; i++)
                    if (hand[i] != id && Cards.ColorOf(hand[i]) == Color) return false;
                return true;
            }
            return c == Color || v == Cards.ValueOf(Top);
        }

        public List<int> LegalMoves(int p)
        {
            var res = new List<int>();
            if (Over || p != Turn) return res;
            if (HasDrawn)
            {
                if (DrawnCard >= 0 && IsPlayable(p, DrawnCard)) res.Add(DrawnCard);
                return res;
            }
            foreach (int id in Hands[p]) if (IsPlayable(p, id)) res.Add(id);
            return res;
        }

        List<int> DrawCards(int p, int k)
        {
            var got = new List<int>();
            for (int i = 0; i < k; i++)
            {
                if (DrawPile.Count == 0) Reshuffle();
                if (DrawPile.Count == 0) break;
                int id = Pop(DrawPile);
                Hands[p].Add(id);
                got.Add(id);
            }
            return got;
        }

        void Reshuffle()
        {
            if (Discard.Count <= 1) return;
            int top = Pop(Discard);
            var rest = Discard;
            Discard = new List<int> { top };
            Shuffle(rest);
            DrawPile = rest;
            var ev = Ev.Of("reshuffle");
            ev.n = rest.Count;
            Emit(ev);
        }

        Ev DrawEvent(int p, List<int> got, bool forced)
        {
            var ev = Ev.Of("draw", p);
            ev.n = got.Count;
            ev.cards = got.ToArray();
            ev.forced = forced ? 1 : 0;
            return ev;
        }

        /// <param name="forgive">The player forgot UNO but nobody caught it (bot opponents don't always notice).</param>
        public string Play(int p, int id, int chosen, bool uno, bool forgive = false)
        {
            if (Over) return "game over";
            if (p != Turn) return "not your turn";
            var hand = Hands[p];
            int idx = hand.IndexOf(id);
            if (idx < 0) return "card not in hand";
            if (HasDrawn && id != DrawnCard) return "only the drawn card can be played";
            if (!IsPlayable(p, id)) return "card cannot be played";
            int v = Cards.ValueOf(id), c = Cards.ColorOf(id);
            if (c == Cards.WildColor && !(chosen >= 0 && chosen <= 3)) return "choose a colour";

            hand.RemoveAt(idx);
            Discard.Add(id);
            Color = c == Cards.WildColor ? chosen : c;
            HasDrawn = false;
            DrawnCard = -1;
            var play = Ev.Of("play", p);
            play.card = id;
            play.color = Color;
            Emit(play);

            if (hand.Count == 1)
            {
                if (uno) Emit(Ev.Of("uno", p));
                else if (!forgive)
                {
                    var got = DrawCards(p, 2);
                    var pen = Ev.Of("unoPen", p);
                    pen.n = got.Count;
                    pen.cards = got.ToArray();
                    Emit(pen);
                }
            }

            bool again = false; // 2-player Skip/Reverse: the same player goes again
            if (v == Cards.Reverse)
            {
                Dir = -Dir;
                var ev = Ev.Of("reverse");
                ev.dir = Dir;
                Emit(ev);
                if (N == 2) again = true;
            }
            else if (v == Cards.Skip)
            {
                Emit(Ev.Of("skip", Next(p)));
                again = true;
            }
            else if (v == Cards.Draw2 || v == Cards.Wild4)
            {
                int amount = v == Cards.Draw2 ? 2 : 4;
                if (Stacking)
                {
                    Pending += amount;
                    var ev = Ev.Of("stack");
                    ev.n = Pending;
                    Emit(ev);
                }
                else
                {
                    int t = Next(p);
                    Emit(DrawEvent(t, DrawCards(t, amount), true));
                    again = true;
                }
            }

            if (hand.Count == 0)
            {
                if (Pending > 0)
                {
                    int t = Next(p);
                    var got = DrawCards(t, Pending);
                    Pending = 0;
                    Emit(DrawEvent(t, got, true));
                }
                Finish(p, "cards");
                return null;
            }

            Turn = again ? (N == 2 ? p : Next(Next(p))) : Next(p);
            Emit(Ev.Of("turn", Turn));
            return null;
        }

        public string Draw(int p)
        {
            if (Over) return "game over";
            if (p != Turn) return "not your turn";
            if (Pending > 0)
            {
                int amount = Pending;
                Pending = 0;
                Emit(DrawEvent(p, DrawCards(p, amount), true));
                Turn = Next(p);
                Emit(Ev.Of("turn", Turn));
                return null;
            }
            if (HasDrawn) return "already drew this turn";
            var got = DrawCards(p, 1);
            Emit(DrawEvent(p, got, false));
            if (got.Count == 1 && IsPlayable(p, got[0]))
            {
                HasDrawn = true;
                DrawnCard = got[0];
                return null; // still this player's turn: play the drawn card or pass
            }
            Turn = Next(p);
            Emit(Ev.Of("turn", Turn));
            return null;
        }

        public string Pass(int p)
        {
            if (Over) return "game over";
            if (p != Turn) return "not your turn";
            if (!HasDrawn) return "draw a card first";
            HasDrawn = false;
            DrawnCard = -1;
            Turn = Next(p);
            Emit(Ev.Of("turn", Turn));
            return null;
        }

        /// <summary>What happens when a player's turn timer runs out.</summary>
        public void AutoMove(int p)
        {
            if (Over || p != Turn) return;
            if (Pending > 0) { Draw(p); return; }
            if (!HasDrawn) Draw(p);
            if (!Over && Turn == p && HasDrawn) Pass(p);
        }

        void Finish(int winner, string reason)
        {
            Over = true;
            Winner = winner;
            int pts = 0;
            for (int i = 0; i < N; i++)
                if (i != winner) foreach (int id in Hands[i]) pts += Cards.Points(id);
            Points = pts;
            HasDrawn = false;
            DrawnCard = -1;
            var ev = Ev.Of("win", winner);
            ev.pts = pts;
            ev.reason = reason;
            Emit(ev);
        }

        public void Forfeit(int loser, string reason)
        {
            if (Over) return;
            Finish((loser + 1) % N, string.IsNullOrEmpty(reason) ? "forfeit" : reason);
        }

        public St View(int seat)
        {
            bool mine = Turn == seat;
            var counts = new int[N];
            for (int i = 0; i < N; i++) counts[i] = Hands[i].Count;
            return new St
            {
                you = seat,
                hand = Hands[seat].ToArray(),
                counts = counts,
                top = Top,
                color = Color,
                dir = Dir,
                turn = Turn,
                pending = Pending,
                hasDrawn = mine && HasDrawn ? 1 : 0,
                drawn = mine ? DrawnCard : -1,
                pile = DrawPile.Count,
                over = Over ? 1 : 0,
                winner = Winner,
                pts = Points,
            };
        }

        /// <summary>Events as one seat sees them: other players' drawn cards are hidden.</summary>
        public static List<Ev> EventsFor(int seat, List<Ev> evs)
        {
            var res = new List<Ev>(evs.Count);
            foreach (var ev in evs)
            {
                if (ev.cards != null && ev.cards.Length > 0 && ev.p != seat)
                {
                    var copy = ev.Clone();
                    copy.cards = System.Array.Empty<int>();
                    res.Add(copy);
                }
                else res.Add(ev);
            }
            return res;
        }
    }
}
