using System.Collections.Generic;
using UnoGame.Core;
using UnityEngine;

namespace UnoGame
{
    /// <summary>All game sprites. Filled in by the editor importer (UNO Game > Import Art) from the generated atlases.</summary>
    public sealed class ArtLibrary : ScriptableObject
    {
        public List<Sprite> cards = new List<Sprite>();   // every card face, named as Cards.SpriteName
        public Sprite cardBack;
        public Sprite cardMask;
        public Sprite[] avatars = new Sprite[12];
        public Sprite[] emotes = new Sprite[6];
        public Sprite[] balls = new Sprite[4];
        public Sprite[] darkBalls = new Sprite[4];
        public Sprite logo;
        public Sprite bgTable;
        public Sprite bgTableLand;   // landscape table (5+ players)
        public Sprite bgHome;
        public List<Sprite> ui = new List<Sprite>();

        Dictionary<string, Sprite> map;
        Dictionary<string, Sprite> cardMap;
        static ArtLibrary inst;

        public static ArtLibrary I
        {
            get
            {
                if (inst == null) inst = Resources.Load<ArtLibrary>("ArtLibrary");
                return inst;
            }
        }

        public Sprite Get(string name)
        {
            if (map == null)
            {
                map = new Dictionary<string, Sprite>();
                foreach (var s in ui) if (s != null) map[s.name] = s;
            }
            if (map.TryGetValue(name, out var sprite)) return sprite;
            Debug.LogWarning("[Art] missing sprite " + name);
            return null;
        }

        public Sprite Card(string name)
        {
            if (cardMap == null)
            {
                cardMap = new Dictionary<string, Sprite>();
                foreach (var s in cards) if (s != null) cardMap[s.name] = s;
            }
            if (cardMap.TryGetValue(name, out var sprite)) return sprite;
            Debug.LogWarning("[Art] missing card " + name);
            return cardBack;
        }

        public Sprite FaceSprite(Face f, int side) => Card(Cards.SpriteName(f, side));

        /// <summary>A card of the match on screen (current mode and Flip side).</summary>
        public Sprite Face(int id) => id < 0 ? cardBack : FaceSprite(Table.FaceOf(id), Table.Side);

        public Sprite Avatar(int i) => avatars[Mathf.Clamp(i, 0, avatars.Length - 1)];

        public Sprite Ball(int c, int side) => side == 1 ? darkBalls[c] : balls[c];
    }

    public static class Palette
    {
        public static readonly Color Red = Hex("#E8333C");
        public static readonly Color Yellow = Hex("#F8BF1F");
        public static readonly Color Green = Hex("#39AE4A");
        public static readonly Color Blue = Hex("#1E85E0");
        public static readonly Color Ink = Hex("#1B1830");
        public static readonly Color Panel = Hex("#1F2A2E");
        public static readonly Color TextDim = new Color(1f, 1f, 1f, 0.55f);
        public static readonly Color Online = Hex("#4CD964");
        public static readonly Color Gold = Hex("#FFC21A");
        public static readonly Color TurnBlue = Hex("#2F8CFF");
        static readonly Color[] Dark = { Hex("#E0399C"), Hex("#0FA0A3"), Hex("#F2731F"), Hex("#7A3FCB") };

        public static Color ForCard(int color) => ForCard(color, 0);

        public static Color ForCard(int color, int side)
        {
            if (color < 0 || color > 3) return Color.white;
            if (side == 1) return Dark[color];
            switch (color)
            {
                case 0: return Red;
                case 1: return Yellow;
                case 2: return Green;
                default: return Blue;
            }
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
