using System;
using System.Collections;
using TMPro;
using UnoGame.Core;
using UnoGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.Game
{
    /// <summary>
    /// UNO Dare: "do the dare or draw". The physical dares become quick on-screen challenges:
    /// tap frenzy, reflex test, colour trap and hold steady. Success keeps your turn; failing draws.
    /// </summary>
    public sealed class DarePanel : MonoBehaviour
    {
        public CanvasGroup group;
        public RectTransform panel;
        public TextMeshProUGUI title;
        public TextMeshProUGUI body;
        public RectTransform choiceRoot;
        public Button doButton;
        public Button drawButton;
        public TextMeshProUGUI drawLabel;
        public RectTransform challengeRoot;
        public TextMeshProUGUI instruction;
        public Image timerFill;
        public Image zone;
        public Button bigButton;
        public Image bigImage;
        public TextMeshProUGUI bigLabel;
        public TextMeshProUGUI countText;
        public TextMeshProUGUI wordText;
        public Button[] balls = new Button[4];
        public TextMeshProUGUI resultText;

        enum Kind { TapFrenzy, Reflex, ColorTrap, HoldSteady }
        const int TapGoal = 14;
        const float TapTime = 4f, ReflexWindow = 0.8f, TrapTime = 3f, HoldFull = 3f, ZoneMin = 0.55f, ZoneMax = 0.78f;

        Action<bool> done;
        Kind kind;
        bool running, finished;
        float started, goAt, holdStart = -1;
        int taps, inkColor, side;
        float trackWidth;

        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            doButton.onClick.AddListener(StartChallenge);
            drawButton.onClick.AddListener(() => Finish(false, false));
            bigButton.onClick.AddListener(OnBigClick);
            var hold = bigButton.GetComponent<HoldButton>();
            hold.Down += OnHoldDown;
            hold.Up += OnHoldUp;
            for (int i = 0; i < balls.Length; i++)
            {
                int c = i;
                balls[i].onClick.AddListener(() => OnBall(c));
            }
        }

        public void Show(int amount, int flipSide, Action<bool> onDone)
        {
            done = onDone;
            side = flipSide;
            finished = false;
            running = false;
            gameObject.SetActive(true);
            title.text = "DARE!";
            body.text = $"Do a dare, or draw {amount} cards?";
            drawLabel.text = $"DRAW {amount}";
            choiceRoot.gameObject.SetActive(true);
            challengeRoot.gameObject.SetActive(false);
            resultText.gameObject.SetActive(false);
            group.alpha = 0f;
            group.blocksRaycasts = true;
            Tw.Alpha(group, 1f, 0.2f);
            panel.localScale = Vector3.one * 0.7f;
            Tw.Scale(panel, 1f, 0.4f, Ease.OutBack);
            App.Play(Sound.Uno, 0.6f, 0.8f);
        }

        void StartChallenge()
        {
            if (running || finished) return;
            running = true;
            choiceRoot.gameObject.SetActive(false);
            challengeRoot.gameObject.SetActive(true);
            kind = (Kind)UnityEngine.Random.Range(0, 4);
            started = Time.unscaledTime;
            taps = 0;
            holdStart = -1;
            trackWidth = ((RectTransform)timerFill.transform.parent).rect.width;
            bigButton.gameObject.SetActive(kind != Kind.ColorTrap);
            countText.gameObject.SetActive(kind == Kind.TapFrenzy);
            wordText.gameObject.SetActive(kind == Kind.ColorTrap);
            zone.gameObject.SetActive(kind == Kind.HoldSteady);
            foreach (var b in balls) b.gameObject.SetActive(kind == Kind.ColorTrap);
            bigImage.color = Palette.TurnBlue;
            timerFill.color = Palette.Gold;
            switch (kind)
            {
                case Kind.TapFrenzy:
                    title.text = "TAP FRENZY";
                    instruction.text = $"Tap the button {TapGoal} times in {TapTime:0} seconds!";
                    bigLabel.text = "TAP!";
                    countText.text = $"0 / {TapGoal}";
                    break;
                case Kind.Reflex:
                    title.text = "REFLEX TEST";
                    instruction.text = "Wait for <color=#5ED46E>GREEN</color>, then tap as fast as you can!";
                    bigLabel.text = "WAIT...";
                    bigImage.color = Palette.Red;
                    goAt = started + UnityEngine.Random.Range(1.2f, 2.8f);
                    break;
                case Kind.ColorTrap:
                {
                    title.text = "COLOUR TRAP";
                    instruction.text = "Tap the colour of the <b>INK</b>, not the word!";
                    int word = UnityEngine.Random.Range(0, 4);
                    inkColor = (word + UnityEngine.Random.Range(1, 4)) % 4;
                    wordText.text = Cards.ColorName(word, side).ToUpper();
                    wordText.color = Palette.ForCard(inkColor, side);
                    for (int i = 0; i < balls.Length; i++) balls[i].GetComponent<Image>().sprite = ArtLibrary.I.Ball(i, side);
                    break;
                }
                case Kind.HoldSteady:
                    title.text = "HOLD STEADY";
                    instruction.text = "Hold the button and let go inside the <color=#5ED46E>green zone</color>.";
                    bigLabel.text = "HOLD";
                    var z = zone.rectTransform;
                    z.anchoredPosition = new Vector2(trackWidth * ZoneMin, 0);
                    z.sizeDelta = new Vector2(trackWidth * (ZoneMax - ZoneMin), z.sizeDelta.y);
                    break;
            }
            SetBar(1f);
        }

        void SetBar(float k)
        {
            var rt = timerFill.rectTransform;
            rt.sizeDelta = new Vector2(Mathf.Max(8f, trackWidth * Mathf.Clamp01(k)), rt.sizeDelta.y);
        }

        void Update()
        {
            if (!running || finished) return;
            float t = Time.unscaledTime - started;
            switch (kind)
            {
                case Kind.TapFrenzy:
                    SetBar(1f - t / TapTime);
                    if (t > TapTime) Finish(false, true);
                    break;
                case Kind.Reflex:
                    if (Time.unscaledTime >= goAt && bigLabel.text != "TAP!")
                    {
                        bigLabel.text = "TAP!";
                        bigImage.color = Palette.Green;
                        App.Play(Sound.Tick);
                    }
                    SetBar(Time.unscaledTime < goAt ? 1f : 1f - (Time.unscaledTime - goAt) / ReflexWindow);
                    if (Time.unscaledTime > goAt + ReflexWindow) Finish(false, true);
                    break;
                case Kind.ColorTrap:
                    SetBar(1f - t / TrapTime);
                    if (t > TrapTime) Finish(false, true);
                    break;
                case Kind.HoldSteady:
                    if (holdStart < 0)
                    {
                        SetBar(0f);
                        if (t > 4f) Finish(false, true);
                    }
                    else
                    {
                        float k = (Time.unscaledTime - holdStart) / HoldFull;
                        SetBar(k);
                        timerFill.color = k >= ZoneMin && k <= ZoneMax ? Palette.Green : Palette.Gold;
                        if (k >= 1f) Finish(false, true);
                    }
                    break;
            }
        }

        void OnBigClick()
        {
            if (!running || finished) return;
            if (kind == Kind.TapFrenzy)
            {
                taps++;
                countText.text = $"{taps} / {TapGoal}";
                Tw.Punch(bigButton.transform, 0.08f, 0.12f);
                App.Play(Sound.Click, 0.6f, 1f + taps * 0.03f);
                if (taps >= TapGoal) Finish(true, true);
            }
            else if (kind == Kind.Reflex)
            {
                Finish(Time.unscaledTime >= goAt, true);
            }
        }

        void OnHoldDown()
        {
            if (!running || finished || kind != Kind.HoldSteady) return;
            holdStart = Time.unscaledTime;
        }

        void OnHoldUp()
        {
            if (!running || finished || kind != Kind.HoldSteady || holdStart < 0) return;
            float k = (Time.unscaledTime - holdStart) / HoldFull;
            Finish(k >= ZoneMin && k <= ZoneMax, true);
        }

        void OnBall(int c)
        {
            if (!running || finished || kind != Kind.ColorTrap) return;
            Finish(c == inkColor, true);
        }

        void Finish(bool ok, bool attempted)
        {
            if (finished) return;
            finished = true;
            running = false;
            StartCoroutine(FinishRoutine(ok, attempted));
        }

        IEnumerator FinishRoutine(bool ok, bool attempted)
        {
            if (attempted)
            {
                resultText.gameObject.SetActive(true);
                resultText.text = ok ? "DARE COMPLETE!" : "DARE FAILED";
                resultText.color = ok ? Palette.Online : new Color(1f, 0.45f, 0.45f);
                resultText.transform.localScale = Vector3.one * 2f;
                Tw.Scale(resultText.transform, 1f, 0.4f, Ease.OutBackStrong);
                App.Play(ok ? Sound.Found : Sound.Error);
                yield return Tw.Wait(1.0f);
            }
            group.blocksRaycasts = false;
            yield return Tw.Alpha(group, 0f, 0.2f).Wait();
            gameObject.SetActive(false);
            var cb = done;
            done = null;
            cb?.Invoke(ok);
        }

        /// <summary>Autopilot: answer without a person.</summary>
        public void AutoAnswer(bool doDare, bool succeed)
        {
            if (!IsOpen || finished) return;
            if (!doDare) { Finish(false, false); return; }
            StartChallenge();
            Finish(succeed, true);
        }

        // ------------------------------------------------------------ layout (editor builder)

        public void Build()
        {
            var c = new Vector2(0.5f, 0.5f);
            var root = (RectTransform)transform;
            root.Fill();
            group = UIKit.Group(root);
            UIKit.Dim(root, 0.72f);
            panel = UIKit.Img(root, "Panel", UIKit.S("panel"), Color.white, true).rectTransform;
            panel.At(c, new Vector2(0, 120), new Vector2(920, 1040));
            title = UIKit.Txt(panel, "Title", "DARE!", 92, Color.white, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic).Shadowed();
            title.Gradient(new Color(1f, 0.95f, 0.5f), new Color(1f, 0.55f, 0.1f));
            title.rectTransform.At(c, new Vector2(0, 410), new Vector2(860, 130));
            title.gameObject.AddComponent<WaveText>().amplitude = 5f;
            body = UIKit.Txt(panel, "Body", "", 42, new Color(1, 1, 1, 0.85f));
            body.textWrappingMode = TextWrappingModes.Normal;
            body.rectTransform.At(c, new Vector2(0, 300), new Vector2(800, 110));

            choiceRoot = UIKit.Node("Choice", panel).Fill();
            doButton = UIKit.Btn(choiceRoot, "DoDare", UIKit.S("btn_green"), new Vector2(620, 150), "DO THE DARE", 54);
            ((RectTransform)doButton.transform).At(c, new Vector2(0, 70), new Vector2(620, 150));
            drawButton = UIKit.Btn(choiceRoot, "Draw", UIKit.S("btn_red"), new Vector2(520, 130), "DRAW 2", 48);
            ((RectTransform)drawButton.transform).At(c, new Vector2(0, -110), new Vector2(520, 130));
            drawLabel = drawButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            var hint = UIKit.Txt(choiceRoot, "Hint", "Complete the dare and you keep your turn.", 32, new Color(1, 1, 1, 0.6f));
            hint.rectTransform.At(c, new Vector2(0, -260), new Vector2(820, 50));

            challengeRoot = UIKit.Node("Challenge", panel).Fill();
            instruction = UIKit.Txt(challengeRoot, "Instruction", "", 38, Color.white);
            instruction.textWrappingMode = TextWrappingModes.Normal;
            instruction.rectTransform.At(c, new Vector2(0, 290), new Vector2(820, 110));
            var track = UIKit.Img(challengeRoot, "Track", UIKit.S("rounded_small"), new Color(0, 0, 0, 0.45f));
            track.rectTransform.At(c, new Vector2(0, 195), new Vector2(720, 30));
            track.pixelsPerUnitMultiplier = 1.4f;
            zone = UIKit.Img(track.rectTransform, "Zone", UIKit.S("rounded_small"), new Color(0.37f, 0.83f, 0.43f, 0.45f));
            zone.rectTransform.At(new Vector2(0, 0.5f), Vector2.zero, new Vector2(100, 30), new Vector2(0, 0.5f));
            zone.pixelsPerUnitMultiplier = 1.4f;
            timerFill = UIKit.Img(track.rectTransform, "Fill", UIKit.S("rounded_small"), Palette.Gold);
            timerFill.rectTransform.At(new Vector2(0, 0.5f), Vector2.zero, new Vector2(720, 30), new Vector2(0, 0.5f));
            timerFill.pixelsPerUnitMultiplier = 1.4f;

            bigImage = UIKit.Img(challengeRoot, "BigButton", UIKit.S("circle"), Palette.TurnBlue, true);
            bigImage.rectTransform.At(c, new Vector2(0, -40), new Vector2(320, 320));
            bigButton = bigImage.gameObject.AddComponent<Button>();
            bigButton.transition = Selectable.Transition.None;
            bigImage.gameObject.AddComponent<HoldButton>();
            var ring = UIKit.Img(bigImage.transform, "Ring", UIKit.S("ring_thick"), new Color(1, 1, 1, 0.5f));
            ring.rectTransform.Fill(-14, -14, -14, -14);
            bigLabel = UIKit.Txt(bigImage.transform, "Label", "TAP!", 64, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).Shadowed();
            bigLabel.rectTransform.Fill();
            countText = UIKit.Txt(challengeRoot, "Count", "", 50, Palette.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            countText.rectTransform.At(c, new Vector2(0, -270), new Vector2(400, 70));
            wordText = UIKit.Txt(challengeRoot, "Word", "RED", 130, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).Shadowed();
            wordText.rectTransform.At(c, new Vector2(0, 40), new Vector2(820, 170));
            for (int i = 0; i < 4; i++)
            {
                var b = UIKit.Btn(challengeRoot, "Ball" + i, ArtLibrary.I.balls[i], new Vector2(160, 160));
                b.GetComponent<Image>().type = Image.Type.Simple;
                ((RectTransform)b.transform).At(c, new Vector2(-282 + i * 188, -190), new Vector2(160, 160));
                balls[i] = b;
            }
            resultText = UIKit.Txt(panel, "Result", "", 80, Color.white, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic).Shadowed();
            resultText.rectTransform.At(c, new Vector2(0, -40), new Vector2(880, 140));
        }
    }
}
