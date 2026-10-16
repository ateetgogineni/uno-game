using System;

namespace UnoGame.Core
{
    // Wire format shared with the Node server (Server/src). Field names match the JSON keys exactly,
    // so JsonUtility can read every server message into ServerMsg.

    [Serializable]
    public class PlayerInfo
    {
        public string name;
        public int avatar;
    }

    /// <summary>One game event. Which fields are meaningful depends on k.</summary>
    [Serializable]
    public class Ev
    {
        public string k;      // deal, start, turn, play, uno, unoPen, reverse, skip, stack, draw, reshuffle, swap, rotate, out, win
        public int p;         // player seat
        public int n;         // number of cards / stack size
        public int card;      // card id
        public int color;     // colour after a play, 0..3
        public int[] cards;   // cards dealt/drawn (only filled in for the receiving player)
        public int dir;       // direction after a reverse
        public int pts;       // points for the winner
        public string reason; // win/out reason: cards, mercy, forfeit, left, idle
        public int forced;    // 1 when a draw was a penalty (stack, +2/+4)

        public static Ev Of(string k, int p = 0) => new Ev { k = k, p = p, cards = Array.Empty<int>() };

        public Ev Clone()
        {
            var e = (Ev)MemberwiseClone();
            e.cards = cards == null ? Array.Empty<int>() : (int[])cards.Clone();
            return e;
        }
    }

    /// <summary>What one player can see of the game.</summary>
    [Serializable]
    public class St
    {
        public int you;
        public string mode;
        public int side;        // UNO Flip: 0 light, 1 dark
        public int[] hand;
        public int[] counts;
        public int[] @out;      // 1 for players who are out of the game (No Mercy knockout, left a 3+ player game)
        public int top;
        public int color;       // 4 = anything goes
        public int dir;
        public int turn;
        public int pending;
        public int lastDraw;
        public string pendingKind;  // "", "stack", "roulette", "dare"
        public int dare;
        public int hasDrawn;
        public int drawn;
        public int[] legal;     // cards you may play right now (server-authoritative)
        public int pile;
        public int[] oppFaces;  // UNO Flip: other side of every opponent's cards, seat by seat from the one after you
        public int pileFace;    // UNO Flip: face code of the other side of the draw pile's top card
        public int over;
        public int winner;
        public int pts;
        public int ms;        // time left in the current turn
        public int turnMs;    // full turn length
    }

    [Serializable]
    public class ServerMsg
    {
        public string t;      // welcome, queued, matched, up, sync, emote, err, pong
        public int v;
        public int online;
        public int resumed;
        public string match;
        public string mode;
        public int seat;
        public int size;      // queued: table size being filled
        public PlayerInfo[] players;
        public Ev[] ev;
        public St st;
        public int p;
        public int e;
        public string msg;

        [NonSerialized] public float receivedAt;
    }

    public static class Emotes
    {
        public const int Happy = 0, Laugh = 1, Cool = 2, Wow = 3, Angry = 4, Sad = 5;
        public const int Count = 6;
    }
}
