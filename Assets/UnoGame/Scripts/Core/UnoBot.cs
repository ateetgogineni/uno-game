using System.Collections.Generic;

namespace UnoGame.Core
{
    /// <summary>Bot player for every mode, a port of Server/src/bot.js (same rng calls in the same order).</summary>
    public static class UnoBot
    {
        public enum MoveType { Play, Draw, Pass, Roulette, Dare }

        // Difficulty: new players get Easy, then it adapts to their recent results (Profile.BotLevel).
        public const int Easy = 0, Normal = 1, Hard = 2;

        static readonly double[] StackChance = { 0.35, 0.7, 1 };
        static readonly double[] UnoChance = { 0.6, 0.85, 0.92 };
        static readonly double[] DareSuccess = { 0.5, 0.65, 0.8 };
        public static readonly float[] CatchChance = { 0.25f, 0.7f, 1f };
        const double EasyDrawChance = 0.2, EasyPassChance = 0.4, NormalRandomChance = 0.25;

        public struct Move
        {
            public MoveType Type;
            public int Card;
            public int Color;
            public bool Uno;
            public bool Ok;
            public int Target;   // who a swap / targeted draw goes to (-1: the next player)
        }

        public static int ClampLevel(int level) => level < Easy || level > Hard ? Hard : level;

        /// <summary>Colour the bot holds most of (ignoring except); ties go to the lowest colour index.</summary>
        public static int BestColor(UnoEngine g, List<int> hand, int except)
        {
            var counts = new int[4];
            foreach (int id in hand)
            {
                if (id == except) continue;
                int c = g.FaceOf(id).C;
                if (c < 4) counts[c]++;
            }
            int best = -1, bestCount = 0;
            for (int c = 0; c < 4; c++)
                if (counts[c] > bestCount) { best = c; bestCount = counts[c]; }
            return best >= 0 ? best : g.RandInt(4);
        }

        static bool IsSwap(UnoEngine g, Face f) =>
            f.K == Kind.Swap || (g.Cfg.SevenZero && f.K == Kind.Num && (f.N == 7 || f.N == 0));

        /// <summary>
        /// The opponent closest to winning (fewest cards; ties go to whoever plays first from p).
        /// Used as the target of swaps and targeted draws; with two players it is simply the opponent.
        /// </summary>
        public static int FewestCards(UnoEngine g, int p)
        {
            int best = -1;
            for (int t = g.Next(p), guard = 0; t != p && guard < g.N; t = g.Next(t), guard++)
                if (best < 0 || g.Hands[t].Count < g.Hands[best].Count) best = t;
            return best;
        }

        /// <summary>The player who passes their hand to p when a No Mercy 0 rotates hands (the one before p).</summary>
        static int PreviousPlayer(UnoEngine g, int p)
        {
            int prev = p;
            for (int t = g.Next(p), guard = 0; t != p && guard < g.N; t = g.Next(t), guard++) prev = t;
            return prev;
        }

        static double ScoreCard(UnoEngine g, int p, int id, int oppCount)
        {
            var f = g.FaceOf(id);
            var hand = g.Hands[p];
            int sameColor = 0;
            foreach (int h in hand) if (h != id && g.FaceOf(h).C == f.C) sameColor++;
            if (IsSwap(g, f))
            {
                bool rotates = f.K == Kind.Num && f.N == 0 && g.ActiveCount() > 2;
                int from = rotates ? PreviousPlayer(g, p) : FewestCards(g, p);
                return hand.Count - 1 > g.Hands[from].Count ? 45 : -20;
            }
            if (g.Cfg.AllWild)
            {
                if (f.K == Kind.WildDraw) return oppCount <= 3 ? 40 : 16;
                if (f.K == Kind.TargetDraw) return 18;
                if (f.K == Kind.Wild) return 10;
                return 14;
            }
            switch (f.K)
            {
                case Kind.WildDraw: case Kind.WildRevDraw: case Kind.WildDrawColor: case Kind.Roulette:
                case Kind.HitFire: case Kind.HitAll: case Kind.WildDare:
                    return oppCount <= 3 ? 40 : 2;
                case Kind.Wild:
                    return oppCount <= 2 ? 18 : 4;
                case Kind.Draw: case Kind.Hit: case Kind.Dare:
                    return (oppCount <= 3 ? 34 : 14) + sameColor;
                case Kind.Skip: case Kind.Reverse: case Kind.SkipAll: case Kind.SkipTwo:
                    return (oppCount <= 2 ? 30 : 12) + sameColor;
                case Kind.DiscardAll:
                    return 10 + sameColor * 6;
                case Kind.Flip:
                    return 12 + sameColor;
                case Kind.TargetDraw:
                    return 20;
                default:
                    return 10 + f.N * 0.5 + sameColor * 1.5;
            }
        }

        static Move Simple(MoveType t) => new Move { Type = t, Card = -1, Color = -1, Target = -1 };

        public static Move Choose(UnoEngine g, int p, int level = Hard)
        {
            level = ClampLevel(level);
            if (g.PendingKind == "roulette") return new Move { Type = MoveType.Roulette, Card = -1, Color = g.RandInt(4), Target = -1 };
            if (g.PendingKind == "dare") return new Move { Type = MoveType.Dare, Card = -1, Color = -1, Ok = g.Rng.Next() < DareSuccess[level], Target = -1 };

            var hand = g.Hands[p];
            var legal = g.LegalMoves(p);
            if (legal.Count == 0) return Simple(g.HasDrawn ? MoveType.Pass : MoveType.Draw);

            if (level == Easy)
            {
                if (g.HasDrawn)
                {
                    if (g.Rng.Next() < EasyPassChance) return Simple(MoveType.Pass);
                }
                else if (g.PendingKind == "" && g.Rng.Next() < EasyDrawChance) return Simple(MoveType.Draw);
            }

            int pick = -1;
            if (g.PendingKind == "stack")
            {
                if (StackChance[level] < 1 && g.Rng.Next() >= StackChance[level]) return Simple(MoveType.Draw);
                foreach (int id in legal) if (pick < 0 || g.FaceOf(id).N < g.FaceOf(pick).N) pick = id;
            }
            else if (level == Easy)
            {
                var plain = legal.FindAll(id => g.FaceOf(id).K == Kind.Num);
                var pool = plain.Count > 0 && g.Rng.Next() < 0.75 ? plain : legal;
                pick = pool[g.RandInt(pool.Count)];
            }
            else if (level == Normal && g.Rng.Next() < NormalRandomChance)
            {
                pick = legal[g.RandInt(legal.Count)];
            }
            else
            {
                int oppCount = g.Hands[g.Next(p)].Count;
                double bestScore = -1e9;
                foreach (int id in legal)
                {
                    double s = ScoreCard(g, p, id, oppCount) + g.Rng.Next() * 3;
                    if (s > bestScore) { bestScore = s; pick = id; }
                }
            }

            var f = g.FaceOf(pick);
            int color = -1;
            if (f.IsWild && !g.Cfg.AllWild)
                color = level == Easy && g.Rng.Next() < 0.5 ? g.RandInt(4) : BestColor(g, hand, pick);
            bool uno = hand.Count == 2 && g.Rng.Next() < UnoChance[level];
            int target = g.NeedsTarget(f) ? FewestCards(g, p) : -1;
            return new Move { Type = MoveType.Play, Card = pick, Color = color, Uno = uno, Target = target };
        }

        public static string Apply(UnoEngine g, int p, Move m)
        {
            switch (m.Type)
            {
                case MoveType.Play: return g.Play(p, m.Card, m.Color, m.Uno, false, m.Target);
                case MoveType.Draw: return g.Draw(p);
                case MoveType.Roulette: return g.Roulette(p, m.Color);
                case MoveType.Dare: return g.Dare(p, m.Ok);
                default: return g.Pass(p);
            }
        }
    }
}
