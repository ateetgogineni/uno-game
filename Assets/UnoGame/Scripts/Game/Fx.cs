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
    /// <summary>One-shot pop-up effects (icons, "+4", "UNO!") on the effects layer.</summary>
    public static class Fx
    {
        public static void PopSprite(RectTransform layer, Sprite sprite, Vector3 worldPos, Vector2 size, Color color, float hold = 0.5f, float rotate = 0f)
        {
            var img = UIKit.Img(layer, "Fx", sprite, color);
            img.type = Image.Type.Simple;
            var rt = img.rectTransform;
            rt.sizeDelta = size;
            rt.position = worldPos;
            rt.localScale = Vector3.zero;
            if (rotate != 0f) Tw.Spin(rt, rotate, 0.5f + hold, Ease.OutCubic);
            Tw.Scale(rt, 1f, 0.35f, Ease.OutBackStrong);
            Tw.Alpha(img, 0f, 0.3f, Ease.InQuad, 0.35f + hold).OnComplete(() => { if (img) UnityEngine.Object.Destroy(img.gameObject); });
            Tw.Pos(rt, rt.anchoredPosition + new Vector2(0, 40f), 0.65f + hold, Ease.OutCubic);
        }

        public static void PopText(RectTransform layer, string text, Vector3 worldPos, Color color, float size = 110f, float hold = 0.6f)
        {
            var t = UIKit.Txt(layer, "FxText", text, size, color, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic).Shadowed();
            var rt = t.rectTransform;
            rt.sizeDelta = new Vector2(500, 200);
            rt.position = worldPos;
            rt.localScale = Vector3.zero;
            Tw.Scale(rt, 1f, 0.4f, Ease.OutBackStrong);
            Tw.Pos(rt, rt.anchoredPosition + new Vector2(0, 90f), 0.75f + hold, Ease.OutCubic);
            Tw.Alpha(t, 0f, 0.3f, Ease.InQuad, 0.45f + hold).OnComplete(() => { if (t) UnityEngine.Object.Destroy(t.gameObject); });
        }
    }
}
