using System.Collections.Generic;

namespace UnoGame.Core
{
    /// <summary>Card kinds (same numbers as Server/src/cards.js).</summary>
    public static class Kind
    {
        public const int Num = 0, Skip = 1, Reverse = 2, Draw = 3, Wild = 4, WildDraw = 5, Flip = 6, SkipAll = 7,
            WildDrawColor = 8, DiscardAll = 9, WildRevDraw = 10, Roulette = 11, SkipTwo = 12, TargetDraw = 13,
            Swap = 14, Hit = 15, HitAll = 16, HitFire = 17, Dare = 18, WildDare = 19;
    }

    /// <summary>One side of a card: colour (0-3, 4 = wild), kind, number or card count.</summary>
    public readonly struct Face
    {
        public const int WildColor = 4;
        public readonly int C, K, N;

        public Face(int c, int k, int n = 0) { C = c; K = k; N = n; }

        public bool IsWild => C == WildColor;
        public bool IsDrawCard => K == Kind.Draw || K == Kind.WildDraw || K == Kind.WildRevDraw;
        public int Code => C * 1024 + K * 32 + N;
        public static Face FromCode(int code) => new Face(code / 1024, (code / 32) % 32, code % 32);
    }

    public sealed class CardDef
    {
        public Face A;          // light / only side
        public Face B;          // UNO Flip dark side
        public bool HasB;
    }

    /// <summary>Rules for one game mode (see Server/src/cards.js MODES).</summary>
    public sealed class ModeRules
    {
        public string Id;
        public string Stack;           // "classic", "nomercy" or null
        public bool RestrictWildDraw;
        public bool Flip;
        public bool DrawUntil;
        public int Mercy;
        public bool SevenZero;
        public bool AllWild;
        public bool Launcher;
        public bool Dares;
    }

    /// <summary>Decks for every mode, built in exactly the same order as the server.</summary>
    public static class Decks
    {
        public static readonly string[] ModeIds = { "classic", "flip", "nomercy", "allwild", "attack", "dare" };

        static readonly Dictionary<string, CardDef[]> cache = new Dictionary<string, CardDef[]>();
        static readonly Dictionary<string, ModeRules> rules = new Dictionary<string, ModeRules>
        {
            { "classic", new ModeRules { Id = "classic", Stack = "classic", RestrictWildDraw = true } },
            { "flip", new ModeRules { Id = "flip", RestrictWildDraw = true, Flip = true } },
            { "nomercy", new ModeRules { Id = "nomercy", Stack = "nomercy", DrawUntil = true, Mercy = 25, SevenZero = true } },
            { "allwild", new ModeRules { Id = "allwild", AllWild = true } },
            { "attack", new ModeRules { Id = "attack", Launcher = true } },
            { "dare", new ModeRules { Id = "dare", Dares = true } },
        };

        public static string Normalize(string mode) => mode != null && rules.ContainsKey(mode) ? mode : "classic";
        public static ModeRules Rules(string mode) => rules[Normalize(mode)];

        public static CardDef[] For(string mode)
        {
            mode = Normalize(mode);
            if (!cache.TryGetValue(mode, out var deck))
            {
                switch (mode)
                {
                    case "flip": deck = FlipDeck(); break;
                    case "nomercy": deck = NoMercyDeck(); break;
                    case "allwild": deck = AllWildDeck(); break;
                    case "attack": deck = AttackDeck(); break;
                    case "dare": deck = DareDeck(); break;
                    default: deck = ClassicDeck(); break;
                }
                cache[mode] = deck;
            }
            return deck;
        }

        /// <summary>The face of a card on the given side.</summary>
        public static Face FaceOf(string mode, int side, int id)
        {
            var d = For(mode)[id];
            return side == 1 && d.HasB ? d.B : d.A;
        }

        const int W = Face.WildColor;

        static CardDef[] Single(List<Face> faces)
        {
            var res = new CardDef[faces.Count];
            for (int i = 0; i < faces.Count; i++) res[i] = new CardDef { A = faces[i] };
            return res;
        }

        static void Add(List<Face> d, Face f, int count)
        {
            for (int i = 0; i < count; i++) d.Add(f);
        }

        static CardDef[] ClassicDeck()
        {
            var d = new List<Face>();
            for (int c = 0; c < 4; c++)
            {
                d.Add(new Face(c, Kind.Num, 0));
                for (int v = 1; v <= 12; v++)
                {
                    Face f = v <= 9 ? new Face(c, Kind.Num, v) : v == 10 ? new Face(c, Kind.Skip) : v == 11 ? new Face(c, Kind.Reverse) : new Face(c, Kind.Draw, 2);
                    Add(d, f, 2);
                }
            }
            Add(d, new Face(W, Kind.Wild), 4);
            Add(d, new Face(W, Kind.WildDraw, 4), 4);
            return Single(d);
        }

        static CardDef[] FlipDeck()
        {
            var light = new List<Face>();
            var dark = new List<Face>();
            for (int c = 0; c < 4; c++)
            {
                for (int v = 1; v <= 9; v++) Add(light, new Face(c, Kind.Num, v), 2);
                foreach (var f in new[] { new Face(c, Kind.Draw, 1), new Face(c, Kind.Reverse), new Face(c, Kind.Skip), new Face(c, Kind.Flip) }) Add(light, f, 2);
            }
            Add(light, new Face(W, Kind.Wild), 4);
            Add(light, new Face(W, Kind.WildDraw, 2), 4);
            for (int c = 0; c < 4; c++)
            {
                for (int v = 1; v <= 9; v++) Add(dark, new Face(c, Kind.Num, v), 2);
                foreach (var f in new[] { new Face(c, Kind.Draw, 5), new Face(c, Kind.Reverse), new Face(c, Kind.SkipAll), new Face(c, Kind.Flip) }) Add(dark, f, 2);
            }
            Add(dark, new Face(W, Kind.Wild), 4);
            Add(dark, new Face(W, Kind.WildDrawColor), 4);
            // Like a printed deck, each card has a fixed (but unpredictable) dark side.
            var rng = new UnoRng(20190706);
            for (int i = dark.Count - 1; i > 0; i--)
            {
                int j = (int)System.Math.Floor(rng.Next() * (i + 1));
                var t = dark[i]; dark[i] = dark[j]; dark[j] = t;
            }
            var res = new CardDef[light.Count];
            for (int i = 0; i < light.Count; i++) res[i] = new CardDef { A = light[i], B = dark[i], HasB = true };
            return res;
        }

        static CardDef[] NoMercyDeck()
        {
            var d = new List<Face>();
            for (int c = 0; c < 4; c++)
            {
                for (int v = 0; v <= 9; v++) Add(d, new Face(c, Kind.Num, v), 2);
                for (int i = 0; i < 3; i++)
                {
                    d.Add(new Face(c, Kind.Skip));
                    d.Add(new Face(c, Kind.Reverse));
                    d.Add(new Face(c, Kind.Draw, 2));
                    d.Add(new Face(c, Kind.DiscardAll));
                }
                for (int i = 0; i < 2; i++)
                {
                    d.Add(new Face(c, Kind.Draw, 4));
                    d.Add(new Face(c, Kind.SkipAll));
                }
            }
            Add(d, new Face(W, Kind.WildRevDraw, 4), 8);
            Add(d, new Face(W, Kind.WildDraw, 6), 4);
            Add(d, new Face(W, Kind.WildDraw, 10), 4);
            Add(d, new Face(W, Kind.Roulette), 8);
            return Single(d);
        }

        static CardDef[] AllWildDeck()
        {
            var d = new List<Face>();
            Add(d, new Face(W, Kind.Wild), 54);
            Add(d, new Face(W, Kind.Reverse), 14);
            Add(d, new Face(W, Kind.Skip), 14);
            Add(d, new Face(W, Kind.SkipTwo), 6);
            Add(d, new Face(W, Kind.WildDraw, 2), 10);
            Add(d, new Face(W, Kind.WildDraw, 4), 6);
            Add(d, new Face(W, Kind.TargetDraw, 2), 4);
            Add(d, new Face(W, Kind.Swap), 4);
            return Single(d);
        }

        static CardDef[] AttackDeck()
        {
            var d = new List<Face>();
            for (int c = 0; c < 4; c++)
            {
                for (int v = 1; v <= 9; v++) Add(d, new Face(c, Kind.Num, v), 2);
                foreach (var f in new[] { new Face(c, Kind.Hit, 2), new Face(c, Kind.Reverse), new Face(c, Kind.Skip) }) Add(d, f, 2);
                d.Add(new Face(c, Kind.DiscardAll));
                d.Add(new Face(c, Kind.Swap));
            }
            Add(d, new Face(W, Kind.Wild), 4);
            Add(d, new Face(W, Kind.HitFire), 2);
            Add(d, new Face(W, Kind.HitAll), 2);
            return Single(d);
        }

        static CardDef[] DareDeck()
        {
            var d = new List<Face>();
            for (int c = 0; c < 4; c++)
            {
                d.Add(new Face(c, Kind.Num, 0));
                for (int v = 1; v <= 9; v++) Add(d, new Face(c, Kind.Num, v), 2);
                foreach (var f in new[] { new Face(c, Kind.Reverse), new Face(c, Kind.Skip), new Face(c, Kind.Dare, 2) }) Add(d, f, 2);
            }
            Add(d, new Face(W, Kind.WildDare, 4), 8);
            return Single(d);
        }

        /// <summary>Points the winner scores for a card left in a loser's hand.</summary>
        public static int Points(string mode, Face f, int side)
        {
            if (mode == "flip")
            {
                if (f.K == Kind.Num) return f.N;
                if (side == 0) return f.K == Kind.Draw ? 10 : f.K == Kind.Wild ? 40 : f.K == Kind.WildDraw ? 50 : 20;
                return f.K == Kind.SkipAll ? 30 : f.K == Kind.Wild ? 40 : f.K == Kind.WildDrawColor ? 60 : 20;
            }
            if (mode == "allwild") return f.K == Kind.Wild ? 20 : 50;
            if (f.K == Kind.Num) return f.N;
            return f.IsWild ? 50 : 20;
        }
    }
}
