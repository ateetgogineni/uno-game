using System.Collections.Generic;

namespace UnoGame.Core
{
    /// <summary>Helpers over the state the server sends (which already lists the legal cards).</summary>
    public static class ViewRules
    {
        public static List<int> Legal(St st)
        {
            var res = new List<int>();
            if (st == null || st.over == 1 || st.turn != st.you || st.legal == null) return res;
            res.AddRange(st.legal);
            return res;
        }

        /// <summary>Simple strategy used by the autopilot (tests and video capture).</summary>
        public static int PickCard(St st, IList<int> hand, out int color)
        {
            var legal = Legal(st);
            color = -1;
            if (legal.Count == 0) return -1;
            int best = legal[0];
            int bestScore = int.MinValue;
            foreach (int id in legal)
            {
                var f = Decks.FaceOf(st.mode, st.side, id);
                int score = f.K == Kind.Num ? 10 + f.N : f.IsWild ? 2 : 30;
                if (f.K == Kind.Swap && hand.Count > 3) score = 0;
                if (st.pendingKind == "stack" && f.IsDrawCard) score += 100 - f.N;
                if (score > bestScore) { bestScore = score; best = id; }
            }
            var bf = Decks.FaceOf(st.mode, st.side, best);
            if (bf.IsWild && st.mode != "allwild")
            {
                var counts = new int[4];
                foreach (int id in hand)
                {
                    var f = Decks.FaceOf(st.mode, st.side, id);
                    if (id != best && !f.IsWild) counts[f.C]++;
                }
                color = 0;
                for (int c = 1; c < 4; c++) if (counts[c] > counts[color]) color = c;
            }
            return best;
        }
    }
}
