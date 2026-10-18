using System;
using System.Collections.Generic;

namespace UnoGame.Core
{
    /// <summary>
    /// Everything needed to carry on a game exactly where it was (including the shuffle's random state).
    /// The Photon host shares it after every move so another phone can take over if the host leaves.
    /// </summary>
    [Serializable]
    public sealed class EngineSnapshot
    {
        public int n;
        public string mode;
        public bool stacking;
        public long rng;
        public int[] handCounts;
        public int[] hands;          // every hand, one after another (sizes in handCounts)
        public int[] skips;
        public int[] outs;
        public int[] drawPile;
        public int[] discard;
        public int side, color, dir, turn, pending, lastDraw;
        public string pendingKind;
        public int dareAmount, launcherCharge, idleTurns, moves;
        public bool hasDrawn;
        public int drawnCard;
        public bool over;
        public int winner, points;
    }

    public sealed partial class UnoEngine
    {
        public EngineSnapshot Snapshot()
        {
            var counts = new int[N];
            var all = new List<int>();
            for (int i = 0; i < N; i++) { counts[i] = Hands[i].Count; all.AddRange(Hands[i]); }
            var outs = new int[N];
            for (int i = 0; i < N; i++) outs[i] = Out[i] ? 1 : 0;
            return new EngineSnapshot
            {
                n = N, mode = Mode, stacking = Stacking, rng = Rng.State,
                handCounts = counts, hands = all.ToArray(), skips = (int[])Skips.Clone(), outs = outs,
                drawPile = DrawPile.ToArray(), discard = Discard.ToArray(),
                side = Side, color = Color, dir = Dir, turn = Turn, pending = Pending, lastDraw = LastDraw,
                pendingKind = PendingKind, dareAmount = DareAmount, launcherCharge = LauncherCharge,
                idleTurns = IdleTurns, moves = Moves, hasDrawn = HasDrawn, drawnCard = DrawnCard,
                over = Over, winner = Winner, points = Points,
            };
        }

        public static UnoEngine Restore(EngineSnapshot s)
        {
            var g = new UnoEngine(s.n, 1, s.stacking, s.mode);
            g.Rng.State = unchecked((uint)s.rng);
            int k = 0;
            for (int i = 0; i < s.n; i++)
            {
                g.Hands[i] = new List<int>();
                for (int c = 0; c < s.handCounts[i]; c++) g.Hands[i].Add(s.hands[k++]);
                g.Skips[i] = s.skips[i];
                g.Out[i] = s.outs[i] == 1;
            }
            g.DrawPile = new List<int>(s.drawPile);
            g.Discard = new List<int>(s.discard);
            g.Side = s.side;
            g.Color = s.color;
            g.Dir = s.dir;
            g.Turn = s.turn;
            g.Pending = s.pending;
            g.LastDraw = s.lastDraw;
            g.PendingKind = s.pendingKind ?? "";
            g.DareAmount = s.dareAmount;
            g.LauncherCharge = s.launcherCharge;
            g.IdleTurns = s.idleTurns;
            g.Moves = s.moves;
            g.HasDrawn = s.hasDrawn;
            g.DrawnCard = s.drawnCard;
            g.Over = s.over;
            g.Winner = s.winner;
            g.Points = s.points;
            return g;
        }
    }
}
