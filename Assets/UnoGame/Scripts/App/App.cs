using System.Collections;
using UnoGame.Net;
using UnoGame.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UnoGame
{
    /// <summary>
    /// Persistent app root, created before the first scene loads: config, profile, sound, matchmaking
    /// and the fade used between scenes.
    /// </summary>
    public sealed class App : MonoBehaviour
    {
        public const string HomeScene = "Home";
        public const string GameScene = "Game";

        public static App I { get; private set; }

        public GameConfig Config { get; private set; }
        public Profile Profile { get; private set; }
        public Sfx Sfx { get; private set; }
        public Matchmaker Match { get; private set; }

        /// <summary>Set by "Play again": the home screen opens matchmaking straight away.</summary>
        public bool AutoSearch;

        CanvasGroup fader;
        bool loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("App");
            DontDestroyOnLoad(go);
            go.AddComponent<App>();
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Input.multiTouchEnabled = false;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Config = GameConfig.Load();
            Profile = Profile.Load();
            Sfx = gameObject.AddComponent<Sfx>();
            Sfx.Enabled = Profile.sound;
            Match = gameObject.AddComponent<Matchmaker>();
            Match.Init(Config, Profile);
            BuildFader();
            Debug.Log("[App] started, server " + Config.ServerUrl + (Config.forceOffline ? " (offline mode)" : ""));
        }

        public static void Play(Sound s, float volume = 1f, float pitch = 1f)
        {
            if (I != null) I.Sfx.Play(s, volume, pitch);
        }

        public void SetSound(bool on)
        {
            Profile.sound = on;
            Profile.Save();
            Sfx.Enabled = on;
        }

        public bool IsLoading => loading;

        public void LoadScene(string scene)
        {
            if (!loading) StartCoroutine(LoadRoutine(scene));
        }

        IEnumerator LoadRoutine(string scene)
        {
            loading = true;
            fader.blocksRaycasts = true;
            loadTitle.text = scene == GameScene ? "PREPARING GAME" : "LOADING";
            loadTip.text = LoadingTips[Random.Range(0, LoadingTips.Length)];
            SetProgress(0f);
            yield return Tw.Alpha(fader, 1f, 0.28f, Ease.InOutQuad).Wait();

            // Fill the bar smoothly: it never runs ahead of the real load, and stays up long enough to read.
            var op = SceneManager.LoadSceneAsync(scene);
            op.allowSceneActivation = false;
            float shown = 0f, started = Time.unscaledTime;
            while (shown < 0.999f)
            {
                float real = Mathf.Clamp01(op.progress / 0.9f);
                float paced = Mathf.Clamp01((Time.unscaledTime - started) / MinLoadSeconds);
                shown = Mathf.MoveTowards(shown, Mathf.Min(real, paced), Time.unscaledDeltaTime * 2.2f);
                SetProgress(shown);
                yield return null;
            }
            SetProgress(1f);
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return null;
            yield return Tw.Alpha(fader, 0f, 0.32f, Ease.InOutQuad).Wait();
            fader.blocksRaycasts = false;
            loading = false;
        }

        const float MinLoadSeconds = 1.1f;

        static readonly string[] LoadingTips =
        {
            "Match the color or the number of the top card",
            "Stack +2 and +4 cards to pass the penalty on",
            "Tap UNO! before playing your second-last card",
            "Skip and Reverse give you another turn",
            "Wild Draw 4 only works with no card of the current color",
        };

        TMPro.TextMeshProUGUI loadTitle, loadPercent, loadTip;
        RectTransform loadFill;
        float loadFillWidth;

        void SetProgress(float k)
        {
            loadFill.sizeDelta = new Vector2(Mathf.Max(30f, loadFillWidth * k), loadFill.sizeDelta.y);
            loadPercent.text = Mathf.RoundToInt(k * 100f) + "%";
        }

        /// <summary>Full-screen loading overlay used between scenes (fades in/out over the old/new scene).</summary>
        void BuildFader()
        {
            var go = new GameObject("Loading", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);
            go.layer = 5;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            go.AddComponent<AdaptiveScaler>();
            fader = go.GetComponent<CanvasGroup>();
            fader.alpha = 0f;
            fader.blocksRaycasts = false;
            var root = (RectTransform)go.transform;
            var c = new Vector2(0.5f, 0.5f);

            var art = ArtLibrary.I;
            var bg = UIKit.Img(root, "Background", art != null ? art.bgHome : null, art != null ? Color.white : new Color(0.1f, 0.06f, 0.25f));
            bg.rectTransform.Fill();
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1080f / 1920f;
            var shade = UIKit.Img(root, "Shade", null, new Color(0.03f, 0.02f, 0.08f, 0.45f));
            shade.rectTransform.Fill(-50, -50, -50, -50);
            if (art == null) return;

            var glow = UIKit.Img(root, "Glow", UIKit.S("glow"), new Color(0.6f, 0.45f, 1f, 0.55f));
            glow.rectTransform.At(c, new Vector2(0, 170), new Vector2(520, 520));
            glow.gameObject.AddComponent<Pulse>().amount = 0.08f;
            var cardRoot = UIKit.Node("Card", root).At(c, new Vector2(0, 170), new Vector2(200, 280));
            var card = UIKit.Img(cardRoot, "Face", art.cardBack, Color.white);
            card.type = Image.Type.Simple;
            card.rectTransform.Fill();
            cardRoot.gameObject.AddComponent<CardFlipper>().face = card;

            loadTitle = UIKit.Txt(root, "Title", "PREPARING GAME", 64, Color.white, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold).Shadowed();
            loadTitle.characterSpacing = 8;
            loadTitle.Gradient(Color.white, new Color(1f, 0.82f, 0.3f));
            loadTitle.rectTransform.At(c, new Vector2(0, -60), new Vector2(1000, 96));
            loadTitle.gameObject.AddComponent<WaveText>().amplitude = 7f;

            var track = UIKit.Img(root, "Track", UIKit.S("rounded"), new Color(0f, 0f, 0f, 0.45f));
            track.rectTransform.At(c, new Vector2(0, -170), new Vector2(620, 34));
            track.pixelsPerUnitMultiplier = 128f / 34f;
            var trackBorder = UIKit.Img(track.rectTransform, "Border", UIKit.S("rounded"), new Color(1, 1, 1, 0.18f));
            trackBorder.rectTransform.Fill(-3, -3, -3, -3);
            trackBorder.pixelsPerUnitMultiplier = 128f / 40f;
            trackBorder.transform.SetAsFirstSibling();
            var fill = UIKit.Img(track.rectTransform, "Fill", UIKit.S("rounded"), Palette.Gold);
            fill.pixelsPerUnitMultiplier = 128f / 26f;
            loadFill = fill.rectTransform;
            loadFill.At(new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(30, 26), new Vector2(0, 0.5f));
            loadFillWidth = 612f;
            var shine = UIKit.Img(loadFill, "Shine", UIKit.S("rounded"), new Color(1, 1, 1, 0.35f));
            shine.pixelsPerUnitMultiplier = 128f / 10f;
            shine.rectTransform.Fill(8, 14, 8, 4);

            loadPercent = UIKit.Txt(root, "Percent", "0%", 44, Palette.Gold, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold).Shadowed();
            loadPercent.rectTransform.At(c, new Vector2(0, -235), new Vector2(300, 60));

            loadTip = UIKit.Txt(root, "Tip", "", 34, new Color(1, 1, 1, 0.75f));
            loadTip.textWrappingMode = TMPro.TextWrappingModes.Normal;
            loadTip.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0, 220), new Vector2(880, 110));
        }
    }
}
