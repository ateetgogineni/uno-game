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
        public string k;      // deal, start, turn, play, uno, unoPen, reverse, skip, stack, draw, reshuffle, win
        public int p;         // player seat
        public int n;         // number of cards / stack size
        public int card;      // card id
        public int color;     // colour after a play, 0..3
        public int[] cards;   // cards dealt/drawn (only filled in for the receiving player)
        public int dir;       // direction after a reverse
        public int pts;       // points for the winner
        public string reason; // win reason: cards, forfeit, left, idle
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
        public int[] hand;
        public int[] counts;
        public int top;
        public int color;
        public int dir;
        public int turn;
        public int pending;
        public int hasDrawn;
        public int drawn;
        public int pile;
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
        public int seat;
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
