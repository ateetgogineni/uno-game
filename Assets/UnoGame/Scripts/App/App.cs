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
            yield return Tw.Alpha(fader, 1f, 0.28f, Ease.InOutQuad).Wait();
            var op = SceneManager.LoadSceneAsync(scene);
            while (!op.isDone) yield return null;
            yield return null;
            yield return Tw.Alpha(fader, 0f, 0.32f, Ease.InOutQuad).Wait();
            fader.blocksRaycasts = false;
            loading = false;
        }

        void BuildFader()
        {
            var go = new GameObject("Fader", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            fader = go.GetComponent<CanvasGroup>();
            fader.alpha = 0f;
            fader.blocksRaycasts = false;
            var img = new GameObject("Black", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(go.transform, false);
            var rt = (RectTransform)img.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            img.GetComponent<Image>().color = new Color(0.04f, 0.03f, 0.08f, 1f);
        }
    }
}
