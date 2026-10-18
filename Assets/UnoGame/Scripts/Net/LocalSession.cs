using UnoGame.Core;
using UnityEngine;

namespace UnoGame.Net
{
    /// <summary>
    /// Offline match against 1 to 9 bots, used when no online play is available. The table runs on this
    /// phone (TableHost, the same code a Photon host uses) and sends exactly the messages a server would.
    /// </summary>
    public sealed class LocalSession : GameSession
    {
        const int MeSeat = 0;
        readonly TableHost host;

        /// <param name="players">You first, then the bots.</param>
        public LocalSession(GameConfig cfg, PlayerInfo[] players, int level, string mode)
        {
            Mode = Decks.Normalize(mode);
            MatchId = "local";
            Seat = MeSeat;
            Players = players;
            var seats = new TableHost.Seat[players.Length];
            for (int i = 0; i < seats.Length; i++)
                seats[i] = new TableHost.Seat { info = players[i], bot = i != MeSeat, level = UnoBot.ClampLevel(level) };
            host = new TableHost(cfg, seats, Mode, MatchId, (seat, m) => Deliver(m));
        }

        public override bool IsOnline => false;

        bool Playing => host.Started && !host.Game.Over;

        public override void Ready()
        {
            if (!host.Started) host.Start(Random.Range(1, int.MaxValue));
        }

        public override void Tick() => host.Tick();

        public override void Play(int card, int color, bool uno, int target = -1)
        {
            if (Playing) host.Play(MeSeat, card, color, uno, target);
        }

        public override void Draw()
        {
            if (Playing) host.Act(MeSeat, () => host.Game.Draw(MeSeat));
        }

        public override void Pass()
        {
            if (Playing) host.Act(MeSeat, () => host.Game.Pass(MeSeat));
        }

        public override void Roulette(int color)
        {
            if (Playing) host.Act(MeSeat, () => host.Game.Roulette(MeSeat, color));
        }

        public override void Dare(bool ok)
        {
            if (Playing) host.Act(MeSeat, () => host.Game.Dare(MeSeat, ok));
        }

        public override void Emote(int e) => host.Emote(MeSeat, e);

        public override void Leave()
        {
            if (Playing) host.Leave(MeSeat, "left");
            Finished = true;
        }

        public override void RequestSync() => host.SendSync(MeSeat);
    }
}
