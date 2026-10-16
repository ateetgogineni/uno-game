namespace UnoGame.Core
{
    /// <summary>
    /// Card naming, sorting and sprite lookup for every mode. A card's face depends on the mode's deck and,
    /// in UNO Flip, on which side is up; Table holds the mode/side of the match on screen.
    /// </summary>
    public static class Cards
    {
        public const int HandSize = 7;
        public const int WildColor = Face.WildColor;

        public static readonly string[] LightNames = { "Red", "Yellow", "Green", "Blue" };
        public static readonly string[] DarkNames = { "Pink", "Teal", "Orange", "Purple" };
        // Text colours for card names on the dark table (slightly lighter than the card art).
        static readonly string[] LightHex = { "#FF5C5C", "#FFD447", "#5ED46E", "#4FA9FF" };
        static readonly string[] DarkHex = { "#FF6FC4", "#3FE0DD", "#FF9A4D", "#B98AFF" };

        public static string ColorName(int c, int side) => c >= 0 && c < 4 ? (side == 1 ? DarkNames[c] : LightNames[c]) : "Wild";
        public static string ColorHex(int c, int side) => c >= 0 && c < 4 ? (side == 1 ? DarkHex[c] : LightHex[c]) : "#FFFFFF";

        public static string KindName(Face f)
        {
            switch (f.K)
            {
                case Kind.Num: return f.N.ToString();
                case Kind.Skip: return f.IsWild ? "Wild Skip" : "Skip";
                case Kind.Reverse: return f.IsWild ? "Wild Reverse" : "Reverse";
                case Kind.Draw: return "Draw " + f.N;
                case Kind.Wild: return "Wild";
                case Kind.WildDraw: return "Wild Draw " + f.N;
                case Kind.Flip: return "Flip";
                case Kind.SkipAll: return "Skip Everyone";
                case Kind.WildDrawColor: return "Wild Draw Color";
                case Kind.DiscardAll: return "Discard All";
                case Kind.WildRevDraw: return "Wild Reverse Draw 4";
                case Kind.Roulette: return "Color Roulette";
                case Kind.SkipTwo: return "Wild Skip Two";
                case Kind.TargetDraw: return "Targeted Draw 2";
                case Kind.Swap: return f.IsWild ? "Forced Swap" : "Trade Hands";
                case Kind.Hit: return "Hit " + f.N;
                case Kind.HitAll: return "Wild All Hit";
                case Kind.HitFire: return "Wild Hit-Fire";
                case Kind.Dare: return "Dare";
                case Kind.WildDare: return "Wild Dare";
                default: return "Card";
            }
        }

        /// <summary>"Red 7", "Pink Draw 5", "Wild Draw 6".</summary>
        public static string Name(Face f, int side) => f.IsWild ? KindName(f) : ColorName(f.C, side) + " " + KindName(f);

        /// <summary>Card name wrapped in a TextMeshPro colour tag.</summary>
        public static string RichName(Face f, int side) => "<color=" + ColorHex(f.C, side) + ">" + Name(f, side) + "</color>";

        public static string RichColor(int color, int side) =>
            color >= 0 && color < 4 ? "<color=" + ColorHex(color, side) + ">" + ColorName(color, side) + "</color>" : "any colour";

        /// <summary>Hand order: by colour (wilds last), then kind, then number.</summary>
        public static int SortKey(Face f, int id) => f.C * 100000 + f.K * 1000 + f.N * 10 + (id % 10);

        static readonly string[] LightSprite = { "red", "yellow", "green", "blue" };
        static readonly string[] DarkSprite = { "pink", "teal", "orange", "purple" };

        /// <summary>Sprite name in the card atlases (Tools/art/generate.js uses the same names).</summary>
        public static string SpriteName(Face f, int side)
        {
            if (f.IsWild)
            {
                if (side == 1 && f.K == Kind.Wild) return "wd_wild";
                if (f.K == Kind.WildDrawColor) return "wd_wdc";
                switch (f.K)
                {
                    case Kind.Wild: return "w_wild";
                    case Kind.WildDraw: return "w_wd" + f.N;
                    case Kind.WildRevDraw: return "w_wrd4";
                    case Kind.Roulette: return "w_roulette";
                    case Kind.Reverse: return "w_rev";
                    case Kind.Skip: return "w_skip";
                    case Kind.SkipTwo: return "w_skip2";
                    case Kind.TargetDraw: return "w_td2";
                    case Kind.Swap: return "w_swap";
                    case Kind.HitAll: return "w_hitall";
                    case Kind.HitFire: return "w_hitfire";
                    case Kind.WildDare: return "w_dare";
                    default: return "w_wild";
                }
            }
            string color = side == 1 ? DarkSprite[f.C] : LightSprite[f.C];
            string token;
            switch (f.K)
            {
                case Kind.Num: token = f.N.ToString(); break;
                case Kind.Skip: token = "skip"; break;
                case Kind.Reverse: token = "rev"; break;
                case Kind.Draw: token = "d" + f.N; break;
                case Kind.Flip: token = "flip"; break;
                case Kind.SkipAll: token = "skipall"; break;
                case Kind.DiscardAll: token = "discard"; break;
                case Kind.Hit: token = "hit" + f.N; break;
                case Kind.Swap: token = "swap"; break;
                case Kind.Dare: token = "dare"; break;
                default: token = "0"; break;
            }
            return "c_" + color + "_" + token;
        }
    }

    /// <summary>Mode and Flip side of the match on screen (set by the game controller).</summary>
    public static class Table
    {
        public static string Mode = "classic";
        public static int Side;
        public static Face FaceOf(int id) => Decks.FaceOf(Mode, Side, id);
    }
}
