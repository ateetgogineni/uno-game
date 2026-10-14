using System.Collections.Generic;

namespace UnoGame.Core
{
    /// <summary>Bot player, a port of Server/src/bot.js (same rng calls in the same order).</summary>
    public static class UnoBot
    {
        public enum MoveType { Play, Draw, Pass }

        public struct Move
        {
            public MoveType Type;
            public int Card;
            public int Color;
            public bool Uno;
        }

        /// <summary>Colour the bot holds most of (ignoring except); ties go to the lowest colour index.</summary>
        public static int BestColor(UnoEngine g, List<int> hand, int except)
        {
            var counts = new int[4];
            foreach (int id in hand)
            {
                if (id == except) continue;
                int c = Cards.ColorOf(id);
                if (c < 4) counts[c]++;
            }
            int best = -1, bestCount = 0;
            for (int c = 0; c < 4; c++)
                if (counts[c] > bestCount) { best = c; bestCount = counts[c]; }
            return best >= 0 ? best : g.RandInt(4);
        }

        static double ScoreCard(UnoEngine g, int p, int id, int oppCount)
        {
            int v = Cards.ValueOf(id), c = Cards.ColorOf(id);
            int sameColor = 0;
            foreach (int h in g.Hands[p]) if (h != id && Cards.ColorOf(h) == c) sameColor++;
            if (v == Cards.Wild4) return oppCount <= 3 ? 40 : 2;
            if (v == Cards.Wild) return oppCount <= 2 ? 18 : 4;
            if (v == Cards.Draw2) return (oppCount <= 3 ? 34 : 14) + sameColor;
            if (v == Cards.Skip || v == Cards.Reverse) return (oppCount <= 2 ? 30 : 12) + sameColor;
            return 10 + v * 0.5 + sameColor * 1.5;
        }

        public static Move Choose(UnoEngine g, int p)
        {
            var hand = g.Hands[p];
            var legal = g.LegalMoves(p);
            if (legal.Count == 0) return new Move { Type = g.HasDrawn ? MoveType.Pass : MoveType.Draw, Card = -1, Color = -1 };

            int pick = -1;
            if (g.Pending > 0)
            {
                foreach (int id in legal) if (Cards.ValueOf(id) == Cards.Draw2) { pick = id; break; }
                if (pick < 0) pick = legal[0];
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

            int color = Cards.IsWild(pick) ? BestColor(g, hand, pick) : -1;
            bool uno = hand.Count == 2 && g.Rng.Next() < 0.92;
            return new Move { Type = MoveType.Play, Card = pick, Color = color, Uno = uno };
        }

        public static string Apply(UnoEngine g, int p, Move m)
        {
            switch (m.Type)
            {
                case MoveType.Play: return g.Play(p, m.Card, m.Color, m.Uno);
                case MoveType.Draw: return g.Draw(p);
                default: return g.Pass(p);
            }
        }
    }
}
