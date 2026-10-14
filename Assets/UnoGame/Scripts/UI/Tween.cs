using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.UI
{
    public enum Ease { Linear, InQuad, OutQuad, InOutQuad, InCubic, OutCubic, InOutCubic, OutBack, InBack, OutElastic, OutBounce, InOutSine, OutExpo, OutBackStrong }

    /// <summary>A running animation. Created through Tw.*; runs on unscaled time.</summary>
    public sealed class Tween
    {
        internal object target;
        internal string channel;
        internal float delay, duration, time;
        internal Ease ease;
        internal Action onStart;
        internal Action<float> apply;
        internal Action onComplete;
        internal bool started;
        internal int loops;   // -1 = forever
        internal bool yoyo;
        internal bool yoyoBack;

        public bool Done { get; internal set; }

        public Tween OnComplete(Action a) { onComplete += a; return this; }
        public Tween Loop(int count = -1, bool pingPong = false) { loops = count; yoyo = pingPong; return this; }
        public IEnumerator Wait() { while (!Done) yield return null; }

        public void Kill(bool complete = false)
        {
            if (Done) return;
            if (complete)
            {
                if (!started) { started = true; onStart?.Invoke(); }
                apply?.Invoke(Tw.Evaluate(ease, 1f));
                Done = true;
                onComplete?.Invoke();
            }
            Done = true;
        }
    }

    public static class Tw
    {
        public static Tween Value(float duration, Action<float> apply, Ease ease = Ease.OutCubic, float delay = 0, object target = null, string channel = null)
        {
            var t = new Tween { duration = duration, apply = apply, ease = ease, delay = delay, target = target, channel = channel };
            return Tweener.Add(t);
        }

        public static Tween Pos(RectTransform rt, Vector2 to, float duration, Ease ease = Ease.OutCubic, float delay = 0)
        {
            Vector2 from = default;
            var t = new Tween { duration = duration, ease = ease, delay = delay, target = rt, channel = "pos" };
            t.onStart = () => from = rt.anchoredPosition;
            t.apply = k => rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            return Tweener.Add(t);
        }

        /// <summary>Moves to a world position (used for cards flying between containers).</summary>
        public static Tween WorldPos(Transform tr, Vector3 to, float duration, Ease ease = Ease.OutCubic, float delay = 0)
        {
            Vector3 from = default;
            var t = new Tween { duration = duration, ease = ease, delay = delay, target = tr, channel = "pos" };
            t.onStart = () => from = tr.position;
            t.apply = k => tr.position = Vector3.LerpUnclamped(from, to, k);
            return Tweener.Add(t);
        }

        public static Tween Scale(Transform tr, Vector3 to, float duration, Ease ease = Ease.OutCubic, float delay = 0)
        {
            Vector3 from = default;
            var t = new Tween { duration = duration, ease = ease, delay = delay, target = tr, channel = "scale" };
            t.onStart = () => from = tr.localScale;
            t.apply = k => tr.localScale = Vector3.LerpUnclamped(from, to, k);
            return Tweener.Add(t);
        }

        public static Tween Scale(Transform tr, float to, float duration, Ease ease = Ease.OutCubic, float delay = 0) =>
            Scale(tr, new Vector3(to, to, 1f), duration, ease, delay);

        /// <summary>Rotates to angle z the short way round.</summary>
        public static Tween Rot(Transform tr, float z, float duration, Ease ease = Ease.OutCubic, float delay = 0)
        {
            float from = 0, to = 0;
            var t = new Tween { duration = duration, ease = ease, delay = delay, target = tr, channel = "rot" };
            t.onStart = () => { from = tr.localEulerAngles.z; to = from + Mathf.DeltaAngle(from, z); };
            t.apply = k => tr.localEulerAngles = new Vector3(0, 0, Mathf.LerpUnclamped(from, to, k));
            return Tweener.Add(t);
        }

        /// <summary>Rotates by a relative amount (full spins allowed).</summary>
        public static Tween Spin(Transform tr, float by, float duration, Ease ease = Ease.OutCubic, float delay = 0)
        {
            float from = 0;
            var t = new Tween { duration = duration, ease = ease, delay = delay, target = tr, channel = "rot" };
            t.onStart = () => from = tr.localEulerAngles.z;
            t.apply = k => tr.localEulerAngles = new Vector3(0, 0, from + by * k);
            return Tweener.Add(t);
        }

        public static Tween Alpha(CanvasGroup cg, float to, float duration, Ease ease = Ease.OutQuad, float delay = 0)
        {
            float from = 0;
            var t = new Tween { duration = duration, ease = ease, delay = delay, target = cg, channel = "alpha" };
            t.onStart = () => from = cg.alpha;
            t.apply = k => cg.alpha = Mathf.LerpUnclamped(from, to, k);
            return Tweener.Add(t);
        }

        public static Tween Alpha(Graphic g, float to, float duration, Ease ease = Ease.OutQuad, float delay = 0)
        {
            float from = 0;
            var t = new Tween { duration = duration, ease = ease, delay = delay, target = g, channel = "alpha" };
            t.onStart = () => from = g.color.a;
            t.apply = k => { var c = g.color; c.a = Mathf.LerpUnclamped(from, to, k); g.color = c; };
            return Tweener.Add(t);
        }

        public static Tween Color(Graphic g, Color to, float duration, Ease ease = Ease.OutQuad, float delay = 0)
        {
            UnityEngine.Color from = default;
            var t = new Tween { duration = duration, ease = ease, delay = delay, target = g, channel = "alpha" };
            t.onStart = () => from = g.color;
            t.apply = k => g.color = UnityEngine.Color.LerpUnclamped(from, to, k);
            return Tweener.Add(t);
        }

        public static Tween Size(RectTransform rt, Vector2 to, float duration, Ease ease = Ease.OutCubic, float delay = 0)
        {
            Vector2 from = default;
            var t = new Tween { duration = duration, ease = ease, delay = delay, target = rt, channel = "size" };
            t.onStart = () => from = rt.sizeDelta;
            t.apply = k => rt.sizeDelta = Vector2.LerpUnclamped(from, to, k);
            return Tweener.Add(t);
        }

        // Resting scale of transforms that are mid-punch, so back-to-back punches don't compound.
        static readonly Dictionary<Transform, Vector3> punchRest = new Dictionary<Transform, Vector3>();

        /// <summary>Quick scale punch (button presses, badges).</summary>
        public static Tween Punch(Transform tr, float amount = 0.15f, float duration = 0.35f)
        {
            if (!punchRest.TryGetValue(tr, out Vector3 baseScale) || !HasRunning(tr, "scale"))
            {
                baseScale = tr.localScale;
                punchRest[tr] = baseScale;
            }
            var t = new Tween { duration = duration, ease = Ease.Linear, target = tr, channel = "scale" };
            t.apply = k => tr.localScale = baseScale * (1f + amount * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f));
            t.onComplete = () => { tr.localScale = baseScale; punchRest.Remove(tr); };
            return Tweener.Add(t);
        }

        static bool HasRunning(object target, string channel) => Tweener.Any(x => ReferenceEquals(x.target, target) && x.channel == channel);

        public static Tween Shake(RectTransform rt, float strength = 14f, float duration = 0.35f)
        {
            Vector2 origin = rt.anchoredPosition;
            var t = new Tween { duration = duration, ease = Ease.Linear, target = rt, channel = "shake" };
            t.apply = k => rt.anchoredPosition = origin + new Vector2(Mathf.Sin(k * 50f) * strength * (1f - k), 0);
            t.onComplete = () => rt.anchoredPosition = origin;
            return Tweener.Add(t);
        }

        public static void Kill(object target) => Tweener.KillWhere(x => ReferenceEquals(x.target, target));

        public static void Kill(object target, string channel) =>
            Tweener.KillWhere(x => ReferenceEquals(x.target, target) && x.channel == channel);

        /// <summary>Wait on unscaled time (also correct while capturing video at a fixed frame rate).</summary>
        public static IEnumerator Wait(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) yield return null;
        }

        public static float Evaluate(Ease e, float k)
        {
            switch (e)
            {
                case Ease.Linear: return k;
                case Ease.InQuad: return k * k;
                case Ease.OutQuad: return 1 - (1 - k) * (1 - k);
                case Ease.InOutQuad: return k < 0.5f ? 2 * k * k : 1 - Mathf.Pow(-2 * k + 2, 2) / 2;
                case Ease.InCubic: return k * k * k;
                case Ease.OutCubic: return 1 - Mathf.Pow(1 - k, 3);
                case Ease.InOutCubic: return k < 0.5f ? 4 * k * k * k : 1 - Mathf.Pow(-2 * k + 2, 3) / 2;
                case Ease.OutBack: { const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(k - 1, 3) + c1 * Mathf.Pow(k - 1, 2); }
                case Ease.OutBackStrong: { const float c1 = 2.6f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(k - 1, 3) + c1 * Mathf.Pow(k - 1, 2); }
                case Ease.InBack: { const float c1 = 1.70158f, c3 = c1 + 1; return c3 * k * k * k - c1 * k * k; }
                case Ease.OutElastic:
                {
                    if (k <= 0) return 0;
                    if (k >= 1) return 1;
                    const float c4 = 2 * Mathf.PI / 3;
                    return Mathf.Pow(2, -10 * k) * Mathf.Sin((k * 10 - 0.75f) * c4) + 1;
                }
                case Ease.OutBounce:
                {
                    const float n1 = 7.5625f, d1 = 2.75f;
                    if (k < 1 / d1) return n1 * k * k;
                    if (k < 2 / d1) { k -= 1.5f / d1; return n1 * k * k + 0.75f; }
                    if (k < 2.5f / d1) { k -= 2.25f / d1; return n1 * k * k + 0.9375f; }
                    k -= 2.625f / d1;
                    return n1 * k * k + 0.984375f;
                }
                case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * k) - 1) / 2;
                case Ease.OutExpo: return k >= 1 ? 1 : 1 - Mathf.Pow(2, -10 * k);
                default: return k;
            }
        }
    }
}
