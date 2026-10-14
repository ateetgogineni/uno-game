namespace UnoGame.Core
{
    /// <summary>
    /// Card ids 0..107 map to a fixed colour and value (same mapping as Server/src/engine.js).
    /// Colours: 0 red, 1 yellow, 2 green, 3 blue, 4 wild.
    /// Values: 0-9 numbers, 10 skip, 11 reverse, 12 draw two, 13 wild, 14 wild draw four.
    /// </summary>
    public static class Cards
    {
        public const int Skip = 10, Reverse = 11, Draw2 = 12, Wild = 13, Wild4 = 14;
        public const int WildColor = 4;
        public const int DeckSize = 108;
        public const int HandSize = 7;

        public static readonly string[] ColorNames = { "Red", "Yellow", "Green", "Blue", "Wild" };

        // Text colours for card names on the dark table (slightly lighter than the card art).
        public static readonly string[] ColorHex = { "#FF5C5C", "#FFD447", "#5ED46E", "#4FA9FF", "#FFFFFF" };

        public static int ColorOf(int id) => id >= 100 ? WildColor : id / 25;

        public static int ValueOf(int id)
        {
            if (id >= 100) return id < 104 ? Wild : Wild4;
            int r = id % 25;
            return r == 0 ? 0 : (r - 1) / 2 + 1;
        }

        public static bool IsWild(int id) => id >= 100;

        public static int Points(int id)
        {
            int v = ValueOf(id);
            if (v <= 9) return v;
            if (v <= Draw2) return 20;
            return 50;
        }

        public static string ValueName(int v)
        {
            switch (v)
            {
                case Skip: return "Skip";
                case Reverse: return "Reverse";
                case Draw2: return "Draw 2";
                case Wild: return "Wild";
                case Wild4: return "Wild Draw 4";
                default: return v.ToString();
            }
        }

        /// <summary>"Red 7", "Blue Skip", "Wild Draw 4".</summary>
        public static string Name(int id)
        {
            int c = ColorOf(id), v = ValueOf(id);
            return c == WildColor ? ValueName(v) : ColorNames[c] + " " + ValueName(v);
        }

        /// <summary>Card name wrapped in a TextMeshPro colour tag.</summary>
        public static string RichName(int id)
        {
            return "<color=" + ColorHex[ColorOf(id)] + ">" + Name(id) + "</color>";
        }

        public static string RichColor(int color)
        {
            return "<color=" + ColorHex[color] + ">" + ColorNames[color] + "</color>";
        }

        /// <summary>Hand order: by colour (red, yellow, green, blue, wild) then value.</summary>
        public static int SortKey(int id) => ColorOf(id) * 1000 + ValueOf(id) * 10 + (id % 2);
    }
}
