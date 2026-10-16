using UnityEngine;

namespace UnoGame
{
    /// <summary>Game modes shown on the home screen, with the rules shown before matchmaking.</summary>
    public sealed class ModeInfo
    {
        public string Id;
        public string Title;
        public string Tagline;
        public Color Accent;
        public string[] Art;            // card sprites fanned on the tile and the rules screen
        public string[] Rules;
        public (string sprite, string name, string text)[] Specials;

        public static readonly ModeInfo[] All =
        {
            new ModeInfo
            {
                Id = "classic", Title = "Classic UNO", Tagline = "The original and still the standard",
                Accent = Palette.Hex("#E8333C"), Art = new[] { "c_red_7", "c_blue_skip", "w_wd4" },
                Rules = new[]
                {
                    "Match the top card by colour, number or symbol.",
                    "Wild cards go on anything. Wild Draw 4 only when you have no card of the current colour.",
                    "Can't play? Draw one card. If it fits, you can play it straight away.",
                    "Stack +2 and +4 cards to pass the penalty on to your opponent.",
                    "Tap UNO! before you play your second-last card, or you may have to draw 2.",
                    "First to empty their hand wins and scores the cards left in the other hand.",
                },
                Specials = new[]
                {
                    ("c_blue_skip", "Skip", "Your opponent misses a turn, so you go again."),
                    ("c_green_rev", "Reverse", "With two players it works like a Skip."),
                    ("c_yellow_d2", "Draw 2", "Opponent draws 2 and loses the turn (stackable)."),
                    ("w_wild", "Wild", "Choose the next colour."),
                    ("w_wd4", "Wild Draw 4", "Choose a colour and your opponent draws 4."),
                },
            },
            new ModeInfo
            {
                Id = "flip", Title = "UNO Flip", Tagline = "Double-sided deck with a Light and a Dark side",
                Accent = Palette.Hex("#C2369B"), Art = new[] { "c_yellow_flip", "c_pink_d5", "wd_wdc" },
                Rules = new[]
                {
                    "Every card has a Light side and a Dark side. The game starts on the Light side.",
                    "Play a FLIP card and everything turns over: both hands, the draw pile and the discard pile.",
                    "You can see the other side of your opponent's cards and of the draw pile. Plan ahead!",
                    "The Dark side is meaner: Draw 5, Skip Everyone and Wild Draw Color.",
                    "No stacking. Wild draw cards only when you have no card of the current colour.",
                },
                Specials = new[]
                {
                    ("c_red_flip", "Flip", "Turns the whole game over to the other side."),
                    ("c_blue_d1", "Draw 1", "Light side: opponent draws 1 and loses the turn."),
                    ("w_wd2", "Wild Draw 2", "Light side: choose a colour, opponent draws 2."),
                    ("c_pink_d5", "Draw 5", "Dark side: opponent draws 5 and loses the turn."),
                    ("c_teal_skipall", "Skip Everyone", "Dark side: you play again."),
                    ("wd_wdc", "Wild Draw Color", "Dark side: opponent draws until they get your colour."),
                },
            },
            new ModeInfo
            {
                Id = "nomercy", Title = "Show 'Em No Mercy", Tagline = "Brutal, with stacking draw cards",
                Accent = Palette.Hex("#B0122A"), Art = new[] { "w_wd10", "w_roulette", "c_red_d4" },
                Rules = new[]
                {
                    "Stack any draw card of equal or higher value: +2, +4, +6 and +10 keep piling up.",
                    "Can't play? Keep drawing until you get a card you can play.",
                    "Mercy rule: if you ever hold 25 cards, you're out and your opponent wins.",
                    "Playing a 7 or a 0 swaps hands with your opponent.",
                    "Discard All dumps every card of its colour from your hand at once.",
                },
                Specials = new[]
                {
                    ("c_green_discard", "Discard All", "Play it and discard all your cards of that colour."),
                    ("c_yellow_skipall", "Skip Everyone", "You play again."),
                    ("w_wrd4", "Wild Reverse Draw 4", "Reverse, then your opponent draws 4."),
                    ("w_wd6", "Wild Draw 6", "Choose a colour, opponent draws 6."),
                    ("w_wd10", "Wild Draw 10", "Choose a colour, opponent draws 10."),
                    ("w_roulette", "Color Roulette", "Opponent names a colour and draws until it shows up."),
                },
            },
            new ModeInfo
            {
                Id = "allwild", Title = "UNO All Wild", Tagline = "Every card is a wild",
                Accent = Palette.Hex("#F5A623"), Art = new[] { "w_skip2", "w_swap", "w_td2" },
                Rules = new[]
                {
                    "No colours and no numbers: any card can be played on any card.",
                    "It's all about the action cards and when you play them.",
                    "A Forced Swap as your last card hands the win to your opponent, so time it well!",
                    "Tap UNO! before you play your second-last card.",
                },
                Specials = new[]
                {
                    ("w_skip", "Wild Skip", "Your opponent misses a turn."),
                    ("w_skip2", "Wild Skip Two", "Your opponent misses two turns in a row."),
                    ("w_rev", "Wild Reverse", "With two players it works like a Skip."),
                    ("w_wd2", "Wild Draw 2 / 4", "Opponent draws 2 or 4 and loses the turn."),
                    ("w_td2", "Targeted Draw 2", "Opponent draws 2 but still gets their turn."),
                    ("w_swap", "Forced Swap", "Swap hands with your opponent."),
                },
            },
            new ModeInfo
            {
                Id = "attack", Title = "UNO Attack", Tagline = "Card launcher adds chaos",
                Accent = Palette.Hex("#F2731F"), Art = new[] { "c_blue_hit2", "w_hitfire", "c_red_swap" },
                Rules = new[]
                {
                    "There is no draw pile: when you can't (or won't) play, hit the launcher.",
                    "The launcher might fire nothing... or spray a handful of cards at you.",
                    "The longer it goes without firing, the more likely the next hit blows.",
                    "After hitting the launcher your turn is over.",
                },
                Specials = new[]
                {
                    ("c_blue_hit2", "Hit 2", "Opponent hits the launcher twice and loses the turn."),
                    ("w_hitall", "Wild All Hit", "Opponent hits the launcher once."),
                    ("w_hitfire", "Wild Hit-Fire", "Opponent keeps hitting until it fires."),
                    ("c_yellow_discard", "Discard All", "Discard all your cards of that colour."),
                    ("c_red_swap", "Trade Hands", "Swap hands with your opponent."),
                },
            },
            new ModeInfo
            {
                Id = "dare", Title = "UNO Dare", Tagline = "Play a card or do a dare",
                Accent = Palette.Hex("#12A3A6"), Art = new[] { "c_green_dare", "w_dare", "c_yellow_5" },
                Rules = new[]
                {
                    "Dare cards challenge the next player: do the dare or draw.",
                    "Dares are quick on-screen challenges: tap frenzy, reflex test, colour trap or hold steady.",
                    "Complete the dare and you keep your turn. Fail or refuse and you draw the cards.",
                    "Dare cards play on a matching colour or on another Dare.",
                },
                Specials = new[]
                {
                    ("c_green_dare", "Dare", "Next player: do a dare or draw 2."),
                    ("w_dare", "Wild Dare", "Choose a colour. Next player: do a dare or draw 4."),
                    ("c_red_skip", "Skip", "Your opponent misses a turn."),
                    ("c_blue_rev", "Reverse", "With two players it works like a Skip."),
                },
            },
        };

        public static ModeInfo Get(string id)
        {
            foreach (var m in All) if (m.Id == id) return m;
            return All[0];
        }
    }
}
