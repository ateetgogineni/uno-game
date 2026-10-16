using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnoGame.Game;
using UnoGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.Home
{
    /// <summary>Home screen: logo, profile, online status and the list of game modes.</summary>
    public sealed class HomeScreen : MonoBehaviour
    {
        public RectTransform logo;
        public Button profileButton;
        public Image profileAvatar;
        public TextMeshProUGUI profileName;
        public TextMeshProUGUI winsText;
        public Button soundButton;
        public Image soundIcon;
        public Image onlineDot;
        public Image onlineHalo;
        public Image onlineIcon;
        public RectTransform onlinePill;
        public TextMeshProUGUI onlineText;
        public RectTransform modesHeader;
        public RectTransform[] modeTiles;
        public Button[] modeButtons;
        public RectTransform floatLayer;
        public RectTransform topBar;
        public RectTransform bottomInfo;
        public ModeInfoPanel modeInfo;
        public MatchmakingScreen matchmaking;
        public ProfilePanel profilePanel;
        public ConfirmDialog quitDialog;

        readonly List<(RectTransform rt, Vector2 vel, float spin)> floaters = new List<(RectTransform, Vector2, float)>();

        public bool Ready { get; private set; }

        void Start()
        {
            for (int i = 0; i < modeButtons.Length; i++)
            {
                var m = ModeInfo.All[i];
                modeButtons[i].onClick.AddListener(() => SelectMode(m.Id));
            }
            profileButton.onClick.AddListener(() => profilePanel.Show(RefreshProfile));
            soundButton.onClick.AddListener(() => { App.I.SetSound(!App.I.Profile.sound); RefreshSound(); });
            App.I.Match.StatusChanged += RefreshOnline;
            RefreshProfile();
            RefreshSound();
            RefreshOnline();
            var version = bottomInfo.GetComponentInChildren<TextMeshProUGUI>();
            if (version != null) version.text = "v" + Application.version;
            SpawnFloaters();
            App.I.Match.Probe();
            StartCoroutine(Intro());
        }

        void OnDestroy()
        {
            if (App.I != null) App.I.Match.StatusChanged -= RefreshOnline;
        }

        IEnumerator Intro()
        {
            var logoHome = logo.anchoredPosition;
            logo.anchoredPosition = logoHome + new Vector2(0, 500);
            logo.localScale = Vector3.one * 0.7f;
            onlinePill.localScale = Vector3.zero;
            var headerGroup = UIKit.Group(modesHeader, 0f, false);
            foreach (var t in modeTiles) t.localScale = Vector3.zero;
            topBar.anchoredPosition += new Vector2(0, 200);
            Tw.Pos(topBar, topBar.anchoredPosition - new Vector2(0, 200), 0.5f, Ease.OutCubic, 0.2f);
            Tw.Pos(logo, logoHome, 0.7f, Ease.OutBack);
            Tw.Scale(logo, 1f, 0.7f, Ease.OutBack);
            Tw.Scale(onlinePill, 1f, 0.45f, Ease.OutBack, 0.45f);
            Tw.Alpha(headerGroup, 1f, 0.4f, Ease.OutQuad, 0.5f);
            for (int i = 0; i < modeTiles.Length; i++)
                Tw.Scale(modeTiles[i], 1f, 0.5f, Ease.OutBackStrong, 0.55f + i * 0.07f);
            yield return Tw.Wait(0.35f);
            App.Play(Sound.Whoosh, 0.6f);
            yield return Tw.Wait(0.75f);
            logo.GetComponent<Floater>().Rebase();
            logo.GetComponent<Floater>().enabled = true;
            Ready = true;
            if (App.I.AutoSearch)
            {
                App.I.AutoSearch = false;
                StartMatch(ModeInfo.Get(App.I.SelectedMode));
            }
        }

        /// <summary>Shows the rules for a mode; its PLAY button starts matchmaking.</summary>
        public void SelectMode(string id)
        {
            if (matchmaking.IsOpen || modeInfo.IsOpen || App.I.IsLoading) return;
            modeInfo.Show(ModeInfo.Get(id), StartMatch);
        }

        public void StartMatch(ModeInfo m)
        {
            if (matchmaking.IsOpen || App.I.IsLoading) return;
            App.I.SelectedMode = m.Id;
            matchmaking.Open();
        }

        void RefreshProfile()
        {
            var p = App.I.Profile;
            profileAvatar.sprite = ArtLibrary.I.Avatar(p.avatar);
            profileName.text = p.name;
            winsText.text = p.wins.ToString();
        }

        void RefreshSound()
        {
            soundIcon.sprite = UIKit.S(App.I.Profile.sound ? "icon_sound_on" : "icon_sound_off");
        }

        void RefreshOnline()
        {
            var m = App.I.Match;
            Color dot;
            string icon;
            bool spin = false;
            if (m.ServerOnline)
            {
                dot = Palette.Online;
                icon = "icon_users";
                int n = Mathf.Max(1, m.OnlineCount);
                onlineText.text = n == 1 ? "ONLINE  •  READY TO PLAY" : $"<color=#FFD447>{n:N0}</color>  PLAYERS ONLINE";
            }
            else if (m.Connecting)
            {
                dot = new Color(1f, 1f, 1f, 0.6f);
                icon = "icon_replay";
                spin = true;
                onlineText.text = "CONNECTING...";
            }
            else
            {
                dot = new Color(1f, 0.7f, 0.15f);
                icon = "icon_bolt";
                onlineText.text = "OFFLINE  •  PLAY VS BOTS";
            }
            onlineDot.color = dot;
            onlineHalo.color = new Color(dot.r, dot.g, dot.b, 0.35f);
            onlineIcon.sprite = UIKit.S(icon);
            var spinner = onlineIcon.GetComponent<Spin>();
            if (spinner == null) spinner = onlineIcon.gameObject.AddComponent<Spin>();
            spinner.degreesPerSecond = -240f;
            spinner.enabled = spin;
            if (!spin) onlineIcon.transform.localEulerAngles = Vector3.zero;
        }

        void SpawnFloaters()
        {
            var rnd = new System.Random();
            var cards = ArtLibrary.I.cards;
            for (int i = 0; i < 9 && cards.Count > 0; i++)
            {
                var img = UIKit.Img(floatLayer, "Float", cards[rnd.Next(cards.Count)], new Color(1, 1, 1, 0.2f));
                img.type = Image.Type.Simple;
                var rt = img.rectTransform;
                float s = 0.5f + (float)rnd.NextDouble() * 0.45f;
                rt.sizeDelta = new Vector2(CardView.Width * s, CardView.Height * s);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(rnd.Next(-560, 560), rnd.Next(-1150, 1150));
                rt.localEulerAngles = new Vector3(0, 0, rnd.Next(0, 360));
                floaters.Add((rt, new Vector2(rnd.Next(-25, 25), 40 + rnd.Next(0, 50)), rnd.Next(-30, 30)));
            }
        }

        void Update()
        {
            float h = floatLayer.rect.height * 0.5f + 220f;
            float w = floatLayer.rect.width * 0.5f + 160f;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < floaters.Count; i++)
            {
                var f = floaters[i];
                var p = f.rt.anchoredPosition + f.vel * dt;
                if (p.y > h) p = new Vector2(Random.Range(-w + 160, w - 160), -h);
                f.rt.anchoredPosition = p;
                f.rt.Rotate(0, 0, f.spin * dt);
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (profilePanel.IsOpen) profilePanel.Hide();
                else if (modeInfo.IsOpen) modeInfo.Hide();
                else if (quitDialog.IsOpen) quitDialog.Close(false);
                else if (!matchmaking.IsOpen) quitDialog.Show("Quit game?", "Come back soon!", "QUIT", "STAY", Application.Quit);
            }
        }
    }
}
