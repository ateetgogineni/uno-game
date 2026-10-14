using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.UI
{
    public sealed class Tweener : MonoBehaviour
    {
        static Tweener inst;
        readonly List<Tween> active = new List<Tween>();
        readonly List<Tween> adding = new List<Tween>();

        static Tweener I
        {
            get
            {
                if (inst == null)
                {
                    var go = new GameObject("Tweener");
                    DontDestroyOnLoad(go);
                    inst = go.AddComponent<Tweener>();
                }
                return inst;
            }
        }

        internal static Tween Add(Tween t)
        {
            var me = I;
            if (t.target != null && t.channel != null) KillWhere(x => ReferenceEquals(x.target, t.target) && x.channel == t.channel);
            me.adding.Add(t);
            return t;
        }

        internal static bool Any(Predicate<Tween> pred)
        {
            if (inst == null) return false;
            foreach (var t in inst.active) if (!t.Done && pred(t)) return true;
            foreach (var t in inst.adding) if (!t.Done && pred(t)) return true;
            return false;
        }

        internal static void KillWhere(Predicate<Tween> pred)
        {
            if (inst == null) return;
            foreach (var t in inst.active) if (!t.Done && pred(t)) t.Done = true;
            foreach (var t in inst.adding) if (!t.Done && pred(t)) t.Done = true;
        }

        static bool TargetGone(object target) => target is UnityEngine.Object o && o == null;

        void Update()
        {
            if (adding.Count > 0) { active.AddRange(adding); adding.Clear(); }
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < active.Count; i++)
            {
                var t = active[i];
                if (t.Done) continue;
                if (TargetGone(t.target)) { t.Done = true; continue; }
                float step = dt;
                if (t.delay > 0)
                {
                    t.delay -= step;
                    if (t.delay > 0) continue;
                    step = -t.delay;
                    t.delay = 0;
                }
                if (!t.started) { t.started = true; t.onStart?.Invoke(); }
                t.time += step;
                float k = t.duration <= 0 ? 1f : Mathf.Clamp01(t.time / t.duration);
                float eval = t.yoyoBack ? 1f - k : k;
                try { t.apply?.Invoke(Tw.Evaluate(t.ease, eval)); }
                catch (Exception e) { Debug.LogException(e); t.Done = true; continue; }
                if (k < 1f) continue;
                if (t.loops != 0)
                {
                    if (t.loops > 0) t.loops--;
                    t.time = 0;
                    if (t.yoyo) t.yoyoBack = !t.yoyoBack;
                    continue;
                }
                t.Done = true;
                try { t.onComplete?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            active.RemoveAll(x => x.Done);
        }
    }
}
