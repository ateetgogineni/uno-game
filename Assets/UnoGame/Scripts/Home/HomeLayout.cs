using TMPro;
using UnoGame.Game;
using UnoGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.Home
{
    /// <summary>Builds the home screen hierarchy (called by the editor scene builder).</summary>
    public static class HomeLayout
    {
        public static HomeScreen Build(RectTransform canvasRoot, RuntimeAnimatorController matchmakingAnim)
        {
            var bg = UIKit.Img(canvasRoot, "Background", ArtLibrary.I.bgHome, Color.white);
            bg.rectTransform.Fill();
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1080f / 1920f;

            var floatLayer = UIKit.Node("FloatingCards", canvasRoot).Fill();

            var safe = UIKit.Node("SafeArea", canvasRoot).Fill();
            safe.gameObject.AddComponent<SafeArea>();
            var home = safe.gameObject.AddComponent<HomeScreen>();
            home.floatLayer = floatLayer;

            // top bar: profile chip (left), wins + sound (right)
            home.topBar = UIKit.Node("TopBar", safe).HStrip(1f, -90, 140);
            home.profileButton = UIKit.Btn(home.topBar, "Profile", UIKit.S("rounded"), new Vector2(470, 112));
            var chip = (RectTransform)home.profileButton.transform;
            chip.At(new Vector2(0, 0.5f), new Vector2(36, 0), new Vector2(470, 112), new Vector2(0, 0.5f));
            var chipImg = home.profileButton.GetComponent<Image>();
            chipImg.color = new Color(0, 0, 0, 0.35f);
            chipImg.pixelsPerUnitMultiplier = 1.15f;
            var ring = UIKit.Img(chip, "Ring", UIKit.S("circle"), Color.white);
            ring.rectTransform.At(new Vector2(0, 0.5f), new Vector2(58, 0), new Vector2(100, 100));
            home.profileAvatar = UIKit.Img(chip, "Avatar", ArtLibrary.I.avatars[0], Color.white);
            home.profileAvatar.rectTransform.At(new Vector2(0, 0.5f), new Vector2(58, 0), new Vector2(92, 92));
            home.profileName = UIKit.Txt(chip, "Name", "Player", 40, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            home.profileName.rectTransform.At(new Vector2(0, 0.5f), new Vector2(124, 0), new Vector2(270, 60), new Vector2(0, 0.5f));
            home.profileName.enableAutoSizing = true;
            home.profileName.fontSizeMin = 26;
            home.profileName.fontSizeMax = 40;
            var editIcon = UIKit.Img(chip, "Edit", UIKit.S("icon_edit"), new Color(1, 1, 1, 0.6f));
            editIcon.rectTransform.At(new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(40, 40));

            var wins = UIKit.Img(home.topBar, "Wins", UIKit.S("rounded"), new Color(0, 0, 0, 0.35f));
            wins.rectTransform.At(new Vector2(1, 0.5f), new Vector2(-160, 0), new Vector2(200, 96), new Vector2(1, 0.5f));
            wins.pixelsPerUnitMultiplier = 1.35f;
            var trophy = UIKit.Img(wins.rectTransform, "Trophy", UIKit.S("icon_trophy"), Palette.Gold);
            trophy.rectTransform.At(new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(54, 54));
            home.winsText = UIKit.Txt(wins.rectTransform, "Count", "0", 44, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            home.winsText.rectTransform.Fill(80, 0, 14, 0);

            home.soundButton = UIKit.IconBtn(home.topBar, "Sound", "icon_sound_on", 96, new Color(0, 0, 0, 0.35f));
            ((RectTransform)home.soundButton.transform).At(new Vector2(1, 0.5f), new Vector2(-84, 0), new Vector2(96, 96));
            home.soundIcon = home.soundButton.transform.Find("Icon").GetComponent<Image>();

            // logo
            var logo = UIKit.Img(safe, "Logo", ArtLibrary.I.logo, Color.white);
            logo.type = Image.Type.Simple;
            home.logo = logo.rectTransform;
            home.logo.At(new Vector2(0.5f, 0.5f), new Vector2(0, 360), new Vector2(940, 526));
            var floater = logo.gameObject.AddComponent<Floater>();
            floater.amplitude = 14f;
            floater.speed = 1.6f;
            floater.rotate = 1.2f;
            floater.enabled = false;

            // play button
            home.playRoot = UIKit.Node("PlayRoot", safe).At(new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(600, 190));
            var pulse = home.playRoot.gameObject.AddComponent<Pulse>();
            pulse.amount = 0.035f;
            pulse.speed = 3.2f;
            pulse.enabled = false;
            home.playButton = UIKit.Btn(home.playRoot, "PlayButton", UIKit.S("btn_green"), new Vector2(600, 190), "PLAY", 96);
            ((RectTransform)home.playButton.transform).At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 190));
            var playLabel = home.playButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            playLabel.Shadowed();
            playLabel.rectTransform.Fill(70, 22, 10, 4);
            playLabel.characterSpacing = 6;
            var playIcon = UIKit.Img(home.playButton.transform, "Icon", UIKit.S("icon_play"), Color.white);
            playIcon.rectTransform.At(new Vector2(0, 0.5f), new Vector2(120, 8), new Vector2(84, 84));

            // online status badge: live dot + icon + bold caption
            home.onlinePill = UIKit.Chip(safe, "OnlineBadge", 84);
            home.onlinePill.At(new Vector2(0.5f, 0.5f), new Vector2(0, -318), new Vector2(400, 84));
            var dotRoot = UIKit.Node("DotRoot", home.onlinePill);
            dotRoot.sizeDelta = new Vector2(30, 30);
            var dotLe = dotRoot.gameObject.AddComponent<LayoutElement>();
            dotLe.preferredWidth = 30;
            dotLe.preferredHeight = 30;
            var halo = UIKit.Img(dotRoot, "Halo", UIKit.S("circle"), new Color(Palette.Online.r, Palette.Online.g, Palette.Online.b, 0.35f));
            halo.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            var haloPulse = halo.gameObject.AddComponent<Pulse>();
            haloPulse.amount = 0.35f;
            haloPulse.speed = 4f;
            home.onlineHalo = halo;
            home.onlineDot = UIKit.Img(dotRoot, "Dot", UIKit.S("circle"), Palette.Online);
            home.onlineDot.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18));
            home.onlineIcon = UIKit.LayoutIcon(home.onlinePill, "Icon", UIKit.S("icon_users"), 44, new Color(1, 1, 1, 0.9f));
            home.onlineText = UIKit.Txt(home.onlinePill, "Text", "CONNECTING", 34, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            home.onlineText.characterSpacing = 4;
            home.onlineText.rectTransform.sizeDelta = new Vector2(300, 84);

            // how to play: three illustrated template cards
            home.howHeader = UIKit.Node("HowHeader", safe).At(new Vector2(0.5f, 0.5f), new Vector2(0, -455), new Vector2(900, 50));
            var head = UIKit.Txt(home.howHeader, "Text", "HOW TO PLAY", 32, Palette.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            head.characterSpacing = 10;
            head.rectTransform.Fill();
            for (int side = -1; side <= 1; side += 2)
            {
                var line = UIKit.Img(home.howHeader, "Line", UIKit.S("rounded_small"), new Color(1f, 0.76f, 0.1f, 0.45f));
                line.type = Image.Type.Simple;
                line.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(side * 270, 0), new Vector2(200, 4));
            }
            home.tipCards = new RectTransform[3];
            home.tipCards[0] = TipCard(safe, -340, "Match a\ncolor or number", tc =>
            {
                MiniCard(tc, "card_red_7", new Vector2(-34, 34), -10f);
                MiniCard(tc, "card_blue_7", new Vector2(34, 30), 10f);
            });
            home.tipCards[1] = TipCard(safe, 0, "Stack <color=#FF6B5E>+2</color> and\n<color=#FFD447>+4</color> on them", tc =>
            {
                MiniCard(tc, "card_blue_12", new Vector2(-34, 34), -10f);
                MiniCard(tc, "card_wild4", new Vector2(34, 30), 10f);
            });
            home.tipCards[2] = TipCard(safe, 340, "Call UNO!\nat 2 cards", tc =>
            {
                var burst = UIKit.Img(tc, "Burst", UIKit.S("uno_burst"), Color.white);
                burst.type = Image.Type.Simple;
                burst.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 34), new Vector2(200, 123));
            });

            // version
            home.bottomInfo = UIKit.Node("Bottom", safe).HStrip(0f, 46, 50);
            var version = UIKit.Txt(home.bottomInfo, "Version", "v" + Application.version, 26, new Color(1, 1, 1, 0.35f));
            version.rectTransform.Fill();

            // overlays (full screen, above everything)
            var overlays = UIKit.Node("Overlays", canvasRoot).Fill();
            var pp = UIKit.Node("ProfilePanel", overlays);
            home.profilePanel = pp.gameObject.AddComponent<ProfilePanel>();
            home.profilePanel.Build();
            pp.gameObject.SetActive(false);

            var mm = UIKit.Node("Matchmaking", overlays);
            home.matchmaking = mm.gameObject.AddComponent<MatchmakingScreen>();
            home.matchmaking.Build();
            home.matchmaking.animator.runtimeAnimatorController = matchmakingAnim;
            mm.gameObject.SetActive(false);

            var qd = UIKit.Node("QuitDialog", overlays).Fill();
            home.quitDialog = qd.gameObject.AddComponent<ConfirmDialog>();
            BuildDialog(home.quitDialog, qd);
            qd.gameObject.SetActive(false);
            return home;
        }

        /// <summary>One "how to play" card: illustration on top, two-line caption below.</summary>
        static RectTransform TipCard(RectTransform parent, float x, string caption, System.Action<RectTransform> art)
        {
            var card = UIKit.Img(parent, "TipCard", UIKit.S("panel"), new Color(1, 1, 1, 0.93f)).rectTransform;
            card.At(new Vector2(0.5f, 0.5f), new Vector2(x, -640), new Vector2(316, 256));
            card.GetComponent<Image>().pixelsPerUnitMultiplier = 1.6f;
            var glow = UIKit.Img(card, "Glow", UIKit.S("glow"), new Color(0.55f, 0.42f, 1f, 0.35f));
            glow.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 34), new Vector2(260, 200));
            var artRoot = UIKit.Node("Art", card).Fill();
            art(artRoot);
            var t = UIKit.Txt(card, "Caption", caption, 29, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            t.lineSpacing = -8;
            t.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0, 52), new Vector2(296, 84));
            return card;
        }

        static void MiniCard(RectTransform parent, string sprite, Vector2 pos, float rot)
        {
            var shadow = UIKit.Img(parent, "Shadow", UIKit.S("card_shadow"), new Color(1, 1, 1, 0.7f));
            shadow.type = Image.Type.Simple;
            shadow.rectTransform.At(new Vector2(0.5f, 0.5f), pos + new Vector2(0, -5), new Vector2(98, 130));
            shadow.rectTransform.localEulerAngles = new Vector3(0, 0, rot);
            Sprite s = sprite.StartsWith("card_") ? CardSprite(sprite) : UIKit.S(sprite);
            var img = UIKit.Img(parent, sprite, s, Color.white);
            img.type = Image.Type.Simple;
            img.rectTransform.At(new Vector2(0.5f, 0.5f), pos, new Vector2(80, 112));
            img.rectTransform.localEulerAngles = new Vector3(0, 0, rot);
        }

        static Sprite CardSprite(string name)
        {
            if (name == "card_wild") return ArtLibrary.I.cardFaces[52];
            if (name == "card_wild4") return ArtLibrary.I.cardFaces[53];
            var parts = name.Split('_');
            int c = System.Array.IndexOf(new[] { "red", "yellow", "green", "blue" }, parts[1]);
            return ArtLibrary.I.cardFaces[c * 13 + int.Parse(parts[2])];
        }

        static void BuildDialog(ConfirmDialog d, RectTransform root)
        {
            d.group = UIKit.Group(root);
            UIKit.Dim(root, 0.65f);
            d.panel = UIKit.Img(root, "Panel", UIKit.S("panel"), Color.white, true).rectTransform;
            d.panel.At(new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(820, 500));
            d.title = UIKit.Txt(d.panel, "Title", "", 56, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            d.title.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(740, 80));
            d.body = UIKit.Txt(d.panel, "Body", "", 40, new Color(1, 1, 1, 0.8f));
            d.body.textWrappingMode = TextWrappingModes.Normal;
            d.body.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 35), new Vector2(700, 130));
            d.cancel = UIKit.Btn(d.panel, "Cancel", UIKit.S("btn_gray"), new Vector2(310, 124), "STAY", 46);
            ((RectTransform)d.cancel.transform).At(new Vector2(0.5f, 0.5f), new Vector2(-170, -140), new Vector2(310, 124));
            d.cancelLabel = d.cancel.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            d.ok = UIKit.Btn(d.panel, "Ok", UIKit.S("btn_red"), new Vector2(310, 124), "QUIT", 46);
            ((RectTransform)d.ok.transform).At(new Vector2(0.5f, 0.5f), new Vector2(170, -140), new Vector2(310, 124));
            d.okLabel = d.ok.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        }
    }
}
