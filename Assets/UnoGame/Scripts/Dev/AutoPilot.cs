using System;
using System.Collections.Generic;
using System.IO;
using UnoGame.Core;
using UnoGame.Game;
using UnoGame.Home;
using UnityEngine;

namespace UnoGame.Dev
{
    /// <summary>
    /// Test and capture harness. Inert unless the player is started with -autopilot. It presses Play,
    /// plays the local seat with a simple strategy, takes screenshots, audits the layout and quits.
    ///   -autopilot            enable
    ///   -games N              number of games to play (default 1)
    ///   -shots DIR            save screenshots at key moments + layout report
    ///   -capture DIR          save every frame at 30 fps (for the gameplay video)
    ///   -keepopen             do not quit at the end
    /// </summary>
    public sealed class AutoPilot : MonoBehaviour
    {
        string shotsDir;
        string captureDir;
        int games = 1;
        int played;
        bool keepOpen;
        int frame;
        readonly HashSet<string> taken = new HashSet<string>();
        float thinkUntil = -1;
        int pendingColor = -1;
        float pickerSince = -1;
        float resultSince = -1;
        float mmSince = -1;
        float foundSince = -1;
        float homeSince = -1;
        int turns;
        bool finished;
        bool homeAudited;
        int superSize = 1;
        bool audioOn;
        int channels = 2;
        readonly List<float> audio = new List<float>();
        float startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-autopilot") < 0) return;
            var go = new GameObject("AutoPilot");
            DontDestroyOnLoad(go);
            var ap = go.AddComponent<AutoPilot>();
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length ? args[i + 1] : null;
                if (args[i] == "-games" && next != null) ap.games = Mathf.Max(1, int.Parse(next));
                if (args[i] == "-shots" && next != null) ap.shotsDir = next;
                if (args[i] == "-capture" && next != null) ap.captureDir = next;
                if (args[i] == "-keepopen") ap.keepOpen = true;
                if (args[i] == "-supersize" && next != null) ap.superSize = Mathf.Clamp(int.Parse(next), 1, 4);
            }
            if (ap.shotsDir != null) Directory.CreateDirectory(ap.shotsDir);
            if (ap.captureDir != null)
            {
                Directory.CreateDirectory(ap.captureDir);
                Time.captureFramerate = 30;
                // Render the game's audio in lock-step with the captured frames (written as audio.wav).
                ap.audioOn = AudioRenderer.Start();
                ap.channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            }
            ap.startedAt = Time.unscaledTime;
            Debug.Log($"[AUTO] autopilot on: games={ap.games} shots={ap.shotsDir} capture={ap.captureDir} screen={Screen.width}x{Screen.height}");
        }

        float Now => Time.unscaledTime;

        void Shot(string name)
        {
            if (shotsDir == null) return;
            string key = "g" + (played + 1) + "_" + name;
            if (!taken.Add(key)) return;
            ScreenCapture.CaptureScreenshot(Path.Combine(shotsDir, key + ".png"));
            Debug.Log("[AUTO] screenshot " + key);
        }

        void LateUpdate()
        {
            if (captureDir != null && !finished)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(captureDir, "f" + (frame++).ToString("00000") + ".png"), superSize);
                if (audioOn)
                {
                    int samples = AudioRenderer.GetSampleCountForCaptureFrame();
                    if (samples > 0)
                    {
                        using (var buf = new Unity.Collections.NativeArray<float>(samples * channels, Unity.Collections.Allocator.Temp))
                        {
                            AudioRenderer.Render(buf);
                            audio.AddRange(buf);
                        }
                    }
                }
            }
        }

        void Update()
        {
            if (finished) return;
            if (Now - startedAt > 60f * 8f * games)
            {
                Debug.LogError("[AUTO] timed out");
                Finish(2);
                return;
            }

            var home = FindAnyObjectByType<HomeScreen>();
            if (home != null)
            {
                DriveHome(home);
                return;
            }
            homeSince = -1;
            var gc = FindAnyObjectByType<GameController>();
            if (gc != null) DriveGame(gc);
        }

        void DriveHome(HomeScreen home)
        {
            if (!home.Ready || App.I.IsLoading) return;
            if (homeSince < 0) homeSince = Now;
            var mm = home.matchmaking;
            if (!mm.IsOpen)
            {
                if (played >= games) { Finish(0); return; }
                if (Now - homeSince > 1.2f)
                {
                    Shot("01_home");
                    if (played == 0 && !homeAudited) { homeAudited = true; LayoutAudit.Home(home); }
                }
                if (Now - homeSince > 1.8f) home.OnPlay();
                mmSince = -1;
                foundSince = -1;
                return;
            }
            if (mmSince < 0) mmSince = Now;
            if (Now - mmSince > 1.4f) Shot("02_searching");
            if (mm.Found)
            {
                if (foundSince < 0)
                {
                    foundSince = Now;
                    var s = App.I.Match.Session;
                    if (s != null) Debug.Log("[AUTO] matched " + (s.IsOnline ? "ONLINE" : "OFFLINE") + " vs " + s.Opponent.name);
                }
                if (Now - foundSince > 1.25f) Shot("03_found");
            }
        }

        void DriveGame(GameController gc)
        {
            if (gc.ResultOpen)
            {
                if (resultSince < 0) resultSince = Now;
                if (Now - resultSince > 1.6f) Shot("08_result");
                if (Now - resultSince > 4.0f)
                {
                    resultSince = -1;
                    played++;
                    turns = 0;
                    Debug.Log("[AUTO] game " + played + " finished");
                    if (played < games) gc.PlayAgain();
                    else gc.GoHome();
                }
                return;
            }

            if (gc.PickerOpen)
            {
                if (pickerSince < 0) pickerSince = Now;
                if (Now - pickerSince > 0.5f) Shot("06_color_picker");
                if (Now - pickerSince > 1.1f)
                {
                    pickerSince = -1;
                    gc.AutoPickColor(pendingColor < 0 ? 0 : pendingColor);
                }
                return;
            }

            if (gc.State != null && taken.Count < 64 && gc.MyCards.Count > 0) Shot("04_dealt");

            if (!gc.CanAct) { thinkUntil = -1; return; }
            if (thinkUntil < 0) { thinkUntil = Now + UnityEngine.Random.Range(0.75f, 1.35f); return; }
            if (Now < thinkUntil) return;
            thinkUntil = -1;
            turns++;
            if (turns == 2)
            {
                Shot("05_my_turn");
                if (played == 0) LayoutAudit.Game(gc);
            }

            var st = gc.State;
            var hand = new List<int>();
            foreach (var c in gc.MyCards) hand.Add(c.Id);
            int card = ViewRules.PickCard(st, hand, out int color);
            if (card >= 0)
            {
                if (hand.Count == 2) { gc.AutoTapUno(); Shot("07_uno"); }
                pendingColor = color;
                gc.AutoTapCard(card);
                if (UnityEngine.Random.value < 0.08f) gc.AutoEmote(UnityEngine.Random.Range(0, Emotes.Count));
            }
            else if (st.hasDrawn == 1) gc.AutoTapPass();
            else gc.AutoTapDeck();
        }

        void Finish(int code)
        {
            if (finished) return;
            finished = true;
            if (audioOn)
            {
                AudioRenderer.Stop();
                WriteWav(Path.Combine(captureDir, "audio.wav"), audio, channels, AudioSettings.outputSampleRate);
            }
            Debug.Log("[AUTO] done, games played: " + played + (captureDir != null ? ", frames: " + frame : ""));
            if (!keepOpen) Application.Quit(code);
        }

        static void WriteWav(string path, List<float> samples, int channels, int rate)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int dataBytes = samples.Count * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)channels);
                w.Write(rate);
                w.Write(rate * channels * 2);
                w.Write((short)(channels * 2));
                w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                foreach (float s in samples) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
            }
            Debug.Log($"[AUTO] wrote {path}: {samples.Count / channels / (float)rate:0.0}s of audio");
        }
    }
}
