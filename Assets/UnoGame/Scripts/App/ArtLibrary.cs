using System.Collections.Generic;
using UnoGame.Core;
using UnityEngine;

namespace UnoGame
{
    /// <summary>All game sprites. Filled in by the editor importer (UNO Game > Import Art) from the generated atlases.</summary>
    public sealed class ArtLibrary : ScriptableObject
    {
        // index: colour * 13 + value for the 52 coloured cards, 52 = wild, 53 = wild draw four
        public Sprite[] cardFaces = new Sprite[54];
        public Sprite cardBack;
        public Sprite cardMask;
        public Sprite[] avatars = new Sprite[12];
        public Sprite[] emotes = new Sprite[6];
        public Sprite[] balls = new Sprite[4];
        public Sprite logo;
        public Sprite bgTable;
        public Sprite bgHome;
        public List<Sprite> ui = new List<Sprite>();

        Dictionary<string, Sprite> map;
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

        public Sprite Face(int id)
        {
            if (id < 0) return cardBack;
            int c = Cards.ColorOf(id), v = Cards.ValueOf(id);
            if (c == Cards.WildColor) return cardFaces[v == Cards.Wild ? 52 : 53];
            return cardFaces[c * 13 + v];
        }

        public Sprite Avatar(int i) => avatars[Mathf.Clamp(i, 0, avatars.Length - 1)];
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

        public static Color ForCard(int color)
        {
            switch (color)
            {
                case 0: return Red;
                case 1: return Yellow;
                case 2: return Green;
                case 3: return Blue;
                default: return Color.white;
            }
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
