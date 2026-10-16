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
        static readonly Vector2 C = new Vector2(0.5f, 0.5f);
        const float TileW = 512f, TileH = 340f;

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
            home.logo.At(C, new Vector2(0, 625), new Vector2(600, 337));
            var floater = logo.gameObject.AddComponent<Floater>();
            floater.amplitude = 10f;
            floater.speed = 1.6f;
            floater.rotate = 1.2f;
            floater.enabled = false;

            // online status badge: live dot + icon + bold caption
            home.onlinePill = UIKit.Chip(safe, "OnlineBadge", 80);
            home.onlinePill.At(C, new Vector2(0, 395), new Vector2(400, 80));
            var dotRoot = UIKit.Node("DotRoot", home.onlinePill);
            dotRoot.sizeDelta = new Vector2(30, 30);
            var dotLe = dotRoot.gameObject.AddComponent<LayoutElement>();
            dotLe.preferredWidth = 30;
            dotLe.preferredHeight = 30;
            var halo = UIKit.Img(dotRoot, "Halo", UIKit.S("circle"), new Color(Palette.Online.r, Palette.Online.g, Palette.Online.b, 0.35f));
            halo.rectTransform.At(C, Vector2.zero, new Vector2(30, 30));
            var haloPulse = halo.gameObject.AddComponent<Pulse>();
            haloPulse.amount = 0.35f;
            haloPulse.speed = 4f;
            home.onlineHalo = halo;
            home.onlineDot = UIKit.Img(dotRoot, "Dot", UIKit.S("circle"), Palette.Online);
            home.onlineDot.rectTransform.At(C, Vector2.zero, new Vector2(18, 18));
            home.onlineIcon = UIKit.LayoutIcon(home.onlinePill, "Icon", UIKit.S("icon_users"), 42, new Color(1, 1, 1, 0.9f));
            home.onlineText = UIKit.Txt(home.onlinePill, "Text", "CONNECTING", 32, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            home.onlineText.characterSpacing = 4;
            home.onlineText.rectTransform.sizeDelta = new Vector2(300, 80);

            // game modes
            home.modesHeader = UIKit.Node("ModesHeader", safe).At(C, new Vector2(0, 314), new Vector2(940, 50));
            var head = UIKit.Txt(home.modesHeader, "Text", "CHOOSE A GAME MODE", 32, Palette.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            head.characterSpacing = 8;
            head.rectTransform.Fill();
            for (int side = -1; side <= 1; side += 2)
            {
                var line = UIKit.Img(home.modesHeader, "Line", UIKit.S("rounded_small"), new Color(1f, 0.76f, 0.1f, 0.45f));
                line.type = Image.Type.Simple;
                line.rectTransform.At(C, new Vector2(side * 345, 0), new Vector2(150, 4));
            }
            int n = ModeInfo.All.Length;
            home.modeTiles = new RectTransform[n];
            home.modeButtons = new Button[n];
            for (int i = 0; i < n; i++)
            {
                var pos = new Vector2(i % 2 == 0 ? -263 : 263, 110 - (i / 2) * 356);
                home.modeButtons[i] = ModeTile(safe, ModeInfo.All[i], pos);
                home.modeTiles[i] = (RectTransform)home.modeButtons[i].transform;
            }

            // version
            home.bottomInfo = UIKit.Node("Bottom", safe).HStrip(0f, 46, 50);
            var version = UIKit.Txt(home.bottomInfo, "Version", "v" + Application.version, 26, new Color(1, 1, 1, 0.35f));
            version.rectTransform.Fill();

            // overlays (full screen, above everything)
            var overlays = UIKit.Node("Overlays", canvasRoot).Fill();
            var mi = UIKit.Node("ModeInfo", overlays);
            home.modeInfo = mi.gameObject.AddComponent<ModeInfoPanel>();
            home.modeInfo.Build();
            mi.gameObject.SetActive(false);

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

        /// <summary>One game-mode tile: title on top, fanned cards in the middle, description below, accent frame.</summary>
        static Button ModeTile(RectTransform parent, ModeInfo m, Vector2 pos)
        {
            var frame = UIKit.Img(parent, "Mode_" + m.Id, UIKit.S("rounded"), m.Accent, true);
            frame.pixelsPerUnitMultiplier = 1.7f;
            var rt = frame.rectTransform;
            rt.At(C, pos, new Vector2(TileW, TileH));
            var btn = frame.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = frame;
            frame.gameObject.AddComponent<ButtonFx>();

            var fill = UIKit.Img(rt, "Fill", UIKit.S("panel"), new Color(1, 1, 1, 0.96f));
            fill.rectTransform.Fill(5, 5, 5, 5);
            fill.pixelsPerUnitMultiplier = 1.8f;
            var shade = UIKit.Img(rt, "Tint", UIKit.S("rounded"), new Color(m.Accent.r, m.Accent.g, m.Accent.b, 0.16f));
            shade.rectTransform.Fill(5, 5, 5, 5);
            shade.pixelsPerUnitMultiplier = 1.8f;

            // Top to bottom: title (centred), the mode's cards, description. The "i" marks that it opens the rules.
            var title = UIKit.Txt(rt, "Title", m.Title, 42, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).Shadowed();
            title.enableAutoSizing = true;
            title.fontSizeMin = 24;
            title.fontSizeMax = m.Title.Length > 12 ? 34 : 42;
            title.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(TileW - 88, 58), new Vector2(0.5f, 1));

            var art = UIKit.Node("Art", rt).At(C, Vector2.zero, new Vector2(320, 200));
            var glow = UIKit.Img(art, "Glow", UIKit.S("glow"), new Color(m.Accent.r, m.Accent.g, m.Accent.b, 0.55f));
            glow.rectTransform.At(C, new Vector2(0, -6), new Vector2(380, 280));
            float[] rot = { 14f, 0f, -14f };
            for (int i = 0; i < 3; i++)
            {
                var shadow = UIKit.Img(art, "Shadow" + i, UIKit.S("card_shadow"), new Color(1, 1, 1, 0.75f));
                shadow.type = Image.Type.Simple;
                var at = new Vector2((i - 1) * 74, i == 1 ? 6 : -6);
                shadow.rectTransform.At(C, at + new Vector2(0, -6), new Vector2(128, 170));
                shadow.rectTransform.localEulerAngles = new Vector3(0, 0, rot[i]);
                var card = UIKit.Img(art, "Card" + i, ArtLibrary.I.Card(m.Art[i]), Color.white);
                card.type = Image.Type.Simple;
                card.rectTransform.At(C, at, new Vector2(104, 146));
                card.rectTransform.localEulerAngles = new Vector3(0, 0, rot[i]);
            }

            var tag = UIKit.Txt(rt, "Description", m.Tagline, 25, new Color(1, 1, 1, 0.82f), TextAlignmentOptions.Center, FontStyles.Bold);
            tag.textWrappingMode = TextWrappingModes.Normal;
            tag.enableAutoSizing = true;
            tag.fontSizeMin = 18;
            tag.fontSizeMax = 25;
            tag.lineSpacing = -6;
            tag.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(TileW - 44, 64), new Vector2(0.5f, 0));

            var info = UIKit.Img(rt, "Info", UIKit.S("icon_info"), new Color(1, 1, 1, 0.6f));
            info.rectTransform.At(new Vector2(1, 1), new Vector2(-16, -16), new Vector2(34, 34), new Vector2(1, 1));
            return btn;
        }

        static void BuildDialog(ConfirmDialog d, RectTransform root)
        {
            d.group = UIKit.Group(root);
            UIKit.Dim(root, 0.65f);
            d.panel = UIKit.Img(root, "Panel", UIKit.S("panel"), Color.white, true).rectTransform;
            d.panel.At(C, new Vector2(0, 80), new Vector2(820, 500));
            d.title = UIKit.Txt(d.panel, "Title", "", 56, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            d.title.rectTransform.At(C, new Vector2(0, 150), new Vector2(740, 80));
            d.body = UIKit.Txt(d.panel, "Body", "", 40, new Color(1, 1, 1, 0.8f));
            d.body.textWrappingMode = TextWrappingModes.Normal;
            d.body.rectTransform.At(C, new Vector2(0, 35), new Vector2(700, 130));
            d.cancel = UIKit.Btn(d.panel, "Cancel", UIKit.S("btn_gray"), new Vector2(310, 124), "STAY", 46);
            ((RectTransform)d.cancel.transform).At(C, new Vector2(-170, -140), new Vector2(310, 124));
            d.cancelLabel = d.cancel.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            d.ok = UIKit.Btn(d.panel, "Ok", UIKit.S("btn_red"), new Vector2(310, 124), "QUIT", 46);
            ((RectTransform)d.ok.transform).At(C, new Vector2(170, -140), new Vector2(310, 124));
            d.okLabel = d.ok.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        }
    }
}
