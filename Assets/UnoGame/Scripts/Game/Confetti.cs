using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnoGame.Core;
using UnoGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.Game
{
    /// <summary>Falling confetti made of tinted UI quads.</summary>
    public sealed class Confetti : MonoBehaviour
    {
        struct Piece { public RectTransform rt; public Vector2 vel; public float spin; public float sway; public float phase; }
        readonly List<Piece> pieces = new List<Piece>();
        float life;

        public static void Burst(RectTransform layer, int count = 90, float seconds = 4f)
        {
            var go = UIKit.Node("Confetti", layer).Fill();
            var c = go.gameObject.AddComponent<Confetti>();
            c.Spawn(count, seconds);
        }

        void Spawn(int count, float seconds)
        {
            life = seconds;
            var rt = (RectTransform)transform;
            float w = rt.rect.width, h = rt.rect.height;
            Color[] colors = { Palette.Red, Palette.Yellow, Palette.Green, Palette.Blue, Color.white, Palette.Gold };
            var sprite = UIKit.S("rounded_small");
            for (int i = 0; i < count; i++)
            {
                var img = UIKit.Img(transform, "p", sprite, colors[i % colors.Length]);
                img.type = Image.Type.Simple;
                var prt = img.rectTransform;
                prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
                prt.sizeDelta = UnityEngine.Random.value < 0.5f ? new Vector2(18, 30) : new Vector2(16, 16);
                prt.anchoredPosition = new Vector2(UnityEngine.Random.Range(-w * 0.5f, w * 0.5f), h * 0.5f + UnityEngine.Random.Range(20f, 500f));
                prt.localEulerAngles = new Vector3(0, 0, UnityEngine.Random.Range(0f, 360f));
                pieces.Add(new Piece
                {
                    rt = prt,
                    vel = new Vector2(UnityEngine.Random.Range(-60f, 60f), -UnityEngine.Random.Range(380f, 720f)),
                    spin = UnityEngine.Random.Range(-360f, 360f),
                    sway = UnityEngine.Random.Range(40f, 120f),
                    phase = UnityEngine.Random.Range(0f, 6.28f),
                });
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            life -= dt;
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                p.phase += dt * 3f;
                p.rt.anchoredPosition += new Vector2(p.vel.x + Mathf.Sin(p.phase) * p.sway, p.vel.y) * dt;
                p.rt.Rotate(0, 0, p.spin * dt);
                var s = p.rt.localScale;
                s.x = Mathf.Abs(Mathf.Cos(p.phase * 0.7f)) * 0.8f + 0.2f;
                p.rt.localScale = s;
                pieces[i] = p;
            }
            if (life <= 0f) Destroy(gameObject);
        }
    }
}
