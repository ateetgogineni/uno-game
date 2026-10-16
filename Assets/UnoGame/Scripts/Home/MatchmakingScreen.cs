using System.Collections;
using TMPro;
using UnoGame.Net;
using UnoGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.Home
{
    /// <summary>
    /// Matchmaking overlay. The searching radar and the "found" VS reveal are Animator clips
    /// (Assets/UnoGame/Animations/Matchmaking.controller, states Search -> Found on trigger "found").
    /// The game scene loads only after the reveal has played.
    /// </summary>
    public sealed class MatchmakingScreen : MonoBehaviour
    {
        public CanvasGroup group;
        public Animator animator;
        public Image myAvatar;
        public TextMeshProUGUI myName;
        public Image oppAvatar;
        public TextMeshProUGUI oppName;
        public TextMeshProUGUI status;
        public TextMeshProUGUI timer;
        public TextMeshProUGUI tip;
        public TextMeshProUGUI mode;
        public RectTransform modeChip;
        public TextMeshProUGUI modeTitle;
        public TextMeshProUGUI header;
        public RectTransform tableRow;   // the rest of the table (3+ players), under the VS
        public Button cancel;

        static readonly string[] Tips =
        {
            "Tip: stack a +2 on a +2 to pass the penalty on!",
            "Tip: tap UNO! before playing your second-last card.",
            "Tip: Wild Draw 4 only works when you have no card of the current colour.",
            "Tip: Skip and Reverse give you another turn in a 1v1 game.",
            "Tip: at a big table, watch the player who plays after you.",
            "Tip: save your Wild cards for when you get stuck.",
            "Tip: drew a card you can play? Play it right away or pass.",
        };

        float openedAt;
        float nextTip;
        int tipIndex;
        bool found;
        bool open;

        public bool IsOpen => open;
        public bool Found => found;

        void Awake()
        {
            // The overlay is saved inactive; Awake runs the first time Open() activates it.
            cancel.onClick.AddListener(Cancel);
        }

        public void Open()
        {
            if (open) return;
            open = true;
            found = false;
            gameObject.SetActive(true);
            var p = App.I.Profile;
            myAvatar.sprite = ArtLibrary.I.Avatar(p.avatar);
            myName.text = p.name;
            oppName.text = "";
            group.alpha = 0f;
            group.blocksRaycasts = true;
            Tw.Alpha(group, 1f, 0.25f);
            cancel.gameObject.SetActive(true);
            cancel.interactable = true;
            animator.Rebind();
            animator.Play("Search", 0, 0f);
            openedAt = Time.unscaledTime;
            tipIndex = Random.Range(0, Tips.Length);
            tip.text = Tips[tipIndex];
            nextTip = Time.unscaledTime + 4f;
            status.text = "FINDING PLAYERS...";
            modeChip.gameObject.SetActive(false);
            for (int i = tableRow.childCount - 1; i >= 0; i--) Destroy(tableRow.GetChild(i).gameObject);
            App.I.Match.Matched += OnMatched;
            int size = App.I.SelectedSize;
            modeTitle.text = ModeInfo.Get(App.I.SelectedMode).Title.ToUpper();
            header.text = size == 2 ? "MATCHMAKING" : "MATCHMAKING  •  " + size + " PLAYERS";
            status.text = size == 2 ? "FINDING PLAYERS..." : "FINDING " + (size - 1) + " PLAYERS...";
            App.I.Match.StartSearch(App.I.SelectedMode, size);
            Debug.Log("[Home] matchmaking started");
        }

        public void Cancel()
        {
            if (!open || found) return;
            App.I.Match.CancelSearch();
            Close();
        }

        void Close()
        {
            open = false;
            App.I.Match.Matched -= OnMatched;
            group.blocksRaycasts = false;
            Tw.Alpha(group, 0f, 0.2f).OnComplete(() => gameObject.SetActive(false));
        }

        void OnDestroy()
        {
            if (App.I != null) App.I.Match.Matched -= OnMatched;
        }

        void OnMatched(GameSession s)
        {
            if (!open || found) return;
            found = true;
            App.I.Match.Matched -= OnMatched;
            StartCoroutine(Reveal(s));
        }

        IEnumerator Reveal(GameSession s)
        {
            cancel.interactable = false;
            oppAvatar.sprite = ArtLibrary.I.Avatar(s.Opponent.avatar);
            oppName.text = s.Opponent.name;
            myName.text = s.Me.name;
            status.text = s.Size > 2 ? "PLAYERS FOUND!" : "PLAYER FOUND!";
            ShowTable(s);
            Tw.Punch(status.transform, 0.18f, 0.4f);
            if (!s.IsOnline)
            {
                modeChip.gameObject.SetActive(true);
                modeChip.localScale = Vector3.zero;
                Tw.Scale(modeChip, 1f, 0.4f, Ease.OutBack, 0.2f);
            }
            animator.SetTrigger("found");
            App.Play(Sound.Found);
            yield return Tw.Wait(0.45f);
            App.Play(Sound.Whoosh, 0.8f);
            yield return Tw.Wait(0.2f);
            App.Play(Sound.Plus, 0.7f, 1.4f);
            yield return Tw.Wait(1.6f);
            App.I.LoadScene(App.GameScene);
        }

        /// <summary>Everyone else at a 3+ table pops in as a row of small avatars under the VS.</summary>
        void ShowTable(GameSession s)
        {
            int others = s.Size - 2;
            if (others <= 0) return;
            float step = Mathf.Min(120f, 960f / others);
            float size = Mathf.Min(96f, step - 12f);
            int k = 0;
            for (int i = 0; i < s.Size; i++)
            {
                if (i == s.Seat || i == s.OpponentSeat) continue;
                var c = new Vector2(0.5f, 0.5f);
                var cell = UIKit.Node("Player" + i, tableRow).At(c, new Vector2((k - (others - 1) / 2f) * step, 0), new Vector2(step, 150));
                var ring = UIKit.Img(cell, "Ring", UIKit.S("circle"), Color.white);
                ring.rectTransform.At(c, new Vector2(0, 22), new Vector2(size + 8, size + 8));
                var av = UIKit.Img(cell, "Avatar", ArtLibrary.I.Avatar(s.Players[i].avatar), Color.white);
                av.rectTransform.At(c, new Vector2(0, 22), new Vector2(size, size));
                var name = UIKit.Txt(cell, "Name", s.Players[i].name, 24, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).Shadowed();
                name.rectTransform.At(c, new Vector2(0, -50), new Vector2(step - 4, 34));
                name.enableAutoSizing = true;
                name.fontSizeMin = 14;
                name.fontSizeMax = 24;
                cell.localScale = Vector3.zero;
                Tw.Scale(cell, 1f, 0.4f, Ease.OutBackStrong, 0.5f + k * 0.08f);
                k++;
            }
        }

        void Update()
        {
            if (!open) return;
            if (!found)
            {
                float t = Time.unscaledTime - openedAt;
                int secs = Mathf.FloorToInt(t);
                timer.text = $"{secs / 60}:{secs % 60:00}";
                if (Time.unscaledTime >= nextTip)
                {
                    nextTip = Time.unscaledTime + 4f;
                    tipIndex = (tipIndex + 1) % Tips.Length;
                    tip.text = Tips[tipIndex];
                    tip.alpha = 0f;
                    Tw.Alpha(tip, 1f, 0.4f);
                }
            }
            if (Input.GetKeyDown(KeyCode.Escape)) Cancel();
        }

        // ------------------------------------------------------------ layout (editor builder)

        public void Build()
        {
            var root = (RectTransform)transform;
            root.Fill();
            group = UIKit.Group(root);
            animator = gameObject.AddComponent<Animator>();
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;

            var bg = UIKit.Img(root, "Background", ArtLibrary.I.bgHome, Color.white, true);
            bg.rectTransform.Fill();
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1080f / 1920f;
            var shade = UIKit.Img(root, "Shade", UIKit.S("rounded_small"), new Color(0.03f, 0.02f, 0.08f, 0.55f));
            shade.type = Image.Type.Simple;
            shade.rectTransform.Fill(-50, -50, -50, -50);

            var safe = UIKit.Node("Safe", root).Fill();
            safe.gameObject.AddComponent<SafeArea>();

            header = UIKit.Txt(safe, "Title", "MATCHMAKING", 34, new Color(1, 1, 1, 0.6f), TextAlignmentOptions.Center, FontStyles.Bold);
            header.characterSpacing = 12;
            header.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(900, 50));
            modeTitle = UIKit.Txt(safe, "Mode", "CLASSIC UNO", 54, Palette.Gold, TextAlignmentOptions.Center, FontStyles.Bold).Shadowed();
            modeTitle.characterSpacing = 4;
            modeTitle.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(940, 76));
            modeTitle.enableAutoSizing = true;
            modeTitle.fontSizeMin = 32;
            modeTitle.fontSizeMax = 54;

            var center = UIKit.Node("Center", safe).At(new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(800, 800));
            for (int i = 1; i <= 2; i++)
            {
                var ring = UIKit.Img(center, "Ring" + i, UIKit.S("ring"), new Color(1, 1, 1, 0.6f));
                ring.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330, 330));
            }
            var orbit = UIKit.Node("Orbit", center).At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 600));
            UIKit.Group(orbit, 1f, false);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f;
                var c = UIKit.Img(orbit, "OrbitCard" + i, i % 2 == 0 ? ArtLibrary.I.cardBack : ArtLibrary.I.Card("w_wild"), Color.white);
                c.type = Image.Type.Simple;
                c.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(Mathf.Cos(a) * 285f, Mathf.Sin(a) * 285f), new Vector2(86, 120));
                c.rectTransform.localEulerAngles = new Vector3(0, 0, i * 90f - 90f);
            }

            var me = UIKit.Node("Me", center).At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 360));
            UIKit.Group(me, 1f, false);
            myAvatar = AvatarBlock(me, out myName);
            var opp = UIKit.Node("Opp", center).At(new Vector2(0.5f, 0.5f), new Vector2(760, 0), new Vector2(300, 360));
            UIKit.Group(opp, 0f, false);
            oppAvatar = AvatarBlock(opp, out oppName);
            var vs = UIKit.Img(center, "VS", UIKit.S("vs"), Color.white);
            vs.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(250, 176));
            vs.rectTransform.localScale = Vector3.zero;
            vs.gameObject.AddComponent<CanvasGroup>().alpha = 0f;

            tableRow = UIKit.Node("TableRow", safe).At(new Vector2(0.5f, 0.5f), new Vector2(0, -115), new Vector2(1000, 150));

            var statusRoot = UIKit.Node("Status", safe).At(new Vector2(0.5f, 0.5f), new Vector2(0, -330), new Vector2(1000, 260));
            UIKit.Group(statusRoot, 1f, false);
            status = UIKit.Txt(statusRoot, "Text", "FINDING PLAYERS...", 64, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).Shadowed();
            status.characterSpacing = 6;
            status.Gradient(Color.white, new Color(1f, 0.82f, 0.3f));
            status.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(1000, 96));
            status.gameObject.AddComponent<WaveText>().amplitude = 6f;

            var timerChip = UIKit.Chip(statusRoot, "TimerChip", 70);
            timerChip.At(new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(200, 70));
            UIKit.LayoutIcon(timerChip, "Clock", UIKit.S("icon_clock"), 36, Palette.Gold);
            timer = UIKit.Txt(timerChip, "Timer", "0:00", 40, Palette.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            timer.rectTransform.sizeDelta = new Vector2(100, 70);

            modeChip = UIKit.Chip(statusRoot, "ModeChip", 58, new Color(0.95f, 0.5f, 0.1f, 0.9f), 26f);
            modeChip.At(new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(300, 58));
            UIKit.LayoutIcon(modeChip, "Icon", UIKit.S("icon_bolt"), 30, Color.white);
            mode = UIKit.Txt(modeChip, "Text", "OFFLINE MATCH", 30, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            mode.characterSpacing = 4;
            mode.rectTransform.sizeDelta = new Vector2(260, 58);
            modeChip.gameObject.SetActive(false);

            // tip card with a gold TIP badge
            var tipCard = UIKit.Img(safe, "TipCard", UIKit.S("panel"), new Color(1, 1, 1, 0.95f)).rectTransform;
            tipCard.At(new Vector2(0.5f, 0.5f), new Vector2(0, -565), new Vector2(920, 150));
            tipCard.GetComponent<Image>().pixelsPerUnitMultiplier = 1.6f;
            var badge = UIKit.Img(tipCard, "Badge", UIKit.S("rounded"), Palette.Gold).rectTransform;
            badge.At(new Vector2(0, 0.5f), new Vector2(84, 0), new Vector2(112, 58));
            badge.GetComponent<Image>().pixelsPerUnitMultiplier = 2.2f;
            var badgeText = UIKit.Txt(badge, "Text", "TIP", 32, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            badgeText.characterSpacing = 4;
            badgeText.rectTransform.Fill(0, 2, 0, 0);
            tip = UIKit.Txt(tipCard, "Text", "", 32, new Color(1, 1, 1, 0.92f), TextAlignmentOptions.Left);
            tip.textWrappingMode = TextWrappingModes.Normal;
            tip.rectTransform.Fill(166, 14, 34, 14);

            cancel = UIKit.Btn(safe, "Cancel", UIKit.S("btn_red"), new Vector2(420, 132), "CANCEL", 50);
            ((RectTransform)cancel.transform).At(new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(420, 132));
        }


        static Image AvatarBlock(RectTransform parent, out TextMeshProUGUI name)
        {
            var glow = UIKit.Img(parent, "Glow", UIKit.S("glow"), new Color(0.6f, 0.5f, 1f, 0.6f));
            glow.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(380, 380));
            var ring = UIKit.Img(parent, "Ring", UIKit.S("circle"), Color.white);
            ring.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(236, 236));
            var av = UIKit.Img(parent, "Avatar", ArtLibrary.I.avatars[0], Color.white);
            av.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(222, 222));
            name = UIKit.Txt(parent, "Name", "", 40, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).Shadowed();
            name.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, -122), new Vector2(320, 60));
            name.enableAutoSizing = true;
            name.fontSizeMin = 24;
            name.fontSizeMax = 40;
            return av;
        }
    }
}
