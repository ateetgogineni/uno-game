using System;
using UnityEngine;

namespace UnoGame
{
    public enum Sound { Click, Card, Deal, Play, Draw, Turn, Uno, Whoosh, Plus, Win, Lose, Tick, Found, Pop, Error, Shuffle }

    /// <summary>
    /// Sound effects synthesised at startup (no audio files to license or ship). Each clip is built from
    /// filtered noise and enveloped sine partials; played through a small pool of AudioSources.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        const int Rate = 44100;
        AudioClip[] clips;
        AudioSource[] voices;
        int next;
        public bool Enabled = true;

        void Awake()
        {
            voices = new AudioSource[8];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }
            Build();
        }

        public void Play(Sound s, float volume = 1f, float pitch = 1f)
        {
            if (!Enabled || clips == null) return;
            var v = voices[next];
            next = (next + 1) % voices.Length;
            v.pitch = pitch;
            // 0.75 headroom: several effects often overlap (deal, win fanfare) and must not clip.
            v.PlayOneShot(clips[(int)s], volume * 0.75f);
        }

        void Build()
        {
            var rnd = new System.Random(1234);
            clips = new AudioClip[Enum.GetValues(typeof(Sound)).Length];
            clips[(int)Sound.Click] = Make("click", 0.06f, (t, i) => Tone(t, 1250, 0.012f) * 0.5f + Tone(t, 2500, 0.008f) * 0.15f);
            clips[(int)Sound.Card] = MakeNoise("card", 0.09f, rnd, 0.002f, 0.022f, 0.75f, 0.55f);
            clips[(int)Sound.Deal] = MakeNoise("deal", 0.06f, rnd, 0.001f, 0.012f, 0.8f, 0.4f);
            {
                var noise = NoiseBuffer(0.16f, rnd, 0.25f);
                clips[(int)Sound.Play] = Make("play", 0.16f, (t, i) =>
                    noise[i] * Env(t, 0.001f, 0.028f) * 0.7f + (float)Math.Sin(2 * Math.PI * (140 - 300 * t) * t) * Env(t, 0.002f, 0.06f) * 0.45f);
            }
            {
                var noise = NoiseBuffer(0.2f, rnd, 0f);
                float lp = 0;
                clips[(int)Sound.Draw] = Make("draw", 0.2f, (t, i) =>
                {
                    float cutoff = Mathf.Lerp(0.05f, 0.6f, t / 0.2f);
                    lp += (noise[i] - lp) * cutoff;
                    return lp * Mathf.Sin(Mathf.PI * t / 0.2f) * 0.7f;
                });
            }
            clips[(int)Sound.Turn] = Make("turn", 0.55f, (t, i) => Bell(t, 880f, 0f, 0.22f) * 0.32f + Bell(t, 1318.5f, 0.09f, 0.28f) * 0.3f);
            clips[(int)Sound.Uno] = Make("uno", 0.8f, (t, i) =>
                (Bell(t, 1046.5f, 0f, 0.3f) + Bell(t, 1318.5f, 0.07f, 0.3f) + Bell(t, 1568f, 0.14f, 0.3f) + Bell(t, 2093f, 0.21f, 0.35f)) * 0.2f);
            {
                var noise = NoiseBuffer(0.35f, rnd, 0f);
                float lp = 0;
                clips[(int)Sound.Whoosh] = Make("whoosh", 0.35f, (t, i) =>
                {
                    float k = Mathf.Sin(Mathf.PI * t / 0.35f);
                    lp += (noise[i] - lp) * (0.03f + 0.35f * k);
                    return lp * k * 0.8f;
                });
            }
            clips[(int)Sound.Plus] = Make("plus", 0.45f, (t, i) =>
                (float)Math.Sin(2 * Math.PI * (320 - 220 * Mathf.Min(1, t / 0.35f)) * t) * Env(t, 0.004f, 0.16f) * 0.55f);
            clips[(int)Sound.Win] = Make("win", 1.6f, (t, i) =>
                (Pluck(t, 523.25f, 0f) + Pluck(t, 659.25f, 0.12f) + Pluck(t, 783.99f, 0.24f) + Pluck(t, 1046.5f, 0.36f) +
                 Bell(t, 1046.5f, 0.52f, 0.7f) + Bell(t, 1318.5f, 0.52f, 0.7f) + Bell(t, 1568f, 0.52f, 0.7f)) * 0.16f);
            clips[(int)Sound.Lose] = Make("lose", 1.3f, (t, i) =>
                (Bell(t, 783.99f, 0f, 0.35f) + Bell(t, 622.25f, 0.22f, 0.35f) + Bell(t, 523.25f, 0.44f, 0.6f) + Bell(t, 392f, 0.44f, 0.6f) * 0.6f) * 0.2f);
            clips[(int)Sound.Tick] = Make("tick", 0.05f, (t, i) => Tone(t, 1800, 0.008f) * 0.35f);
            clips[(int)Sound.Found] = Make("found", 0.9f, (t, i) =>
                (Bell(t, 783.99f, 0f, 0.25f) + Bell(t, 987.77f, 0.07f, 0.25f) + Bell(t, 1174.66f, 0.14f, 0.3f) + Bell(t, 1567.98f, 0.21f, 0.45f)) * 0.22f);
            clips[(int)Sound.Pop] = Make("pop", 0.14f, (t, i) =>
                (float)Math.Sin(2 * Math.PI * (380 + 2600 * t) * t) * Env(t, 0.002f, 0.045f) * 0.5f);
            clips[(int)Sound.Error] = Make("error", 0.22f, (t, i) =>
                ((float)Math.Sin(2 * Math.PI * 150 * t) + 0.35f * (float)Math.Sin(2 * Math.PI * 450 * t)) * Env(t, 0.005f, 0.09f) * 0.35f);
            {
                const float len = 0.6f;
                var noise = NoiseBuffer(len, rnd, 0.6f);
                var hits = new float[9];
                for (int h = 0; h < hits.Length; h++) hits[h] = h * 0.06f + (float)rnd.NextDouble() * 0.02f;
                clips[(int)Sound.Shuffle] = Make("shuffle", len, (t, i) =>
                {
                    float a = 0;
                    foreach (float h in hits) if (t >= h) a += Env(t - h, 0.001f, 0.012f);
                    return noise[i] * a * 0.45f;
                });
            }
        }

        static float Env(float t, float attack, float decay)
        {
            if (t < 0) return 0;
            if (t < attack) return t / attack;
            return Mathf.Exp(-(t - attack) / decay);
        }

        static float Tone(float t, float f, float decay) => (float)Math.Sin(2 * Math.PI * f * t) * Env(t, 0.001f, decay);

        static float Bell(float t, float f, float start, float decay)
        {
            float u = t - start;
            if (u < 0) return 0;
            float e = Env(u, 0.004f, decay);
            return ((float)Math.Sin(2 * Math.PI * f * u) + 0.25f * (float)Math.Sin(2 * Math.PI * 2 * f * u) + 0.08f * (float)Math.Sin(2 * Math.PI * 3 * f * u)) * e;
        }

        static float Pluck(float t, float f, float start)
        {
            float u = t - start;
            if (u < 0 || u > 0.4f) return 0;
            float e = Env(u, 0.003f, 0.12f);
            double ph = 2 * Math.PI * f * u;
            return (float)(Math.Sin(ph) + 0.4 * Math.Sin(2 * ph) + 0.2 * Math.Sin(3 * ph)) * e;
        }

        static float[] NoiseBuffer(float seconds, System.Random rnd, float highpass)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var buf = new float[n];
            float prev = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                float w = (float)(rnd.NextDouble() * 2 - 1);
                lp += (w - lp) * (1 - highpass);
                buf[i] = highpass > 0 ? w - lp * highpass + (w - prev) * highpass : w;
                prev = w;
            }
            return buf;
        }

        AudioClip MakeNoise(string name, float seconds, System.Random rnd, float attack, float decay, float highpass, float gain)
        {
            var noise = NoiseBuffer(seconds, rnd, highpass);
            return Make(name, seconds, (t, i) => noise[i] * Env(t, attack, decay) * gain);
        }

        static AudioClip Make(string name, float seconds, Func<float, int, float> gen)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float v = gen(t, i);
                // short fade-out so clips never click at the end
                float tail = (n - i) / (Rate * 0.01f);
                if (tail < 1) v *= tail;
                data[i] = v;
                peak = Mathf.Max(peak, Mathf.Abs(v));
            }
            if (peak > 0.95f) for (int i = 0; i < n; i++) data[i] *= 0.95f / peak;
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
