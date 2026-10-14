using System.Collections.Generic;

namespace UnoGame.Core
{
    /// <summary>Which cards the local player may play, worked out from the state the server sent.</summary>
    public static class ViewRules
    {
        public static bool IsPlayable(St st, IList<int> hand, int id)
        {
            int v = Cards.ValueOf(id), c = Cards.ColorOf(id);
            if (st.pending > 0) return v == Cards.Draw2 || v == Cards.Wild4;
            if (v == Cards.Wild) return true;
            if (v == Cards.Wild4)
            {
                for (int i = 0; i < hand.Count; i++)
                    if (hand[i] != id && Cards.ColorOf(hand[i]) == st.color) return false;
                return true;
            }
            return c == st.color || v == Cards.ValueOf(st.top);
        }

        public static List<int> Legal(St st, IList<int> hand)
        {
            var res = new List<int>();
            if (st == null || st.over == 1 || st.turn != st.you) return res;
            if (st.hasDrawn == 1)
            {
                if (st.drawn >= 0 && IsPlayable(st, hand, st.drawn)) res.Add(st.drawn);
                return res;
            }
            foreach (int id in hand) if (IsPlayable(st, hand, id)) res.Add(id);
            return res;
        }

        /// <summary>Simple strategy used by the autopilot (tests and video capture).</summary>
        public static int PickCard(St st, IList<int> hand, out int color)
        {
            var legal = Legal(st, hand);
            color = -1;
            if (legal.Count == 0) return -1;
            int best = legal[0];
            int bestScore = int.MinValue;
            foreach (int id in legal)
            {
                int v = Cards.ValueOf(id);
                int score = v == Cards.Wild4 ? 1 : v == Cards.Wild ? 2 : v >= 10 ? 30 : 10 + v;
                if (st.pending > 0 && v == Cards.Draw2) score += 100;
                if (score > bestScore) { bestScore = score; best = id; }
            }
            if (Cards.IsWild(best))
            {
                var counts = new int[4];
                foreach (int id in hand) if (id != best && !Cards.IsWild(id)) counts[Cards.ColorOf(id)]++;
                color = 0;
                for (int c = 1; c < 4; c++) if (counts[c] > counts[color]) color = c;
            }
            return best;
        }
    }
}
