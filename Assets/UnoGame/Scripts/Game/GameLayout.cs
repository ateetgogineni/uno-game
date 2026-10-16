using TMPro;
using UnoGame.Core;
using UnoGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.Game
{
    /// <summary>
    /// Builds the game table hierarchy. Called by the editor scene builder (UNO Game > Rebuild Scenes);
    /// the result is saved in Game.unity. Layout is for a 1080-wide portrait canvas (height 1920+).
    /// </summary>
    public sealed partial class GameController
    {
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        static readonly Vector2 Top = new Vector2(0.5f, 1f);
        static readonly Vector2 Bottom = new Vector2(0.5f, 0f);

        public void Build(CardView prefab, RectTransform overlays)
        {
            cardPrefab = prefab;
            var root = (RectTransform)transform;
            root.Fill();

            // ---- top bar
            backButton = UIKit.IconBtn(root, "BackButton", "icon_back", 96);
            ((RectTransform)backButton.transform).At(new Vector2(0, 1), new Vector2(76, -76), new Vector2(96, 96));
            menuButton = UIKit.IconBtn(root, "MenuButton", "icon_dots", 96);
            ((RectTransform)menuButton.transform).At(new Vector2(1, 1), new Vector2(-76, -76), new Vector2(96, 96));

            // ---- opponent
            oppHand = UIKit.Node("OppHand", root).At(Top, new Vector2(0, -40), new Vector2(1000, 200)).gameObject.AddComponent<HandView>();
            oppHand.prefab = prefab;
            oppHand.opponent = true;
            oppHand.cardScale = 0.66f;
            oppHand.maxWidth = 760f;
            oppHand.spacingMax = 96f;
            oppHand.curve = 10f;
            oppHand.transform.SetSiblingIndex(0);
            oppPanel = BuildPlayerPanel(root, false);

            // ---- table centre
            tableCenter = UIKit.Node("Table", root).At(Center, new Vector2(0, 170), new Vector2(700, 520));
            colorGlow = UIKit.Img(tableCenter, "ColorGlow", UIKit.S("glow"), new Color(1, 1, 1, 0f));
            colorGlow.rectTransform.At(Center, new Vector2(60, 0), new Vector2(600, 600));
            deckGlow = UIKit.Img(tableCenter, "DeckGlow", UIKit.S("card_glow"), new Color(Palette.TurnBlue.r, Palette.TurnBlue.g, Palette.TurnBlue.b, 0f));
            deckGlow.rectTransform.At(Center, new Vector2(-180, 0), new Vector2(268 * DeckScale * 1.08f, 348 * DeckScale * 1.08f));

            arrowA = UIKit.Img(tableCenter, "ArrowA", UIKit.S("icon_arrow_dir"), new Color(1, 1, 1, 0.28f)).rectTransform;
            arrowA.At(Center, new Vector2(-170, 235), new Vector2(130, 130));
            arrowB = UIKit.Img(tableCenter, "ArrowB", UIKit.S("icon_arrow_dir"), new Color(1, 1, 1, 0.28f)).rectTransform;
            arrowB.At(Center, new Vector2(255, -150), new Vector2(130, 130));
            arrowB.localEulerAngles = new Vector3(0, 0, 180);

            drawPile = UIKit.Node("DrawPile", tableCenter).At(Center, new Vector2(-180, 0), new Vector2(CardView.Width, CardView.Height));
            drawPile.localScale = Vector3.one * DeckScale;
            var deckShadow = UIKit.Img(drawPile, "Shadow", UIKit.S("card_shadow"), new Color(1, 1, 1, 0.9f));
            deckShadow.rectTransform.Fill(-23, -41, -33, -15);
            var layers = new System.Collections.Generic.List<Image> { deckShadow };
            for (int i = 2; i >= 0; i--)
            {
                var back = UIKit.Img(drawPile, "Back" + i, ArtLibrary.I.cardBack, i == 0 ? Color.white : new Color(0.82f, 0.82f, 0.86f), i == 0);
                back.type = Image.Type.Simple;
                back.rectTransform.Fill(i * 5, -i * 5, -i * 5, i * 5);
                if (i == 0)
                {
                    drawButton = back.gameObject.AddComponent<Button>();
                    deckTop = back;
                }
                else layers.Add(back);
            }
            deckLayers = layers.ToArray();
            drawButton.transition = Selectable.Transition.None;

            // UNO Attack: the card launcher replaces the draw pile (taps still reach the button underneath).
            launcherImg = UIKit.Img(tableCenter, "Launcher", UIKit.S("launcher"), Color.white);
            launcherImg.type = Image.Type.Simple;
            launcherImg.rectTransform.At(Center, new Vector2(-180, 14), new Vector2(236, 272));

            // Mode / Flip side badge above the discard pile.
            modeChip = UIKit.Chip(tableCenter, "ModeChip", 54, null, 22f, 10f);
            // left-anchored so longer labels grow to the right, away from the direction arrow
            modeChip.At(Center, new Vector2(-84, 296), new Vector2(240, 54), new Vector2(0, 0.5f));
            modeIcon = UIKit.LayoutIcon(modeChip, "Dot", UIKit.S("circle"), 22, Palette.Red);
            modeText = UIKit.Txt(modeChip, "Text", "CLASSIC", 28, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            modeText.characterSpacing = 4;
            modeText.rectTransform.sizeDelta = new Vector2(200, 54);

            discardPile = UIKit.Node("DiscardPile", tableCenter).At(Center, new Vector2(60, 0), new Vector2(CardView.Width, CardView.Height));

            stackBadge = UIKit.Img(tableCenter, "StackBadge", UIKit.S("circle"), Palette.Red).rectTransform;
            stackBadge.At(Center, new Vector2(215, 150), new Vector2(112, 112));
            var badgeRing = UIKit.Img(stackBadge, "Ring", UIKit.S("ring"), Color.white);
            badgeRing.rectTransform.Fill(-4, -4, -4, -4);
            stackText = UIKit.Txt(stackBadge, "Text", "+2", 52, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).Shadowed();
            stackText.rectTransform.Fill(0, 4, 0, 0);

            passButton = UIKit.Btn(tableCenter, "PassButton", UIKit.S("btn_yellow"), new Vector2(230, 100), "PASS", 44, Palette.Ink);
            ((RectTransform)passButton.transform).At(Center, new Vector2(-180, -205), new Vector2(230, 100));

            // ---- log
            var logRt = UIKit.Node("Log", root).At(Center, new Vector2(0, -140), new Vector2(1000, 170));
            log = logRt.gameObject.AddComponent<LogView>();
            for (int i = 0; i < 3; i++)
            {
                var line = UIKit.Txt(logRt, "Line" + i, "", 38, Color.white);
                line.rectTransform.At(Center, new Vector2(0, 54 - i * 54), new Vector2(1000, 52));
                line.enableAutoSizing = true;
                line.fontSizeMin = 24;
                line.fontSizeMax = 42;
                log.lines[i] = line;
            }

            // ---- local player
            myHand = UIKit.Node("MyHand", root).At(Bottom, new Vector2(0, 430), new Vector2(1080, 300)).gameObject.AddComponent<HandView>();
            myHand.prefab = prefab;
            myHand.cardScale = 1f;
            myHand.maxWidth = 1000f;
            myHand.spacingMax = 140f;
            mePanel = BuildPlayerPanel(root, true);

            // UNO! button (bottom left, shown when you are about to play your second-last card)
            var unoRt = UIKit.Node("UnoButtonRoot", root).At(new Vector2(0, 0), new Vector2(150, 210), new Vector2(250, 154));
            unoGlow = UIKit.Img(unoRt, "Glow", UIKit.S("glow"), new Color(1f, 0.85f, 0.2f, 0f));
            unoGlow.rectTransform.At(Center, Vector2.zero, new Vector2(380, 380));
            var pulse = UIKit.Node("Pulse", unoRt).Fill();
            pulse.gameObject.AddComponent<Pulse>().amount = 0.05f;
            unoButton = UIKit.Btn(pulse, "UnoButton", UIKit.S("uno_burst"), new Vector2(250, 154));
            ((RectTransform)unoButton.transform).At(Center, Vector2.zero, new Vector2(250, 154));
            unoButton.GetComponent<Image>().type = Image.Type.Simple;

            emoteButton = UIKit.Btn(root, "EmoteButton", ArtLibrary.I.emotes[0], new Vector2(112, 112));
            ((RectTransform)emoteButton.transform).At(new Vector2(1, 0), new Vector2(-88, 660), new Vector2(112, 112));
            emoteButton.GetComponent<Image>().type = Image.Type.Simple;

            // reconnect banner
            reconnectBanner = UIKit.Img(root, "ReconnectBanner", UIKit.S("rounded"), new Color(0.75f, 0.2f, 0.2f, 0.95f)).rectTransform;
            reconnectBanner.At(Top, new Vector2(0, -170), new Vector2(520, 80));
            ((Image)reconnectBanner.GetComponent<Image>()).pixelsPerUnitMultiplier = 1.6f;
            var spinner = UIKit.Img(reconnectBanner, "Spinner", UIKit.S("icon_replay"), Color.white);
            spinner.rectTransform.At(new Vector2(0, 0.5f), new Vector2(56, 0), new Vector2(44, 44));
            spinner.gameObject.AddComponent<Spin>().degreesPerSecond = -300f;
            var rtext = UIKit.Txt(reconnectBanner, "Text", "Reconnecting...", 36, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            rtext.rectTransform.Fill(60, 0, 10, 0);

            fxLayer = UIKit.Node("FxLayer", root).Fill();

            BuildOverlays(overlays);
        }

        PlayerPanel BuildPlayerPanel(RectTransform root, bool me)
        {
            var rt = UIKit.Node(me ? "MePanel" : "OppPanel", root).At(me ? Bottom : Top, new Vector2(0, me ? 200 : -300), new Vector2(400, 300));
            var p = rt.gameObject.AddComponent<PlayerPanel>();
            p.isMe = me;
            p.ring = UIKit.Img(rt, "Ring", UIKit.S("ring_thick"), new Color(Palette.TurnBlue.r, Palette.TurnBlue.g, Palette.TurnBlue.b, 0f));
            p.ring.rectTransform.At(Center, Vector2.zero, new Vector2(156, 156));
            var avatarBg = UIKit.Img(rt, "AvatarShadow", UIKit.S("circle"), new Color(0, 0, 0, 0.35f));
            avatarBg.rectTransform.At(Center, new Vector2(0, -5), new Vector2(126, 126));
            p.avatar = UIKit.Img(rt, "Avatar", ArtLibrary.I.avatars[0], Color.white);
            p.avatar.rectTransform.At(Center, Vector2.zero, new Vector2(124, 124));

            float nameY = me ? -100 : 90;
            p.nameRow = UIKit.Node("NameRow", rt).At(Center, new Vector2(0, nameY), new Vector2(400, 50));
            var h = p.nameRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 10;
            h.childControlWidth = true;
            h.childControlHeight = false;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            var dot = UIKit.Img(p.nameRow, "Dot", UIKit.S("circle"), Palette.Online);
            dot.rectTransform.sizeDelta = new Vector2(20, 20);
            var le = dot.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 20;
            le.preferredHeight = 20;
            p.nameText = UIKit.Txt(p.nameRow, "Name", "Player", 36, new Color(1, 1, 1, 0.9f));
            p.nameText.rectTransform.sizeDelta = new Vector2(200, 50);

            if (!me)
            {
                p.countText = UIKit.Txt(rt, "Count", "7", 58, Color.white, TextAlignmentOptions.Left, FontStyles.Bold).Shadowed();
                p.countText.rectTransform.At(Center, new Vector2(150, 2), new Vector2(120, 80));
            }
            else
            {
                p.turnPill = UIKit.Img(rt, "TurnPill", UIKit.S("rounded"), Palette.TurnBlue).rectTransform;
                p.turnPill.At(Center, new Vector2(0, nameY), new Vector2(236, 66));
                p.turnPill.GetComponent<Image>().pixelsPerUnitMultiplier = 1.95f;
                var label = UIKit.Txt(p.turnPill, "Label", "Your Turn", 36, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                label.rectTransform.Fill(0, 2, 0, 0);
            }

            float timerY = me ? -150 : 126;
            p.timerRoot = UIKit.Node("Timer", rt).At(Center, new Vector2(0, timerY), new Vector2(176, 26));
            var clock = UIKit.Img(p.timerRoot, "Clock", UIKit.S("icon_clock"), new Color(1, 1, 1, 0.8f));
            clock.rectTransform.At(new Vector2(0, 0.5f), new Vector2(13, 0), new Vector2(26, 26));
            var track = UIKit.Img(p.timerRoot, "Track", UIKit.S("rounded_small"), new Color(0, 0, 0, 0.45f));
            track.rectTransform.At(new Vector2(0, 0.5f), new Vector2(36, 0), new Vector2(140, 14), new Vector2(0, 0.5f));
            track.pixelsPerUnitMultiplier = 2.6f;
            p.timerFill = UIKit.Img(track.rectTransform, "Fill", UIKit.S("rounded_small"), Palette.Gold);
            p.timerFill.rectTransform.At(new Vector2(0, 0.5f), Vector2.zero, new Vector2(140, 14), new Vector2(0, 0.5f));
            p.timerFill.pixelsPerUnitMultiplier = 2.6f;

            // emote bubble: right of my avatar, left of the opponent's (their count sits on the right)
            var bubbleRoot = UIKit.Node("BubbleRoot", rt).At(Center, new Vector2(me ? 172 : -172, me ? 10 : -6), new Vector2(150, 150));
            p.bubble = bubbleRoot;
            var bubbleImg = UIKit.Img(bubbleRoot, "Bubble", UIKit.S("bubble"), Color.white);
            bubbleImg.rectTransform.Fill();
            if (!me) bubbleImg.rectTransform.localScale = new Vector3(-1, 1, 1);
            p.bubbleEmote = UIKit.Img(bubbleRoot, "Emote", ArtLibrary.I.emotes[0], Color.white);
            p.bubbleEmote.rectTransform.At(Center, new Vector2(0, 10), new Vector2(104, 104));
            p.bubble.gameObject.SetActive(false);
            return p;
        }

        void BuildOverlays(RectTransform overlays)
        {
            // colour picker
            var pk = UIKit.Node("ColorPicker", overlays).Fill();
            picker = pk.gameObject.AddComponent<ColorPicker>();
            picker.group = UIKit.Group(pk);
            var pdim = UIKit.Dim(pk, 0.55f);
            picker.dim = pdim.gameObject.AddComponent<Button>();
            picker.dim.transition = Selectable.Transition.None;
            picker.panel = UIKit.Img(pk, "Panel", UIKit.S("panel"), new Color(1, 1, 1, 0.97f), true).rectTransform;
            picker.panel.At(Center, new Vector2(0, 170), new Vector2(640, 640));
            var ptitle = UIKit.Txt(picker.panel, "Title", "Choose a color", 48, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            ptitle.rectTransform.At(Center, new Vector2(0, 245), new Vector2(590, 70));
            ptitle.enableAutoSizing = true;
            ptitle.fontSizeMin = 30;
            ptitle.fontSizeMax = 48;
            picker.title = ptitle;
            string[] ballNames = { "ball_red", "ball_yellow", "ball_green", "ball_blue" };
            Vector2[] ballPos = { new Vector2(-122, 60), new Vector2(122, 60), new Vector2(-122, -172), new Vector2(122, -172) };
            for (int i = 0; i < 4; i++)
            {
                var b = UIKit.Btn(picker.panel, "Ball" + i, UIKit.S(ballNames[i]), new Vector2(210, 210));
                ((RectTransform)b.transform).At(Center, ballPos[i], new Vector2(210, 210));
                b.GetComponent<Image>().type = Image.Type.Simple;
                picker.balls[i] = b;
            }
            pk.gameObject.SetActive(false);

            // emote picker
            var ep = UIKit.Node("EmotePanel", overlays).Fill();
            emotePanel = ep.gameObject.AddComponent<EmotePanel>();
            var eclose = UIKit.Dim(ep, 0f);
            emotePanel.closeArea = eclose.gameObject.AddComponent<Button>();
            emotePanel.closeArea.transition = Selectable.Transition.None;
            var esafe = UIKit.Node("Safe", ep).Fill();
            esafe.gameObject.AddComponent<SafeArea>();
            emotePanel.panel = UIKit.Img(esafe, "Panel", UIKit.S("panel"), Color.white, true).rectTransform;
            emotePanel.panel.At(new Vector2(1, 0), new Vector2(-40, 730), new Vector2(440, 310), new Vector2(1, 0));
            for (int i = 0; i < Emotes.Count; i++)
            {
                var b = UIKit.Btn(emotePanel.panel, "Emote" + i, ArtLibrary.I.emotes[i], new Vector2(118, 118));
                b.GetComponent<Image>().type = Image.Type.Simple;
                ((RectTransform)b.transform).At(new Vector2(0, 1), new Vector2(90 + (i % 3) * 130, -88 - (i / 3) * 134), new Vector2(118, 118));
                emotePanel.emotes[i] = b;
            }
            ep.gameObject.SetActive(false);

            // menu
            var mn = UIKit.Node("Menu", overlays).Fill();
            menu = mn.gameObject.AddComponent<MenuPanel>();
            var mclose = UIKit.Dim(mn, 0f);
            menu.closeArea = mclose.gameObject.AddComponent<Button>();
            menu.closeArea.transition = Selectable.Transition.None;
            var msafe = UIKit.Node("Safe", mn).Fill();
            msafe.gameObject.AddComponent<SafeArea>();
            menu.panel = UIKit.Img(msafe, "Panel", UIKit.S("panel"), Color.white, true).rectTransform;
            menu.panel.At(new Vector2(1, 1), new Vector2(-30, -140), new Vector2(440, 250), new Vector2(1, 1));
            menu.soundButton = MenuRow(menu.panel, "Sound", "icon_sound_on", "Sound: On", -70, out menu.soundIcon, out menu.soundLabel);
            menu.leaveButton = MenuRow(menu.panel, "Leave", "icon_home", "Leave game", -180, out _, out _);
            mn.gameObject.SetActive(false);

            // banner
            var bn = UIKit.Node("Banner", overlays).Fill();
            banner = bn.gameObject.AddComponent<Banner>();
            banner.group = UIKit.Group(bn, 1f, false);
            banner.rays = UIKit.Img(bn, "Rays", UIKit.S("rays"), new Color(1f, 0.85f, 0.3f, 0.35f));
            banner.rays.rectTransform.At(Center, new Vector2(0, 170), new Vector2(1100, 1100));
            banner.text = UIKit.Txt(bn, "Text", "YOU WON!", 150, Palette.Gold, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic).Shadowed();
            banner.text.rectTransform.At(Center, new Vector2(0, 170), new Vector2(1000, 220));
            bn.gameObject.SetActive(false);

            // toast
            var ts = UIKit.Node("Toast", overlays).Fill();
            toast = ts.gameObject.AddComponent<Toast>();
            toast.group = UIKit.Group(ts, 1f, false);
            toast.pill = UIKit.Img(ts, "Pill", UIKit.S("rounded"), new Color(0.06f, 0.08f, 0.1f, 0.92f)).rectTransform;
            toast.pill.At(Center, new Vector2(0, 540), new Vector2(700, 96));
            toast.pill.GetComponent<Image>().pixelsPerUnitMultiplier = 1.4f;
            toast.text = UIKit.Txt(toast.pill, "Text", "", 38, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            toast.text.rectTransform.Fill(30, 0, 30, 0);
            ts.gameObject.SetActive(false);

            // result
            var rs = UIKit.Node("Result", overlays).Fill();
            result = rs.gameObject.AddComponent<ResultPanel>();
            result.group = UIKit.Group(rs);
            UIKit.Dim(rs, 0.8f);
            result.rays = UIKit.Img(rs, "Rays", UIKit.S("rays"), new Color(1f, 0.85f, 0.3f, 0.22f));
            result.rays.rectTransform.At(Center, new Vector2(0, 420), new Vector2(1200, 1200));
            result.content = UIKit.Node("Content", rs).At(Center, Vector2.zero, new Vector2(1000, 1400));
            var rpanel = UIKit.Img(result.content, "Panel", UIKit.S("panel"), Color.white);
            rpanel.rectTransform.At(Center, new Vector2(0, 40), new Vector2(900, 760));
            result.ribbon = UIKit.Img(result.content, "Ribbon", UIKit.S("ribbon_gold"), Color.white);
            result.ribbon.type = Image.Type.Simple;
            result.ribbon.rectTransform.At(Center, new Vector2(0, 470), new Vector2(760, 190));
            result.title = UIKit.Txt(result.ribbon.rectTransform, "Title", "YOU WON!", 74, Color.white, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic).Shadowed();
            result.title.rectTransform.At(Center, new Vector2(0, 16), new Vector2(560, 110));
            result.myAvatar = ResultAvatar(result.content, new Vector2(-220, 170), out result.myName, out result.myCrown);
            result.oppAvatar = ResultAvatar(result.content, new Vector2(220, 170), out result.oppName, out result.oppCrown);
            var vs = UIKit.Img(result.content, "VS", UIKit.S("vs"), Color.white);
            vs.rectTransform.At(Center, new Vector2(0, 170), new Vector2(170, 120));
            result.pointsText = UIKit.Txt(result.content, "Points", "+120 points", 50, Palette.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            result.pointsText.rectTransform.At(Center, new Vector2(0, -110), new Vector2(840, 120));
            result.statsText = UIKit.Txt(result.content, "Stats", "", 34, new Color(1, 1, 1, 0.75f));
            result.statsText.rectTransform.At(Center, new Vector2(0, -235), new Vector2(840, 60));
            result.playAgain = UIKit.Btn(result.content, "PlayAgain", UIKit.S("btn_blue"), new Vector2(640, 150), "PLAY AGAIN", 58);
            ((RectTransform)result.playAgain.transform).At(Center, new Vector2(0, -470), new Vector2(640, 150));
            var again = UIKit.Img(result.playAgain.transform, "Icon", UIKit.S("icon_replay"), Color.white);
            again.rectTransform.At(new Vector2(0, 0.5f), new Vector2(86, 8), new Vector2(60, 60));
            result.playAgain.transform.Find("Label").GetComponent<RectTransform>().Fill(80, 14, 10, 4);
            result.home = UIKit.Btn(result.content, "Home", UIKit.S("btn_gray"), new Vector2(400, 124), "HOME", 48);
            ((RectTransform)result.home.transform).At(Center, new Vector2(0, -640), new Vector2(400, 124));
            result.confettiLayer = UIKit.Node("Confetti", rs).Fill();
            rs.gameObject.SetActive(false);

            // UNO Dare: dare-or-draw challenges
            var dp = UIKit.Node("Dare", overlays);
            dare = dp.gameObject.AddComponent<DarePanel>();
            dare.Build();
            dp.gameObject.SetActive(false);

            // dialog
            var dg = UIKit.Node("Dialog", overlays).Fill();
            dialog = dg.gameObject.AddComponent<ConfirmDialog>();
            dialog.group = UIKit.Group(dg);
            UIKit.Dim(dg, 0.65f);
            dialog.panel = UIKit.Img(dg, "Panel", UIKit.S("panel"), Color.white, true).rectTransform;
            dialog.panel.At(Center, new Vector2(0, 80), new Vector2(820, 520));
            dialog.title = UIKit.Txt(dialog.panel, "Title", "Leave match?", 56, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            dialog.title.rectTransform.At(Center, new Vector2(0, 160), new Vector2(740, 80));
            dialog.body = UIKit.Txt(dialog.panel, "Body", "", 40, new Color(1, 1, 1, 0.8f));
            dialog.body.textWrappingMode = TextWrappingModes.Normal;
            dialog.body.rectTransform.At(Center, new Vector2(0, 30), new Vector2(700, 150));
            dialog.cancel = UIKit.Btn(dialog.panel, "Cancel", UIKit.S("btn_gray"), new Vector2(310, 124), "STAY", 46);
            ((RectTransform)dialog.cancel.transform).At(Center, new Vector2(-170, -150), new Vector2(310, 124));
            dialog.cancelLabel = dialog.cancel.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            dialog.ok = UIKit.Btn(dialog.panel, "Ok", UIKit.S("btn_red"), new Vector2(310, 124), "LEAVE", 46);
            ((RectTransform)dialog.ok.transform).At(Center, new Vector2(170, -150), new Vector2(310, 124));
            dialog.okLabel = dialog.ok.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            dg.gameObject.SetActive(false);
        }

        static Button MenuRow(RectTransform panel, string name, string icon, string label, float y, out Image iconImg, out TextMeshProUGUI text)
        {
            var b = UIKit.Btn(panel, name, UIKit.S("rounded"), new Vector2(390, 96));
            var img = b.GetComponent<Image>();
            img.color = new Color(1, 1, 1, 0.08f);
            img.pixelsPerUnitMultiplier = 2.2f;
            ((RectTransform)b.transform).At(new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(390, 96));
            iconImg = UIKit.Img(b.transform, "Icon", UIKit.S(icon), Color.white);
            iconImg.rectTransform.At(new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(54, 54));
            text = UIKit.Txt(b.transform, "Label", label, 38, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            text.rectTransform.Fill(96, 0, 10, 0);
            return b;
        }

        static Image ResultAvatar(RectTransform parent, Vector2 pos, out TextMeshProUGUI name, out Image crown)
        {
            var ring = UIKit.Img(parent, "AvatarRing", UIKit.S("circle"), Color.white);
            ring.rectTransform.At(Center, pos, new Vector2(212, 212));
            var av = UIKit.Img(parent, "Avatar", ArtLibrary.I.avatars[0], Color.white);
            av.rectTransform.At(Center, pos, new Vector2(200, 200));
            crown = UIKit.Img(parent, "Crown", UIKit.S("crown"), Color.white);
            crown.rectTransform.At(Center, pos + new Vector2(0, 140), new Vector2(120, 96));
            crown.rectTransform.localEulerAngles = new Vector3(0, 0, pos.x < 0 ? 12f : -12f);
            name = UIKit.Txt(parent, "Name", "Player", 38, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            name.rectTransform.At(Center, pos + new Vector2(0, -140), new Vector2(380, 56));
            name.enableAutoSizing = true;
            name.fontSizeMin = 24;
            name.fontSizeMax = 38;
            return av;
        }
    }
}
