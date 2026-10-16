using System;
using TMPro;
using UnoGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.Home
{
    /// <summary>
    /// Rules screen for a game mode, shown after picking a mode and before matchmaking. The row above
    /// PLAY picks the table size (2 to 10 players; 5+ is played in landscape).
    /// </summary>
    public sealed class ModeInfoPanel : MonoBehaviour
    {
        public CanvasGroup group;
        public RectTransform panel;
        public Image accentBar;
        public Image[] artCards = new Image[3];
        public TextMeshProUGUI title;
        public TextMeshProUGUI tagline;
        public ScrollRect scroll;
        public RectTransform content;
        public Button play;
        public Button close;
        public Button[] sizeButtons = new Button[SizeCount];
        public TextMeshProUGUI sizeLabel;

        const int SizeMin = 2, SizeCount = 9;

        const float ContentWidth = 820f;
        ModeInfo mode;
        Action<ModeInfo> onPlay;

        public bool IsOpen => gameObject.activeSelf;
        public ModeInfo Mode => mode;

        void Awake()
        {
            play.onClick.AddListener(PlayNow);
            close.onClick.AddListener(Hide);
            for (int i = 0; i < sizeButtons.Length; i++)
            {
                int n = SizeMin + i;
                sizeButtons[i].onClick.AddListener(() => PickSize(n));
            }
        }

        public void PickSize(int n)
        {
            App.I.SelectedSize = n;
            RefreshSize(true);
        }

        void RefreshSize(bool animate)
        {
            int size = App.I.SelectedSize;
            for (int i = 0; i < sizeButtons.Length; i++)
            {
                bool on = SizeMin + i == size;
                var img = sizeButtons[i].GetComponent<Image>();
                img.color = on ? Palette.Gold : new Color(1, 1, 1, 0.1f);
                var label = sizeButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                label.color = on ? Palette.Ink : new Color(1, 1, 1, 0.85f);
                if (on && animate) Tw.Punch(sizeButtons[i].transform, 0.18f, 0.3f);
            }
            sizeLabel.text = size == 2 ? "PLAYERS  <color=#FFFFFFAA>•  1 vs 1</color>"
                : App.IsLandscapeTable(size) ? "PLAYERS  <color=#FFFFFFAA>•  " + size + " at the table, played in landscape</color>"
                : "PLAYERS  <color=#FFFFFFAA>•  " + size + " at the table</color>";
        }

        public void Show(ModeInfo m, Action<ModeInfo> playAction)
        {
            mode = m;
            onPlay = playAction;
            gameObject.SetActive(true);
            title.text = m.Title.ToUpper();
            title.color = Color.Lerp(m.Accent, Color.white, 0.25f);
            tagline.text = m.Tagline;
            accentBar.color = m.Accent;
            for (int i = 0; i < artCards.Length; i++)
            {
                artCards[i].sprite = ArtLibrary.I.Card(m.Art[i % m.Art.Length]);
                artCards[i].transform.localScale = Vector3.zero;
                Tw.Scale(artCards[i].transform, 1f, 0.45f, Ease.OutBackStrong, 0.12f + i * 0.08f);
            }
            Populate();
            RefreshSize(false);
            scroll.verticalNormalizedPosition = 1f;
            group.alpha = 0f;
            group.blocksRaycasts = true;
            Tw.Alpha(group, 1f, 0.2f);
            panel.anchoredPosition = new Vector2(0, -160);
            Tw.Pos(panel, Vector2.zero, 0.4f, Ease.OutBack);
            play.transform.localScale = Vector3.zero;
            Tw.Scale(play.transform, 1f, 0.45f, Ease.OutBackStrong, 0.35f);
        }

        void Populate()
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            float y = 0;
            y = Header("HOW IT WORKS", y);
            foreach (var rule in mode.Rules)
            {
                var row = UIKit.Node("Rule", content);
                row.anchorMin = new Vector2(0, 1);
                row.anchorMax = new Vector2(1, 1);
                row.pivot = new Vector2(0.5f, 1);
                var dot = UIKit.Img(row, "Dot", UIKit.S("circle"), mode.Accent);
                dot.rectTransform.At(new Vector2(0, 1), new Vector2(24, -24), new Vector2(18, 18));
                var t = UIKit.Txt(row, "Text", rule, 34, new Color(1, 1, 1, 0.9f), TextAlignmentOptions.TopLeft);
                t.textWrappingMode = TextWrappingModes.Normal;
                float h = t.GetPreferredValues(rule, ContentWidth - 60, 0).y;
                t.rectTransform.At(new Vector2(0, 1), new Vector2(54, 0), new Vector2(ContentWidth - 60, h), new Vector2(0, 1));
                row.anchoredPosition = new Vector2(0, -y);
                row.sizeDelta = new Vector2(0, h);
                y += h + 22;
            }
            y += 18;
            y = Header("SPECIAL CARDS", y);
            foreach (var s in mode.Specials)
            {
                var row = UIKit.Img(content, "Card", UIKit.S("rounded"), new Color(1, 1, 1, 0.06f)).rectTransform;
                row.GetComponent<Image>().pixelsPerUnitMultiplier = 2.4f;
                row.anchorMin = new Vector2(0, 1);
                row.anchorMax = new Vector2(1, 1);
                row.pivot = new Vector2(0.5f, 1);
                row.anchoredPosition = new Vector2(0, -y);
                row.sizeDelta = new Vector2(0, 156);
                var card = UIKit.Img(row, "Art", ArtLibrary.I.Card(s.sprite), Color.white);
                card.type = Image.Type.Simple;
                card.rectTransform.At(new Vector2(0, 0.5f), new Vector2(66, 0), new Vector2(96, 134));
                var name = UIKit.Txt(row, "Name", s.name, 36, Color.Lerp(mode.Accent, Color.white, 0.35f), TextAlignmentOptions.Left, FontStyles.Bold);
                name.rectTransform.At(new Vector2(0, 0.5f), new Vector2(136, 34), new Vector2(ContentWidth - 160, 50), new Vector2(0, 0.5f));
                var desc = UIKit.Txt(row, "Desc", s.text, 29, new Color(1, 1, 1, 0.78f), TextAlignmentOptions.TopLeft);
                desc.textWrappingMode = TextWrappingModes.Normal;
                desc.rectTransform.At(new Vector2(0, 0.5f), new Vector2(136, 6), new Vector2(ContentWidth - 170, 80), new Vector2(0, 1));
                y += 170;
            }
            content.sizeDelta = new Vector2(0, y + 20);
        }

        float Header(string text, float y)
        {
            var t = UIKit.Txt(content, "Header", text, 32, Palette.Gold, TextAlignmentOptions.Left, FontStyles.Bold);
            t.characterSpacing = 8;
            t.rectTransform.anchorMin = new Vector2(0, 1);
            t.rectTransform.anchorMax = new Vector2(1, 1);
            t.rectTransform.pivot = new Vector2(0.5f, 1);
            t.rectTransform.anchoredPosition = new Vector2(0, -y);
            t.rectTransform.sizeDelta = new Vector2(0, 50);
            return y + 66;
        }

        public void PlayNow()
        {
            if (!IsOpen || mode == null) return;
            var m = mode;
            Hide();
            onPlay?.Invoke(m);
        }

        public void Hide()
        {
            if (!IsOpen) return;
            group.blocksRaycasts = false;
            Tw.Alpha(group, 0f, 0.18f).OnComplete(() => gameObject.SetActive(false));
        }

        // ------------------------------------------------------------ layout (editor builder)

        public void Build()
        {
            var c = new Vector2(0.5f, 0.5f);
            var root = (RectTransform)transform;
            root.Fill();
            group = UIKit.Group(root);
            UIKit.Dim(root, 0.75f);
            var safe = UIKit.Node("Safe", root).Fill();
            safe.gameObject.AddComponent<SafeArea>();
            panel = UIKit.Img(safe, "Panel", UIKit.S("panel"), Color.white, true).rectTransform;
            panel.At(c, Vector2.zero, new Vector2(960, 1680));
            accentBar = UIKit.Img(panel, "Accent", UIKit.S("rounded_small"), Palette.Red);
            accentBar.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(880, 10));
            accentBar.pixelsPerUnitMultiplier = 3f;

            var art = UIKit.Node("Art", panel).At(new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(400, 220));
            var glow = UIKit.Img(art, "Glow", UIKit.S("glow"), new Color(1f, 0.85f, 0.4f, 0.35f));
            glow.rectTransform.At(c, Vector2.zero, new Vector2(520, 360));
            float[] rot = { 14f, 0f, -14f };
            for (int i = 0; i < 3; i++)
            {
                var img = UIKit.Img(art, "Card" + i, ArtLibrary.I.cardBack, Color.white);
                img.type = Image.Type.Simple;
                img.rectTransform.At(c, new Vector2((i - 1) * 96, i == 1 ? 10 : -6), new Vector2(128, 179));
                img.rectTransform.localEulerAngles = new Vector3(0, 0, rot[i]);
                artCards[i] = img;
            }
            title = UIKit.Txt(panel, "Title", "CLASSIC UNO", 62, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).Shadowed();
            title.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -340), new Vector2(880, 84));
            title.enableAutoSizing = true;
            title.fontSizeMin = 36;
            title.fontSizeMax = 62;
            tagline = UIKit.Txt(panel, "Tagline", "", 34, new Color(1, 1, 1, 0.72f));
            tagline.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -402), new Vector2(880, 50));

            var view = UIKit.Node("Scroll", panel).At(new Vector2(0.5f, 1), new Vector2(0, -440), new Vector2(ContentWidth, 830), new Vector2(0.5f, 1));
            var viewBg = view.gameObject.AddComponent<Image>();
            viewBg.color = new Color(0, 0, 0, 0.001f);
            view.gameObject.AddComponent<RectMask2D>();
            content = UIKit.Node("Content", view);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, 1000);
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            // table size: 2 to 10 players
            sizeLabel = UIKit.Txt(panel, "SizeLabel", "PLAYERS", 30, Palette.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            sizeLabel.characterSpacing = 4;
            sizeLabel.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0, 372), new Vector2(880, 44));
            sizeLabel.enableAutoSizing = true;
            sizeLabel.fontSizeMin = 22;
            sizeLabel.fontSizeMax = 30;
            for (int i = 0; i < SizeCount; i++)
            {
                var b = UIKit.Btn(panel, "Size" + (SizeMin + i), UIKit.S("circle"), new Vector2(84, 84), (SizeMin + i).ToString(), 40);
                b.GetComponent<Image>().type = Image.Type.Simple;
                ((RectTransform)b.transform).At(new Vector2(0.5f, 0), new Vector2((i - (SizeCount - 1) / 2f) * 96f, 288), new Vector2(84, 84));
                sizeButtons[i] = b;
            }

            play = UIKit.Btn(panel, "Play", UIKit.S("btn_green"), new Vector2(560, 156), "PLAY", 76);
            ((RectTransform)play.transform).At(new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(560, 156));
            var label = play.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            label.Shadowed();
            label.rectTransform.Fill(60, 20, 10, 4);
            var icon = UIKit.Img(play.transform, "Icon", UIKit.S("icon_play"), Color.white);
            icon.rectTransform.At(new Vector2(0, 0.5f), new Vector2(116, 8), new Vector2(68, 68));
            close = UIKit.IconBtn(panel, "Close", "icon_close", 92, new Color(1, 1, 1, 0.12f));
            ((RectTransform)close.transform).At(new Vector2(1, 1), new Vector2(-70, -70), new Vector2(92, 92));
        }
    }
}
