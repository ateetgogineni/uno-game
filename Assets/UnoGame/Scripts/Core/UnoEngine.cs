using System.Collections.Generic;

namespace UnoGame.Core
{
    /// <summary>
    /// UNO rules engine for every game mode, a line-by-line port of Server/src/engine.js (the online
    /// authority). Used for offline games against a bot. Keep both in sync: Checks.ParityDump compares them.
    /// </summary>
    public sealed partial class UnoEngine
    {
        public const int MaxMoves = 1000;

        public readonly int N;
        public readonly string Mode;
        public readonly ModeRules Cfg;
        public readonly CardDef[] Deck;
        public readonly UnoRng Rng;
        public readonly bool Stacking;
        public List<int>[] Hands;
        public readonly int[] Skips;
        public readonly bool[] Out;   // knocked out (No Mercy 25 cards) or left a 3+ player game
        public List<int> DrawPile = new List<int>();   // top = last element
        public List<int> Discard = new List<int>();    // top = last element
        public int Side;            // UNO Flip: 0 light, 1 dark
        public int Color;           // 4 = anything goes (All Wild, or a wild flipped to the top)
        public int Dir = 1;
        public int Turn;
        public int Pending;
        public int LastDraw;
        public string PendingKind = "";   // "", "stack", "roulette", "dare"
        public int DareAmount;
        public int LauncherCharge;
        public int IdleTurns;
        public int Moves;
        public bool HasDrawn;
        public int DrawnCard = -1;
        public bool Over;
        public int Winner = -1;
        public int Points;

        readonly List<Ev> events = new List<Ev>();

        public UnoEngine(int players, int seed, bool stacking, string mode = "classic")
        {
            Mode = Decks.Normalize(mode);
            Cfg = Decks.Rules(Mode);
            Deck = Decks.For(Mode);
            N = players;
            Rng = new UnoRng(seed);
            Stacking = Cfg.Stack == "classic" ? stacking : Cfg.Stack != null;
            Hands = new List<int>[players];
            for (int i = 0; i < players; i++) Hands[i] = new List<int>();
            Skips = new int[players];
            Out = new bool[players];
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

        public Face FaceOf(int id) { var d = Deck[id]; return Side == 1 && d.HasB ? d.B : d.A; }
        public Face OtherFace(int id) { var d = Deck[id]; return Side == 1 ? d.A : d.B; }
        public int Top => Discard[Discard.Count - 1];
        /// <summary>Next player still in the game, in the direction of play.</summary>
        public int Next(int p)
        {
            int t = p;
            for (int i = 0; i < N; i++)
            {
                t = (t + Dir + N) % N;
                if (!Out[t]) return t;
            }
            return t;
        }

        public int ActiveCount()
        {
            int c = 0;
            for (int i = 0; i < N; i++) if (!Out[i]) c++;
            return c;
        }

        /// <summary>3+ players: Swap / Trade Hands, the No Mercy 7 and Targeted Draw let the player pick who.</summary>
        public bool NeedsTarget(Face f)
        {
            if (ActiveCount() <= 2) return false;
            return f.K == Kind.Swap || f.K == Kind.TargetDraw || (Cfg.SevenZero && f.K == Kind.Num && f.N == 7);
        }

        public bool ValidTarget(int p, int t) => t >= 0 && t < N && t != p && !Out[t];

        bool IsPlain(Face f) => f.K == Kind.Num || (Cfg.AllWild && f.K == Kind.Wild);

        static int Pop(List<int> list)
        {
            int v = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return v;
        }

        public void Start(int firstPlayer = -1)
        {
            var ids = new List<int>(Deck.Length);
            for (int i = 0; i < Deck.Length; i++) ids.Add(i);
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

            // The first discard is always a card without an effect: others go back into the pile.
            int first = Pop(DrawPile);
            while (!IsPlain(FaceOf(first)))
            {
                DrawPile.Insert(RandInt(DrawPile.Count), first);
                first = Pop(DrawPile);
            }
            Discard.Add(first);
            Color = FaceOf(first).C;
            var start = Ev.Of("start");
            start.card = first;
            start.color = Color;
            Emit(start);

            Turn = firstPlayer < 0 ? RandInt(N) : firstPlayer;
            Emit(Ev.Of("turn", Turn));
        }

        static bool SameSymbol(Face f, Face t)
        {
            if (f.K != t.K) return false;
            return (f.K == Kind.Num || f.K == Kind.Draw || f.K == Kind.WildDraw || f.K == Kind.Hit) ? f.N == t.N : true;
        }

        public bool IsPlayable(int p, int id)
        {
            var f = FaceOf(id);
            if (PendingKind == "stack")
            {
                if (!f.IsDrawCard) return false;
                return Cfg.Stack == "nomercy" ? f.N >= LastDraw : true;
            }
            if (PendingKind != "") return false;
            if (Cfg.AllWild) return true;
            if (f.IsWild)
            {
                if (Cfg.RestrictWildDraw && (f.K == Kind.WildDraw || f.K == Kind.WildDrawColor))
                {
                    // Official rule: only when you hold no other card of the current colour.
                    var hand = Hands[p];
                    for (int i = 0; i < hand.Count; i++)
                        if (hand[i] != id && FaceOf(hand[i]).C == Color) return false;
                }
                return true;
            }
            if (Color == Face.WildColor) return true;
            return f.C == Color || SameSymbol(f, FaceOf(Top));
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
                int id = DrawOne();
                if (id < 0) break;
                Hands[p].Add(id);
                got.Add(id);
            }
            return got;
        }

        int DrawOne()
        {
            if (DrawPile.Count == 0) Reshuffle();
            if (DrawPile.Count == 0) return -1;
            return Pop(DrawPile);
        }

        List<int> DrawUntil(int p, System.Func<int, bool> test)
        {
            var got = new List<int>();
            for (int guard = 0; guard < 60; guard++)
            {
                int id = DrawOne();
                if (id < 0) break;
                Hands[p].Add(id);
                got.Add(id);
                if (test(id)) break;
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

        List<int> HitLauncher(int p)
        {
            double chance = System.Math.Min(0.85, 0.22 + 0.16 * LauncherCharge);
            var got = new List<int>();
            if (Rng.Next() < chance)
            {
                int count = 1 + RandInt(4) + (RandInt(3) == 0 ? 2 : 0);
                LauncherCharge = 0;
                got = DrawCards(p, count);
            }
            else LauncherCharge++;
            var ev = Ev.Of("launch", p);
            ev.n = got.Count;
            ev.cards = got.ToArray();
            Emit(ev);
            return got;
        }

        void SwapHands(int a, int b)
        {
            var t = Hands[a];
            Hands[a] = Hands[b];
            Hands[b] = t;
            var ev = Ev.Of("swap", a);
            ev.n = b;
            Emit(ev);
        }

        /// <summary>No Mercy 0 with 3+ players: everyone passes their hand on in the direction of play.</summary>
        void RotateHands(int p)
        {
            var order = new List<int> { p };
            for (int t = Next(p); t != p && order.Count < N; t = Next(t)) order.Add(t);
            var moved = new List<int>[order.Count];
            for (int i = 0; i < order.Count; i++) moved[i] = Hands[order[i]];
            for (int i = 0; i < order.Count; i++) Hands[order[(i + 1) % order.Count]] = moved[i];
            var ev = Ev.Of("rotate", p);
            ev.dir = Dir;
            Emit(ev);
        }

        /// <summary>
        /// A player leaves a game of 3+: their cards go to the bottom of the draw pile and play goes on.
        /// Returns true when only one player is left (who then wins).
        /// </summary>
        bool Eliminate(int p, string reason)
        {
            Out[p] = true;
            Skips[p] = 0;
            var cards = Hands[p];
            Hands[p] = new List<int>();
            Shuffle(cards);
            DrawPile.InsertRange(0, cards);
            var ev = Ev.Of("out", p);
            ev.n = cards.Count;
            ev.reason = reason;
            Emit(ev);
            if (ActiveCount() > 1) return false;
            Finish(Next(p), reason);
            return true;
        }

        bool CheckMercy()
        {
            if (Cfg.Mercy <= 0 || Over) return false;
            for (int i = 0; i < N; i++)
            {
                if (Out[i] || Hands[i].Count < Cfg.Mercy) continue;
                var ev = Ev.Of("mercy", i);
                ev.n = Hands[i].Count;
                Emit(ev);
                if (ActiveCount() <= 2)
                {
                    Finish(Next(i), "mercy");
                    return true;
                }
                if (Eliminate(i, "mercy")) return true;
            }
            return false;
        }

        bool CheckStalemate(bool idle)
        {
            IdleTurns = idle ? IdleTurns + 1 : 0;
            if (IdleTurns < ActiveCount()) return false;
            FinishByCount("stalemate");
            return true;
        }

        void FinishByCount(string reason)
        {
            int best = Turn, bestCount = int.MaxValue, bestPts = int.MaxValue;
            for (int i = 0; i < N; i++)
            {
                int p = (Turn + i) % N;
                if (Out[p]) continue;
                int count = Hands[p].Count, pts = 0;
                foreach (int id in Hands[p]) pts += Decks.Points(Mode, FaceOf(id), Side);
                if (count < bestCount || (count == bestCount && pts < bestPts)) { best = p; bestCount = count; bestPts = pts; }
            }
            Finish(best, reason);
        }

        void EndTurn(int p)
        {
            if (++Moves >= MaxMoves) { FinishByCount("limit"); return; }
            int t = Next(p);
            for (int guard = 0; Skips[t] > 0 && guard < 50; guard++)
            {
                Skips[t]--;
                t = Next(t);
            }
            Turn = t;
            HasDrawn = false;
            DrawnCard = -1;
            Emit(Ev.Of("turn", Turn));
        }

        List<int> ForcedDraw(int t, int amount)
        {
            var got = DrawCards(t, amount);
            Emit(DrawEvent(t, got, true));
            return got;
        }

        static Ev DrawEvent(int p, List<int> got, bool forced)
        {
            var ev = Ev.Of("draw", p);
            ev.n = got.Count;
            ev.cards = got.ToArray();
            ev.forced = forced ? 1 : 0;
            return ev;
        }

        void FlipDeck()
        {
            Side ^= 1;
            Discard.Reverse();
            DrawPile.Reverse();
            Color = FaceOf(Top).C;
            var ev = Ev.Of("flip");
            ev.n = Side;
            ev.card = Top;
            ev.color = Color;
            Emit(ev);
        }

        /// <param name="forgive">The player forgot UNO but nobody caught it (bot opponents don't always notice).</param>
        /// <param name="target">Who a Swap / Trade Hands / No Mercy 7 / Targeted Draw goes to (3+ players; defaults to the next player).</param>
        public string Play(int p, int id, int chosen, bool uno, bool forgive = false, int target = -1)
        {
            if (Over) return "game over";
            if (p != Turn) return "not your turn";
            var hand = Hands[p];
            int idx = hand.IndexOf(id);
            if (idx < 0) return "card not in hand";
            if (HasDrawn && id != DrawnCard) return "only the drawn card can be played";
            if (!IsPlayable(p, id)) return "card cannot be played";
            var f = FaceOf(id);
            bool needsColor = f.IsWild && !Cfg.AllWild;
            if (needsColor && !(chosen >= 0 && chosen <= 3)) return "choose a colour";

            hand.RemoveAt(idx);
            Discard.Add(id);
            IdleTurns = 0;
            Color = f.IsWild ? (Cfg.AllWild ? Face.WildColor : chosen) : f.C;
            HasDrawn = false;
            DrawnCard = -1;
            var play = Ev.Of("play", p);
            play.card = id;
            play.color = Color;
            Emit(play);

            if (f.K == Kind.DiscardAll)
            {
                var extra = hand.FindAll(h => FaceOf(h).C == f.C);
                foreach (int h in extra) hand.Remove(h);
                Discard.InsertRange(Discard.Count - 1, extra);
                var ev = Ev.Of("discardAll", p);
                ev.n = extra.Count;
                ev.cards = extra.ToArray();
                Emit(ev);
            }

            bool swaps = f.K == Kind.Swap || (Cfg.SevenZero && f.K == Kind.Num && (f.N == 7 || f.N == 0));
            if (hand.Count == 1 && !swaps)
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

            int t = Next(p);
            int tgt = NeedsTarget(f) && ValidTarget(p, target) ? target : t;
            switch (f.K)
            {
                case Kind.Skip:
                    Emit(Ev.Of("skip", t));
                    Skips[t]++;
                    break;
                case Kind.SkipAll:
                    for (int i = t, guard = 0; i != p && guard < N; i = Next(i), guard++)
                    {
                        Emit(Ev.Of("skip", i));
                        Skips[i]++;
                    }
                    break;
                case Kind.SkipTwo:
                {
                    var ev = Ev.Of("skip", t);
                    ev.n = 2;
                    Emit(ev);
                    Skips[t] += 2;
                    break;
                }
                case Kind.Reverse:
                {
                    Dir = -Dir;
                    var ev = Ev.Of("reverse");
                    ev.dir = Dir;
                    Emit(ev);
                    if (N == 2) Skips[t]++;
                    break;
                }
                case Kind.Draw:
                case Kind.WildDraw:
                case Kind.WildRevDraw:
                    if (f.K == Kind.WildRevDraw)
                    {
                        Dir = -Dir;
                        var ev = Ev.Of("reverse");
                        ev.dir = Dir;
                        Emit(ev);
                    }
                    if (Stacking)
                    {
                        Pending += f.N;
                        LastDraw = f.N;
                        PendingKind = "stack";
                        var ev = Ev.Of("stack");
                        ev.n = Pending;
                        Emit(ev);
                    }
                    else
                    {
                        ForcedDraw(Next(p), f.N);
                        Skips[Next(p)]++;
                    }
                    break;
                case Kind.WildDrawColor:
                {
                    int want = Color;
                    var got = DrawUntil(t, h => FaceOf(h).C == want);
                    Emit(DrawEvent(t, got, true));
                    Skips[t]++;
                    break;
                }
                case Kind.Roulette:
                    PendingKind = "roulette";
                    Emit(Ev.Of("roulettePending", t));
                    break;
                case Kind.Flip:
                    FlipDeck();
                    break;
                case Kind.TargetDraw:
                    ForcedDraw(tgt, f.N);
                    break;
                case Kind.Swap:
                    SwapHands(p, tgt);
                    break;
                case Kind.Num:
                    if (swaps && f.N == 0 && ActiveCount() > 2) RotateHands(p);
                    else if (swaps) SwapHands(p, tgt);
                    break;
                case Kind.Hit:
                    for (int i = 0; i < f.N && !Over; i++) HitLauncher(t);
                    Skips[t]++;
                    break;
                case Kind.HitAll:
                    for (int i = 0; i < N; i++) if (i != p && !Out[i]) HitLauncher(i);
                    break;
                case Kind.HitFire:
                    for (int guard = 0; guard < 25; guard++)
                        if (HitLauncher(t).Count > 0 || DrawPile.Count + Discard.Count <= 1) break;
                    Skips[t]++;
                    break;
                case Kind.Dare:
                case Kind.WildDare:
                {
                    PendingKind = "dare";
                    DareAmount = f.N;
                    var ev = Ev.Of("darePending", t);
                    ev.n = f.N;
                    Emit(ev);
                    break;
                }
            }

            if (CheckMercy()) return null;

            // Someone with no cards left wins (a swap can hand the win to the other player).
            for (int i = 0; i < N; i++)
            {
                int w = (p + i) % N;
                if (Out[w] || Hands[w].Count != 0) continue;
                if (PendingKind == "stack" && Pending > 0)
                {
                    int victim = Next(w);
                    ForcedDraw(victim, Pending);
                    Pending = 0;
                    PendingKind = "";
                }
                PendingKind = "";
                Finish(w, "cards");
                return null;
            }

            EndTurn(p);
            return null;
        }

        public string Draw(int p)
        {
            if (Over) return "game over";
            if (p != Turn) return "not your turn";
            if (PendingKind == "stack")
            {
                int amount = Pending;
                Pending = 0;
                LastDraw = 0;
                PendingKind = "";
                ForcedDraw(p, amount);
                if (CheckMercy()) return null;
                EndTurn(p);
                return null;
            }
            if (PendingKind == "roulette") return "choose a colour for the roulette";
            if (PendingKind == "dare") return "do the dare or draw";
            if (HasDrawn) return "already drew this turn";

            bool deckEmpty = DrawPile.Count == 0 && Discard.Count <= 1;
            if (Cfg.Launcher)
            {
                var fired = HitLauncher(p);
                if (CheckStalemate(deckEmpty && fired.Count == 0)) return null;
                EndTurn(p);
                return null;
            }

            List<int> got;
            if (Cfg.DrawUntil) got = DrawUntil(p, h => IsPlayable(p, h));
            else got = DrawCards(p, 1);
            Emit(DrawEvent(p, got, false));
            if (CheckMercy()) return null;
            if (Out[p]) { EndTurn(p); return null; }
            if (CheckStalemate(deckEmpty && got.Count == 0)) return null;
            int last = got.Count > 0 ? got[got.Count - 1] : -1;
            if (last >= 0 && IsPlayable(p, last))
            {
                HasDrawn = true;
                DrawnCard = last;
                return null; // still this player's turn: play the drawn card or pass
            }
            EndTurn(p);
            return null;
        }

        public string Pass(int p)
        {
            if (Over) return "game over";
            if (p != Turn) return "not your turn";
            if (!HasDrawn) return "draw a card first";
            EndTurn(p);
            return null;
        }

        /// <summary>No Mercy Color Roulette: the victim names a colour and draws until it comes up.</summary>
        public string Roulette(int p, int color)
        {
            if (Over) return "game over";
            if (p != Turn || PendingKind != "roulette") return "no roulette to resolve";
            if (!(color >= 0 && color <= 3)) return "choose a colour";
            PendingKind = "";
            var got = DrawUntil(p, h => FaceOf(h).C == color);
            var ev = Ev.Of("roulette", p);
            ev.color = color;
            ev.n = got.Count;
            ev.cards = got.ToArray();
            Emit(ev);
            if (CheckMercy()) return null;
            EndTurn(p);
            return null;
        }

        /// <summary>UNO Dare: ok = the dare was done (keep your turn), otherwise draw the penalty and lose the turn.</summary>
        public string Dare(int p, bool ok)
        {
            if (Over) return "game over";
            if (p != Turn || PendingKind != "dare") return "no dare to resolve";
            int amount = DareAmount;
            PendingKind = "";
            DareAmount = 0;
            var ev = Ev.Of("dare", p);
            ev.n = amount;
            ev.forced = ok ? 0 : 1;
            Emit(ev);
            if (ok)
            {
                Emit(Ev.Of("turn", p));
                return null;
            }
            ForcedDraw(p, amount);
            EndTurn(p);
            return null;
        }

        /// <summary>What happens when a player's turn timer runs out.</summary>
        public void AutoMove(int p)
        {
            if (Over || p != Turn) return;
            if (PendingKind == "stack") { Draw(p); return; }
            if (PendingKind == "roulette") { Roulette(p, RandInt(4)); return; }
            if (PendingKind == "dare") { Dare(p, false); return; }
            if (!HasDrawn) Draw(p);
            if (!Over && Turn == p && HasDrawn) Pass(p);
        }

        void Finish(int winner, string reason)
        {
            Over = true;
            Winner = winner;
            int pts = 0;
            for (int i = 0; i < N; i++)
                if (i != winner) foreach (int id in Hands[i]) pts += Decks.Points(Mode, FaceOf(id), Side);
            Points = pts;
            HasDrawn = false;
            DrawnCard = -1;
            var ev = Ev.Of("win", winner);
            ev.pts = pts;
            ev.reason = reason;
            Emit(ev);
        }

        /// <summary>A player leaves or idles out. Two players left: the other one wins. More: the game goes on without them.</summary>
        public void Forfeit(int loser, string reason)
        {
            if (Over || Out[loser]) return;
            if (string.IsNullOrEmpty(reason)) reason = "forfeit";
            if (ActiveCount() <= 2)
            {
                int w = (loser + 1) % N;
                for (int g = 0; g < N && (w == loser || Out[w]); g++) w = (w + 1) % N;
                Finish(w, reason);
                return;
            }
            bool wasTurn = Turn == loser;
            if (Eliminate(loser, reason)) return;
            if (wasTurn)
            {
                Pending = 0;
                LastDraw = 0;
                PendingKind = "";
                DareAmount = 0;
                EndTurn(loser);
            }
        }

        /// <summary>UNO Flip: the other side of every opponent's cards, seat by seat from the one after you.</summary>
        List<int> OthersFaces(int seat)
        {
            var r = new List<int>();
            for (int k = 1; k < N; k++) foreach (int id in Hands[(seat + k) % N]) r.Add(OtherFace(id).Code);
            return r;
        }

        public St View(int seat)
        {
            bool mine = Turn == seat;
            var counts = new int[N];
            var outs = new int[N];
            for (int i = 0; i < N; i++) { counts[i] = Hands[i].Count; outs[i] = Out[i] ? 1 : 0; }
            var oppFaces = Cfg.Flip ? OthersFaces(seat) : new List<int>();
            return new St
            {
                you = seat,
                mode = Mode,
                side = Side,
                hand = Hands[seat].ToArray(),
                counts = counts,
                @out = outs,
                top = Top,
                color = Color,
                dir = Dir,
                turn = Turn,
                pending = Pending,
                lastDraw = LastDraw,
                pendingKind = PendingKind,
                dare = DareAmount,
                hasDrawn = mine && HasDrawn ? 1 : 0,
                drawn = mine ? DrawnCard : -1,
                legal = mine ? LegalMoves(seat).ToArray() : System.Array.Empty<int>(),
                pile = DrawPile.Count,
                oppFaces = oppFaces.ToArray(),
                pileFace = Cfg.Flip && DrawPile.Count > 0 ? OtherFace(DrawPile[DrawPile.Count - 1]).Code : -1,
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
                if (ev.cards != null && ev.cards.Length > 0 && ev.p != seat && ev.k != "discardAll")
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
