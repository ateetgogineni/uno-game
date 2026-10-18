using System.Collections.Generic;
using TMPro;
using UnoGame.Core;
using UnoGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.Game
{
    /// <summary>
    /// Seats around the table. A 1v1 game keeps the scene's opponent fan and panel at the top. Tables of
    /// 3 and 4 stay portrait with compact seats beside and above the table; 5 to 10 players turn to
    /// landscape with the opponents spread evenly along an arc over the table, and chevrons on the arc
    /// showing the direction of play. Seats are listed clockwise from your left (the next player).
    /// </summary>
    public sealed partial class GameController
    {
        PlayerPanel[] panels;
        HandView[] hands;
        bool multi;
        bool landscape;
        RectTransform seatLayer;
        readonly List<RectTransform> ringDots = new List<RectTransform>();
        readonly List<RectTransform> seatRoots = new List<RectTransform>();
        readonly List<RectTransform> seatBubbles = new List<RectTransform>();
        readonly List<RectTransform> chevrons = new List<RectTransform>();
        readonly List<float> chevronAngles = new List<float>();
        Vector2 laidOutFor;
        int chevronDir = 1;
        TargetPicker targets;
        const int RingDots = 96;

        /// <summary>Opponents per landscape table: where the arc starts and ends (degrees) and the seat scale.</summary>
        static readonly Dictionary<int, (float a0, float a1, float scale)> Arc = new Dictionary<int, (float, float, float)>
        {
            { 4, (168f, 12f, 1f) }, { 5, (180f, 0f, 1f) }, { 6, (190f, -10f, 0.95f) },
            { 7, (196f, -16f, 0.9f) }, { 8, (198f, -18f, 0.86f) }, { 9, (201f, -21f, 0.82f) },
        };

        public bool MultiSeat => multi;
        public bool LandscapeTable => landscape;
        public int SeatCount => panels != null ? panels.Length : 2;
        public PlayerPanel SeatPanel(int s) => PanelOf(s);
        public HandView SeatHand(int s) => HandOf(s);
        public int MySeat => seat;

        PlayerPanel PanelOf(int p) => panels != null && p >= 0 && p < panels.Length ? panels[p] : (p == seat ? mePanel : oppPanel);
        HandView HandOf(int p) => hands != null && p >= 0 && p < hands.Length ? hands[p] : (p == seat ? myHand : oppHand);

        /// <summary>Every other seat, clockwise from your left (the order the server lists Flip faces in).</summary>
        IEnumerable<int> Others
        {
            get { for (int k = 1; k < SeatCount; k++) yield return (seat + k) % SeatCount; }
        }

        bool IsOutSeat(int s) => state != null && state.@out != null && s >= 0 && s < state.@out.Length && state.@out[s] == 1;

        /// <summary>Next seat still in the game in direction dir (used to animate hands passing round).</summary>
        int NextSeat(int s, int dir)
        {
            int n = SeatCount;
            for (int i = 1; i <= n; i++)
            {
                int t = ((s + dir * i) % n + n) % n;
                if (!IsOutSeat(t)) return t;
            }
            return s;
        }

        void SetupSeats()
        {
            int n = Mathf.Max(2, session.Size);
            panels = new PlayerPanel[n];
            hands = new HandView[n];
            panels[seat] = mePanel;
            hands[seat] = myHand;
            multi = n > 2;
            landscape = App.IsLandscapeTable(n);
            if (!multi)
            {
                int o = (seat + 1) % n;
                panels[o] = oppPanel;
                hands[o] = oppHand;
                return;
            }

            oppPanel.gameObject.SetActive(false);
            oppHand.gameObject.SetActive(false);
            if (landscape) ApplyLandscape();
            // the UNO Attack launcher is wider than the deck: keep it clear of the side seats
            else launcherImg.rectTransform.localScale = Vector3.one * 0.85f;

            var root = (RectTransform)transform;
            seatLayer = UIKit.Node("Seats", root).Fill();
            seatLayer.SetSiblingIndex(oppHand.transform.GetSiblingIndex() + 1);
            if (landscape)
            {
                // a dotted track round the table, through the seats
                for (int i = 0; i < RingDots; i++)
                {
                    var dot = UIKit.Img(seatLayer, "Dot" + i, UIKit.S("circle"), new Color(1, 1, 1, 0.13f)).rectTransform;
                    dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
                    dot.sizeDelta = new Vector2(7, 7);
                    ringDots.Add(dot);
                }
                for (int k = 0; k < n - 2; k++) chevrons.Add(Chevron(seatLayer, k));
            }
            float scale = landscape ? Arc[Mathf.Clamp(n - 1, 4, 9)].scale : 1.12f;
            for (int k = 1; k < n; k++)
            {
                int s = (seat + k) % n;
                seatRoots.Add(BuildSeat(seatLayer, s, scale, out panels[s], out hands[s]));
            }
            LayoutSeats();
            targets = TargetPicker.Create((RectTransform)picker.transform.parent, landscape);
        }

        static RectTransform Chevron(RectTransform parent, int k)
        {
            var rt = UIKit.Node("Chevron" + k, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(30, 30);
            for (int i = 0; i < 2; i++)
            {
                var bar = UIKit.Img(rt, "Bar" + i, UIKit.S("rounded_small"), new Color(1, 1, 1, 0.32f));
                bar.pixelsPerUnitMultiplier = 6f;
                bar.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, i == 0 ? 5 : -5), new Vector2(18, 6));
                bar.rectTransform.localEulerAngles = new Vector3(0, 0, i == 0 ? -40f : 40f);
            }
            return rt;
        }

        /// <summary>A compact seat: a small fan of cards over the avatar, card-count badge, name and turn timer.</summary>
        RectTransform BuildSeat(RectTransform parent, int s, float sc, out PlayerPanel p, out HandView hand)
        {
            var c = new Vector2(0.5f, 0.5f);
            var rt = UIKit.Node("Seat" + s, parent);
            rt.anchorMin = rt.anchorMax = c;
            rt.sizeDelta = new Vector2(240, 290);
            rt.localScale = Vector3.one * sc;

            hand = UIKit.Node("Hand", rt).At(c, new Vector2(0, 66), new Vector2(150, 90)).gameObject.AddComponent<HandView>();
            hand.prefab = cardPrefab;
            hand.opponent = true;
            hand.fanUp = true;
            hand.cardScale = 0.29f;
            hand.maxWidth = 112f;
            hand.spacingMax = 64f;
            hand.curve = 6f;
            hand.liftHeight = 0f;

            p = rt.gameObject.AddComponent<PlayerPanel>();
            p.ring = UIKit.Img(rt, "Ring", UIKit.S("ring_thick"), new Color(Palette.TurnBlue.r, Palette.TurnBlue.g, Palette.TurnBlue.b, 0f));
            p.ring.rectTransform.At(c, Vector2.zero, new Vector2(152, 152));
            var bg = UIKit.Img(rt, "AvatarBg", UIKit.S("circle"), new Color(0.07f, 0.09f, 0.11f, 0.85f));
            bg.rectTransform.At(c, new Vector2(0, -3), new Vector2(138, 138));
            p.avatar = UIKit.Img(rt, "Avatar", ArtLibrary.I.avatars[0], Color.white);
            p.avatar.rectTransform.At(c, Vector2.zero, new Vector2(124, 124));

            // card count badge, bottom right of the avatar (turns red on one card)
            var border = UIKit.Img(rt, "CountBorder", UIKit.S("rounded"), new Color(1, 1, 1, 0.92f));
            border.pixelsPerUnitMultiplier = 3.8f;
            border.rectTransform.At(c, new Vector2(58, -46), new Vector2(76, 50));
            p.countBadge = UIKit.Img(border.rectTransform, "Badge", UIKit.S("rounded"), Palette.Ink);
            p.countBadge.pixelsPerUnitMultiplier = 4.2f;
            p.countBadge.rectTransform.Fill(3, 3, 3, 3);
            var mini = UIKit.Img(p.countBadge.rectTransform, "Back", ArtLibrary.I.cardBack, Color.white);
            mini.type = Image.Type.Simple;
            mini.rectTransform.At(new Vector2(0, 0.5f), new Vector2(17, 0), new Vector2(16, 22));
            p.countText = UIKit.Txt(p.countBadge.rectTransform, "Text", "7", 30, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            p.countText.rectTransform.Fill(26, 0, 2, 2);
            p.countRoot = border.rectTransform;

            p.unoTag = UIKit.Img(rt, "Uno", UIKit.S("uno_burst"), Color.white).rectTransform;
            p.unoTag.GetComponent<Image>().type = Image.Type.Simple;
            p.unoTag.At(c, new Vector2(-70, 52), new Vector2(104, 64));
            p.unoTag.localEulerAngles = new Vector3(0, 0, 12f);
            p.unoTag.gameObject.SetActive(false);

            p.nameRow = UIKit.Node("NameRow", rt).At(c, new Vector2(0, -94), new Vector2(220, 40));
            var h = p.nameRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 8;
            h.childControlWidth = true;
            h.childControlHeight = false;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            var dot = UIKit.Img(p.nameRow, "Dot", UIKit.S("circle"), Palette.Online);
            dot.rectTransform.sizeDelta = new Vector2(14, 14);
            var le = dot.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 14;
            le.preferredHeight = 14;
            p.nameText = UIKit.Txt(p.nameRow, "Name", "Player", 27, Color.white, TextAlignmentOptions.Left, FontStyles.Bold).Shadowed();
            p.nameText.rectTransform.sizeDelta = new Vector2(190, 40);
            p.nameText.overflowMode = TextOverflowModes.Ellipsis;
            var nle = p.nameText.gameObject.AddComponent<LayoutElement>();
            nle.preferredWidth = -1;
            nle.flexibleWidth = 0;

            p.timerRoot = UIKit.Node("Timer", rt).At(c, new Vector2(0, -126), new Vector2(156, 22));
            var clock = UIKit.Img(p.timerRoot, "Clock", UIKit.S("icon_clock"), new Color(1, 1, 1, 0.8f));
            clock.rectTransform.At(new Vector2(0, 0.5f), new Vector2(11, 0), new Vector2(22, 22));
            var track = UIKit.Img(p.timerRoot, "Track", UIKit.S("rounded_small"), new Color(0, 0, 0, 0.45f));
            track.rectTransform.At(new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(124, 12), new Vector2(0, 0.5f));
            track.pixelsPerUnitMultiplier = 2.6f;
            p.timerFill = UIKit.Img(track.rectTransform, "Fill", UIKit.S("rounded_small"), Palette.Gold);
            p.timerFill.rectTransform.At(new Vector2(0, 0.5f), Vector2.zero, new Vector2(124, 12), new Vector2(0, 0.5f));
            p.timerFill.pixelsPerUnitMultiplier = 2.6f;

            var outRt = UIKit.Node("Out", rt).At(c, Vector2.zero, new Vector2(140, 140));
            p.outGroup = UIKit.Group(outRt, 1f, false);
            var shade = UIKit.Img(outRt, "Shade", UIKit.S("circle"), new Color(0.05f, 0.06f, 0.08f, 0.62f));
            shade.rectTransform.At(c, Vector2.zero, new Vector2(128, 128));
            p.outLabel = UIKit.Txt(outRt, "Label", "OUT", 34, new Color(1f, 0.55f, 0.5f), TextAlignmentOptions.Center, FontStyles.Bold).Shadowed();
            p.outLabel.characterSpacing = 4;
            p.outLabel.rectTransform.At(c, Vector2.zero, new Vector2(140, 50));
            outRt.gameObject.SetActive(false);

            var bubbleRoot = UIKit.Node("BubbleRoot", rt).At(c, new Vector2(118, 30), new Vector2(128, 128));
            p.bubble = bubbleRoot;
            var bubbleImg = UIKit.Img(bubbleRoot, "Bubble", UIKit.S("bubble"), Color.white);
            bubbleImg.rectTransform.Fill();
            p.bubbleEmote = UIKit.Img(bubbleRoot, "Emote", ArtLibrary.I.emotes[0], Color.white);
            p.bubbleEmote.rectTransform.At(c, new Vector2(0, 8), new Vector2(88, 88));
            bubbleRoot.gameObject.SetActive(false);
            seatBubbles.Add(bubbleRoot);
            return rt;
        }

        /// <summary>Places the seats for the current screen size (called again if the screen turns or resizes).</summary>
        void LayoutSeats()
        {
            if (!multi) return;
            var size = ((RectTransform)transform).rect.size;
            laidOutFor = size;
            int m = seatRoots.Count;
            var pos = new Vector2[m];
            if (!landscape)
            {
                float top = size.y / 2f;
                if (m == 2)
                {
                    pos[0] = new Vector2(-310, top - 326);
                    pos[1] = new Vector2(310, top - 326);
                }
                else
                {
                    pos[0] = new Vector2(-410, 260);
                    pos[1] = new Vector2(0, top - 300);
                    pos[2] = new Vector2(410, 260);
                }
            }
            else
            {
                var arc = Arc[Mathf.Clamp(m, 4, 9)];
                // wider screens spread the seats out; taller ones (tablets) lift the arc away from the table
                float rx = Mathf.Max(790f, size.x / 2f - 170f);
                float ry = Mathf.Min(560f, 390f + Mathf.Max(0f, size.y - 1080f) * 0.45f);
                ArcPoints(m, arc.a0, arc.a1, rx, ry, out var seats, out var mids);
                for (int k = 0; k < m; k++) pos[k] = new Vector2(rx * Mathf.Cos(seats[k]), ry * Mathf.Sin(seats[k]));
                for (int i = 0; i < ringDots.Count; i++)
                {
                    float a = i * Mathf.PI * 2f / ringDots.Count;
                    ringDots[i].anchoredPosition = new Vector2(rx * Mathf.Cos(a), ry * Mathf.Sin(a));
                }
                chevronAngles.Clear();
                for (int k = 0; k < chevrons.Count && k < mids.Count; k++)
                {
                    float t = mids[k];
                    chevrons[k].anchoredPosition = new Vector2(rx * Mathf.Cos(t), ry * Mathf.Sin(t));
                    // tangent towards the next seat (clockwise on screen)
                    chevronAngles.Add(Mathf.Atan2(-ry * Mathf.Cos(t), rx * Mathf.Sin(t)) * Mathf.Rad2Deg);
                }
                UpdateChevrons(chevronDir, false);
            }
            for (int k = 0; k < m; k++)
            {
                seatRoots[k].anchoredPosition = pos[k];
                // emote bubbles open towards the middle of the screen
                bool right = pos[k].x > 1f;
                seatBubbles[k].anchoredPosition = new Vector2(right ? -118 : 118, 30);
                seatBubbles[k].GetChild(0).localScale = new Vector3(right ? -1 : 1, 1, 1);
            }
        }

        /// <summary>m points spread evenly by arc length along the ellipse from angle a0 to a1 (degrees), plus the midpoints between them.</summary>
        static void ArcPoints(int m, float a0, float a1, float rx, float ry, out List<float> seats, out List<float> mids)
        {
            const int N = 600;
            var ts = new float[N + 1];
            var len = new float[N + 1];
            Vector2 prev = default;
            for (int i = 0; i <= N; i++)
            {
                float t = (a0 + (a1 - a0) * i / N) * Mathf.Deg2Rad;
                var p = new Vector2(rx * Mathf.Cos(t), ry * Mathf.Sin(t));
                ts[i] = t;
                len[i] = i == 0 ? 0f : len[i - 1] + Vector2.Distance(prev, p);
                prev = p;
            }
            float total = len[N];
            float At(float d)
            {
                for (int i = 0; i <= N; i++) if (len[i] >= d) return ts[i];
                return ts[N];
            }
            seats = new List<float>();
            mids = new List<float>();
            for (int k = 0; k < m; k++) seats.Add(At(m == 1 ? total / 2f : total * k / (m - 1)));
            for (int k = 0; k < m - 1; k++) mids.Add(At(total * (k + 0.5f) / (m - 1)));
        }

        void UpdateChevrons(int dir, bool animate)
        {
            chevronDir = dir >= 0 ? 1 : -1;
            for (int k = 0; k < chevrons.Count && k < chevronAngles.Count; k++)
            {
                float a = chevronAngles[k] + (chevronDir < 0 ? 180f : 0f);
                if (animate) Tw.Rot(chevrons[k], a, 0.45f, Ease.OutBack);
                else chevrons[k].localEulerAngles = new Vector3(0, 0, a);
            }
        }

        /// <summary>Moves the portrait-built table into its landscape places (5+ players).</summary>
        void ApplyLandscape()
        {
            var scaler = GetComponentInParent<CanvasScaler>();
            if (scaler != null) scaler.referenceResolution = new Vector2(1920, 1080);
            if (tableBg != null && ArtLibrary.I.bgTableLand != null)
            {
                tableBg.sprite = ArtLibrary.I.bgTableLand;
                var fit = tableBg.GetComponent<AspectRatioFitter>();
                if (fit != null) fit.aspectRatio = 1920f / 1080f;
            }

            tableCenter.anchoredPosition = new Vector2(0, 58);
            tableCenter.localScale = Vector3.one * 0.89f;
            modeChip.anchoredPosition = new Vector2(-110, 190);
            arrowA.anchoredPosition = new Vector2(-365, 150);
            arrowB.anchoredPosition = new Vector2(371, -96);
            ((RectTransform)passButton.transform).anchoredPosition = new Vector2(-440, 0);

            var logRt = (RectTransform)log.transform;
            logRt.anchoredPosition = new Vector2(0, -154);
            log.sizeScale = 0.82f;
            for (int i = 0; i < log.lines.Length; i++)
            {
                log.lines[i].rectTransform.anchoredPosition = new Vector2(0, 46 - i * 46);
                log.lines[i].rectTransform.sizeDelta = new Vector2(1000, 38);
            }

            var handRt = (RectTransform)myHand.transform;
            handRt.anchoredPosition = new Vector2(0, 132);
            handRt.sizeDelta = new Vector2(1400, 300);
            myHand.cardScale = 0.74f;
            myHand.maxWidth = 900f;

            var me = (RectTransform)mePanel.transform;
            me.anchorMin = me.anchorMax = Vector2.zero;
            me.anchoredPosition = new Vector2(330, 162);
            me.localScale = Vector3.one * 0.86f;

            var uno = (RectTransform)unoButton.transform.parent.parent;
            uno.anchorMin = uno.anchorMax = new Vector2(1, 0);
            uno.anchoredPosition = new Vector2(-280, 150);
            uno.localScale = Vector3.one * 0.92f;
            var emote = (RectTransform)emoteButton.transform;
            emote.anchoredPosition = new Vector2(-74, 90);
            emote.sizeDelta = new Vector2(96, 96);

            // overlays: centred, and scaled to fit the 1080 high screen
            picker.panel.anchoredPosition = Vector2.zero;
            picker.panel.localScale = Vector3.one * 0.8f;
            emotePanel.panel.anchoredPosition = new Vector2(-40, 150);
            banner.rays.rectTransform.anchoredPosition = new Vector2(0, 40);
            banner.text.rectTransform.anchoredPosition = new Vector2(0, 40);
            toast.pill.anchoredPosition = new Vector2(0, 190);
            result.baseScale = 0.7f;
            result.rays.rectTransform.anchoredPosition = new Vector2(0, 290);
            dare.panel.anchoredPosition = Vector2.zero;
            dare.panel.localScale = Vector3.one * 0.78f;
            dialog.panel.anchoredPosition = Vector2.zero;
        }

        // ---------------------------------------------------------------- targeted cards (3+ players)

        bool NeedsTarget(Face f)
        {
            if (!multi || state == null) return false;
            int active = 0;
            for (int i = 0; i < SeatCount; i++) if (!IsOutSeat(i)) active++;
            if (active <= 2) return false;
            return f.K == Kind.Swap || f.K == Kind.TargetDraw || (Decks.Rules(Table.Mode).SevenZero && f.K == Kind.Num && f.N == 7);
        }

        static string TargetHeading(Face f) =>
            f.K == Kind.TargetDraw ? "Who draws " + f.N + "?" : "Swap hands with...";

        List<(int seat, PlayerInfo info, int count)> TargetOptions()
        {
            var list = new List<(int, PlayerInfo, int)>();
            foreach (int s in Others)
            {
                if (IsOutSeat(s)) continue;
                int count = state.counts != null && s < state.counts.Length ? state.counts[s] : HandOf(s).Cards.Count;
                list.Add((s, session.Players[s], count));
            }
            return list;
        }

        /// <summary>After the colour (if any): pick who a targeted card goes to, then play it.</summary>
        void ChooseTargetThen(CardView cv, int color)
        {
            var f = Table.FaceOf(cv.Id);
            if (!NeedsTarget(f)) { CommitPlay(cv, color, -1); return; }
            awaiting = true;
            targets.Show(TargetHeading(f), TargetOptions(), t =>
            {
                awaiting = false;
                if (t >= 0) CommitPlay(cv, color, t);
            });
        }

        /// <summary>Cards fly from one seat's hand to another's (swaps, rotations); the state that follows rebuilds both hands.</summary>
        void FlyHand(int from, int to)
        {
            var src = HandOf(from);
            var cards = new List<CardView>(src.Cards);
            src.Cards.Clear();
            Vector3 dest = HandOf(to).transform.position;
            float scale = HandOf(to) == myHand ? myHand.cardScale : HandOf(to).cardScale;
            for (int i = 0; i < cards.Count; i++)
            {
                var cv = cards[i];
                cv.transform.SetParent(fxLayer, true);
                Tw.WorldPos(cv.transform, dest, 0.5f, Ease.InOutCubic, i * 0.01f);
                Tw.Scale(cv.Rect, scale, 0.5f, Ease.InOutCubic);
                if (from == seat) cv.Flip(false, -1, 0.3f);
                Destroy(cv.gameObject, 0.6f);
            }
            if (from != seat) PanelOf(from).SetCount(0);
        }
    }
}
