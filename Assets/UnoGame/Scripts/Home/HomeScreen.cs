using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnoGame.Game;
using UnoGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.Home
{
    /// <summary>Home screen: logo, profile, online count and the Play button.</summary>
    public sealed class HomeScreen : MonoBehaviour
    {
        public RectTransform logo;
        public Button playButton;
        public RectTransform playRoot;
        public Button profileButton;
        public Image profileAvatar;
        public TextMeshProUGUI profileName;
        public TextMeshProUGUI winsText;
        public Button soundButton;
        public Image soundIcon;
        public Image onlineDot;
        public TextMeshProUGUI onlineText;
        public RectTransform floatLayer;
        public RectTransform topBar;
        public RectTransform bottomInfo;
        public MatchmakingScreen matchmaking;
        public ProfilePanel profilePanel;
        public ConfirmDialog quitDialog;

        readonly List<(RectTransform rt, Vector2 vel, float spin)> floaters = new List<(RectTransform, Vector2, float)>();

        public bool Ready { get; private set; }

        void Start()
        {
            playButton.onClick.AddListener(OnPlay);
            profileButton.onClick.AddListener(() => profilePanel.Show(RefreshProfile));
            soundButton.onClick.AddListener(() => { App.I.SetSound(!App.I.Profile.sound); RefreshSound(); });
            App.I.Match.StatusChanged += RefreshOnline;
            RefreshProfile();
            RefreshSound();
            RefreshOnline();
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
            logo.anchoredPosition = logoHome + new Vector2(0, 600);
            logo.localScale = Vector3.one * 0.7f;
            playRoot.localScale = Vector3.zero;
            topBar.anchoredPosition += new Vector2(0, 200);
            Tw.Pos(topBar, topBar.anchoredPosition - new Vector2(0, 200), 0.5f, Ease.OutCubic, 0.2f);
            Tw.Pos(logo, logoHome, 0.7f, Ease.OutBack);
            Tw.Scale(logo, 1f, 0.7f, Ease.OutBack);
            yield return Tw.Wait(0.45f);
            App.Play(Sound.Whoosh, 0.6f);
            yield return Tw.Scale(playRoot, 1f, 0.5f, Ease.OutBackStrong).Wait();
            logo.GetComponent<Floater>().Rebase();
            logo.GetComponent<Floater>().enabled = true;
            playRoot.GetComponent<Pulse>().enabled = true;
            Ready = true;
            if (App.I.AutoSearch)
            {
                App.I.AutoSearch = false;
                OnPlay();
            }
        }

        public void OnPlay()
        {
            if (matchmaking.IsOpen || App.I.IsLoading) return;
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
            if (m.ServerOnline)
            {
                onlineDot.color = Palette.Online;
                int n = Mathf.Max(1, m.OnlineCount);
                onlineText.text = n == 1 ? "Online  •  bots join if nobody's around" : $"{n:N0} players online";
            }
            else if (m.Connecting)
            {
                onlineDot.color = new Color(1f, 1f, 1f, 0.5f);
                onlineText.text = "Connecting...";
            }
            else
            {
                onlineDot.color = new Color(1f, 0.75f, 0.2f);
                onlineText.text = "Offline  •  play against bots";
            }
        }

        void SpawnFloaters()
        {
            var rnd = new System.Random();
            for (int i = 0; i < 9; i++)
            {
                int id = rnd.Next(0, 108);
                var img = UIKit.Img(floatLayer, "Float", ArtLibrary.I.Face(id), new Color(1, 1, 1, 0.22f));
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
                else if (quitDialog.IsOpen) quitDialog.Close(false);
                else if (!matchmaking.IsOpen) quitDialog.Show("Quit game?", "Come back soon!", "QUIT", "STAY", Application.Quit);
            }
        }
    }
}
